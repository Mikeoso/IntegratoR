using System.Net;
using System.Net.Http.Headers;
using FluentAssertions;
using IntegratoR.OData.Common.Services;
using IntegratoR.OData.Domain.Models;
using NSubstitute;
using PanoramicData.OData.Client;
using Xunit;

namespace IntegratoR.OData.Tests.Common.Services;

/// <summary>
/// Covers the <c>$batch</c> write path in <see cref="ODataClientAdapter"/>: that it issues exactly one
/// request, emits the OData v4.01 multipart shape, and maps the changeset outcome onto per-operation
/// <see cref="BatchOperationResult"/>s.
/// </summary>
public class ODataClientAdapterBatchTests
{
    private const string BaseUrl = "https://d365.example.com/data/";
    private const string EntitySet = "LedgerJournalHeaders";

    private readonly CapturingHandler _handler = new();

    /// <summary>
    /// Records each request together with its body text. <c>FakeHttpMessageHandler</c> stores the
    /// <see cref="HttpRequestMessage"/> itself, which is not enough here: the adapter disposes the
    /// request — and with it the content — as soon as the send completes, so the body has to be read
    /// inside the handler rather than from a stored request afterwards.
    /// </summary>
    private sealed class CapturingHandler : HttpMessageHandler
    {
        private readonly Queue<HttpResponseMessage> _responses = new();

        internal sealed record SentRequest(HttpMethod Method, Uri? Uri, MediaTypeHeaderValue? ContentType, string Body);

        public List<SentRequest> Sent { get; } = [];

        public void Queue(HttpResponseMessage response) => _responses.Enqueue(response);

        public HttpClient CreateClient() => new(this, disposeHandler: false) { BaseAddress = new Uri(BaseUrl) };

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            string body = request.Content is null
                ? string.Empty
                : await request.Content.ReadAsStringAsync(cancellationToken);

            Sent.Add(new SentRequest(
                request.Method, request.RequestUri, request.Content?.Headers.ContentType, body));

            return _responses.Count > 0
                ? _responses.Dequeue()
                : throw new InvalidOperationException("No queued response. Call Queue() first.");
        }
    }

    private ODataClientAdapter CreateAdapter()
    {
        HttpClient httpClient = _handler.CreateClient();

        IHttpClientFactory factory = Substitute.For<IHttpClientFactory>();
        factory.CreateClient("ODataClient").Returns(httpClient);

        var options = new ODataClientOptions { BaseUrl = BaseUrl, HttpClient = httpClient };
        return new ODataClientAdapter(new ODataClient(options), factory);
    }

    /// <summary>Queues a multipart batch response, carrying the boundary on the Content-Type header.</summary>
    private void QueueBatchResponse(HttpStatusCode outerStatus, string boundary, string body)
    {
        var response = new HttpResponseMessage(outerStatus) { Content = new StringContent(body) };
        response.Content.Headers.ContentType =
            MediaTypeHeaderValue.Parse($"multipart/mixed; boundary={boundary}");
        _handler.Queue(response);
    }

    private static string Wire(params string[] lines) => string.Join("\r\n", lines) + "\r\n";

    private static IDictionary<string, object> Payload(string journal) => new Dictionary<string, object>
    {
        ["dataAreaId"] = "USMF",
        ["JournalBatchNumber"] = journal
    };

    private static IDictionary<string, object> Key(string journal) => new Dictionary<string, object>
    {
        ["dataAreaId"] = "USMF",
        ["JournalBatchNumber"] = journal
    };

    private static List<IDictionary<string, object>> TwoPayloads() => [Payload("B1"), Payload("B2")];

    /// <summary>Two creates, both committed, correlated by Content-ID.</summary>
    private static string TwoCreatesCommitted() => Wire(
        "--b1",
        "Content-Type: multipart/mixed; boundary=cs1",
        "",
        "--cs1",
        "Content-Type: application/http",
        "Content-ID: 1",
        "",
        "HTTP/1.1 201 Created",
        "",
        "--cs1",
        "Content-Type: application/http",
        "Content-ID: 2",
        "",
        "HTTP/1.1 201 Created",
        "",
        "--cs1--",
        "--b1--");

    /// <summary>A changeset holding one committed operation.</summary>
    private static string OneCommitted(int status, string reason) => Wire(
        "--b1", "Content-Type: multipart/mixed; boundary=cs1", "",
        "--cs1", "Content-Type: application/http", "Content-ID: 1", "",
        $"HTTP/1.1 {status} {reason}", "",
        "--cs1--", "--b1--");

    private static int CountOf(string haystack, string needle) =>
        System.Text.RegularExpressions.Regex.Matches(
            haystack, System.Text.RegularExpressions.Regex.Escape(needle)).Count;

    /// <summary>
    /// Regression for the reported production failure. PanoramicData's changeset path serialised every
    /// sub-request through <c>HttpMessageContent</c>, which reads <c>Uri.PathAndQuery</c> on a relative
    /// sub-request URI and threw <see cref="InvalidOperationException"/> ("This operation is not
    /// supported for a relative URI") before a single byte reached the network.
    /// </summary>
    [Fact]
    public async Task BatchCreateAsync_RelativeSubRequestUris_DoesNotThrow()
    {
        // Arrange
        ODataClientAdapter adapter = CreateAdapter();
        QueueBatchResponse(HttpStatusCode.OK, "b1", TwoCreatesCommitted());

        // Act
        Func<Task> act = () => adapter.BatchCreateAsync(EntitySet, TwoPayloads(), TestContext.Current.CancellationToken);

        // Assert
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task BatchCreateAsync_SendsOnePostToBatchEndpoint()
    {
        // Arrange
        ODataClientAdapter adapter = CreateAdapter();
        QueueBatchResponse(HttpStatusCode.OK, "b1", TwoCreatesCommitted());

        // Act
        await adapter.BatchCreateAsync(EntitySet, TwoPayloads(), TestContext.Current.CancellationToken);

        // Assert
        _handler.Sent.Should().ContainSingle();
        CapturingHandler.SentRequest sent = _handler.Sent[0];
        sent.Method.Should().Be(HttpMethod.Post);
        sent.Uri!.ToString().Should().Be(BaseUrl + "$batch");
        sent.ContentType!.MediaType.Should().Be("multipart/mixed");
        sent.ContentType.Parameters.Should().Contain(p => p.Name == "boundary");
    }

    [Fact]
    public async Task BatchCreateAsync_EmitsOnePostPerPayloadInsideOneChangeset()
    {
        // Arrange
        ODataClientAdapter adapter = CreateAdapter();
        QueueBatchResponse(HttpStatusCode.OK, "b1", TwoCreatesCommitted());

        // Act
        await adapter.BatchCreateAsync(EntitySet, TwoPayloads(), TestContext.Current.CancellationToken);

        // Assert
        string body = _handler.Sent[0].Body;
        body.Should().Contain("Content-Type: multipart/mixed; boundary=changeset_");
        CountOf(body, $"POST {EntitySet} HTTP/1.1").Should().Be(2);
        body.Should().Contain("Content-ID: 1").And.Contain("Content-ID: 2");
        body.Should().Contain("\"JournalBatchNumber\":\"B1\"").And.Contain("\"JournalBatchNumber\":\"B2\"");
    }

    [Fact]
    public async Task BatchCreateAsync_ChangesetCommitted_MapsEveryOperationToSuccess()
    {
        // Arrange
        ODataClientAdapter adapter = CreateAdapter();
        QueueBatchResponse(HttpStatusCode.OK, "b1", TwoCreatesCommitted());

        // Act
        IReadOnlyList<BatchOperationResult> results =
            await adapter.BatchCreateAsync(EntitySet, TwoPayloads(), TestContext.Current.CancellationToken);

        // Assert
        results.Should().HaveCount(2);
        results.Should().OnlyContain(r => r.IsSuccess);
        results.Select(r => r.Index).Should().Equal(0, 1);
        results.Select(r => r.StatusCode).Should().Equal(201, 201);
    }

    /// <summary>
    /// An atomic changeset collapses to a single error sub-response when it rolls back. Nothing was
    /// applied, so every operation must be reported as failed — not just the one D365 named.
    /// </summary>
    [Fact]
    public async Task BatchCreateAsync_ChangesetRolledBack_MapsEveryOperationToFailureWithServerError()
    {
        // Arrange
        ODataClientAdapter adapter = CreateAdapter();
        QueueBatchResponse(HttpStatusCode.OK, "b1", Wire(
            "--b1",
            "Content-Type: multipart/mixed; boundary=cs1",
            "",
            "--cs1",
            "Content-Type: application/http",
            "",
            "HTTP/1.1 400 Bad Request",
            "Content-Type: application/json",
            "",
            "{\"error\":{\"code\":\"InvalidJournal\",\"message\":\"Journal B2 is closed\"}}",
            "--cs1--",
            "--b1--"));

        // Act
        IReadOnlyList<BatchOperationResult> results =
            await adapter.BatchCreateAsync(EntitySet, TwoPayloads(), TestContext.Current.CancellationToken);

        // Assert
        results.Should().HaveCount(2);
        results.Should().OnlyContain(r => !r.IsSuccess);
        results.Should().OnlyContain(r => r.StatusCode == 400);
        results.Should().OnlyContain(r => r.ResponseBody!.Contains("Journal B2 is closed"));
    }

    /// <summary>A rejected outer request (auth, malformed body) means nothing ran at all.</summary>
    [Fact]
    public async Task BatchCreateAsync_OuterRequestRejected_MapsEveryOperationToFailure()
    {
        // Arrange
        ODataClientAdapter adapter = CreateAdapter();
        _handler.Queue(new HttpResponseMessage(HttpStatusCode.Unauthorized)
        {
            Content = new StringContent("unauthorized")
        });

        // Act
        IReadOnlyList<BatchOperationResult> results =
            await adapter.BatchCreateAsync(EntitySet, TwoPayloads(), TestContext.Current.CancellationToken);

        // Assert
        results.Should().HaveCount(2);
        results.Should().OnlyContain(r => !r.IsSuccess && r.StatusCode == 401);
    }

    [Fact]
    public async Task BatchUpdateAsync_EmitsPatchAgainstTheCompositeKeyUrl()
    {
        // Arrange
        ODataClientAdapter adapter = CreateAdapter();
        QueueBatchResponse(HttpStatusCode.OK, "b1", OneCommitted(204, "No Content"));
        var items = new List<(object Key, IDictionary<string, object> Payload)>
        {
            (Key("B1"), new Dictionary<string, object> { ["Description"] = "updated" })
        };

        // Act
        IReadOnlyList<BatchOperationResult> results =
            await adapter.BatchUpdateAsync(EntitySet, items, TestContext.Current.CancellationToken);

        // Assert
        _handler.Sent[0].Body.Should()
            .Contain($"PATCH {EntitySet}(dataAreaId='USMF',JournalBatchNumber='B1') HTTP/1.1")
            .And.Contain("\"Description\":\"updated\"");
        results.Should().ContainSingle().Which.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task BatchDeleteAsync_EmitsDeleteAgainstTheCompositeKeyUrl_WithNoJsonBody()
    {
        // Arrange
        ODataClientAdapter adapter = CreateAdapter();
        QueueBatchResponse(HttpStatusCode.OK, "b1", OneCommitted(204, "No Content"));
        var keys = new List<object> { Key("B1") };

        // Act
        IReadOnlyList<BatchOperationResult> results =
            await adapter.BatchDeleteAsync(EntitySet, keys, TestContext.Current.CancellationToken);

        // Assert
        _handler.Sent[0].Body.Should()
            .Contain($"DELETE {EntitySet}(dataAreaId='USMF',JournalBatchNumber='B1') HTTP/1.1")
            .And.NotContain("Content-Type: application/json");
        results.Should().ContainSingle().Which.IsSuccess.Should().BeTrue();
    }

    /// <summary>
    /// The single-argument constructor has no named client to send the <c>$batch</c> through. It never
    /// supported batch writes — the old changeset path threw too — so it fails with a message naming
    /// the constructor to use rather than a relative-URI error from deep inside PanoramicData.
    /// </summary>
    [Fact]
    public async Task BatchCreateAsync_WithoutHttpClientFactory_ThrowsNamingTheRequiredConstructor()
    {
        // Arrange
        var httpClient = new HttpClient { BaseAddress = new Uri(BaseUrl) };
        var options = new ODataClientOptions { BaseUrl = BaseUrl, HttpClient = httpClient };
        var adapter = new ODataClientAdapter(new ODataClient(options));

        // Act
        Func<Task> act = () => adapter.BatchCreateAsync(EntitySet, TwoPayloads(), TestContext.Current.CancellationToken);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*IHttpClientFactory-based constructor*");
    }

    [Fact]
    public async Task BatchCreateAsync_EmptyPayloads_SendsNothing()
    {
        // Arrange
        ODataClientAdapter adapter = CreateAdapter();

        // Act
        IReadOnlyList<BatchOperationResult> results =
            await adapter.BatchCreateAsync(
                EntitySet, new List<IDictionary<string, object>>(), TestContext.Current.CancellationToken);

        // Assert
        results.Should().BeEmpty();
        _handler.Sent.Should().BeEmpty();
    }

    /// <summary>
    /// The batch body is assembled as text, so a key value carrying CR or LF would terminate the
    /// embedded <c>METHOD url HTTP/1.1</c> request line early and inject headers — or a forged
    /// request — into that part. The read path is shielded by <see cref="Uri"/>; this path is not,
    /// so the guard has to live in the URL builder.
    /// </summary>
    [Theory]
    [InlineData("USMF') HTTP/1.1\r\nX-Injected: 1\r\n\r\n{\"evil\":\"body\"}")]
    [InlineData("USMF\r\nX-Injected: 1")]
    [InlineData("USMF\n")]
    [InlineData("USMF\tB")]
    public async Task BatchUpdateAsync_CompositeKeyValueWithControlCharacters_ThrowsAndSendsNothing(
        string tainted)
    {
        // Arrange
        ODataClientAdapter adapter = CreateAdapter();
        var items = new List<(object Key, IDictionary<string, object> Payload)>
        {
            (new Dictionary<string, object>
                {
                    ["dataAreaId"] = tainted,
                    ["JournalBatchNumber"] = "B1"
                },
                new Dictionary<string, object> { ["Description"] = "x" })
        };

        // Act
        Func<Task> act = () =>
            adapter.BatchUpdateAsync(EntitySet, items, TestContext.Current.CancellationToken);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>().WithMessage("*control characters*");
        _handler.Sent.Should().BeEmpty();
    }

    [Fact]
    public async Task BatchDeleteAsync_ScalarKeyWithControlCharacters_ThrowsAndSendsNothing()
    {
        // Arrange
        ODataClientAdapter adapter = CreateAdapter();
        var keys = new List<object> { "B1') HTTP/1.1\r\nX-Injected: 1" };

        // Act
        Func<Task> act = () =>
            adapter.BatchDeleteAsync(EntitySet, keys, TestContext.Current.CancellationToken);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>().WithMessage("*control characters*");
        _handler.Sent.Should().BeEmpty();
    }

    /// <summary>
    /// Correlation is by <c>Content-ID</c>, not by position. Every other fixture here returns
    /// sub-responses in request order, so the positional fallback alone would satisfy them even if
    /// Content-ID matching were broken — this one returns them reversed to pin the real behaviour.
    /// </summary>
    [Fact]
    public async Task BatchCreateAsync_SubResponsesOutOfOrder_CorrelatesByContentId()
    {
        // Arrange
        ODataClientAdapter adapter = CreateAdapter();
        QueueBatchResponse(HttpStatusCode.OK, "b1", Wire(
            "--b1", "Content-Type: multipart/mixed; boundary=cs1", "",
            "--cs1", "Content-Type: application/http", "Content-ID: 2", "",
            "HTTP/1.1 204 No Content", "",
            "--cs1", "Content-Type: application/http", "Content-ID: 1", "",
            "HTTP/1.1 201 Created", "",
            "--cs1--", "--b1--"));

        // Act
        IReadOnlyList<BatchOperationResult> results =
            await adapter.BatchCreateAsync(EntitySet, TwoPayloads(), TestContext.Current.CancellationToken);

        // Assert — operation 0 carries Content-ID 1 (201), operation 1 carries Content-ID 2 (204).
        // Positional correlation would yield 204, 201.
        results.Select(r => r.StatusCode).Should().Equal(201, 204);
    }

    [Fact]
    public async Task BatchDeleteAsync_ChangesetRolledBack_MapsEveryOperationToFailure()
    {
        // Arrange
        ODataClientAdapter adapter = CreateAdapter();
        QueueBatchResponse(HttpStatusCode.OK, "b1", Wire(
            "--b1", "Content-Type: multipart/mixed; boundary=cs1", "",
            "--cs1", "Content-Type: application/http", "",
            "HTTP/1.1 409 Conflict", "Content-Type: application/json", "",
            "{\"error\":{\"code\":\"Locked\",\"message\":\"Journal B2 is in use\"}}",
            "--cs1--", "--b1--"));
        var keys = new List<object> { Key("B1"), Key("B2") };

        // Act
        IReadOnlyList<BatchOperationResult> results =
            await adapter.BatchDeleteAsync(EntitySet, keys, TestContext.Current.CancellationToken);

        // Assert
        results.Should().HaveCount(2);
        results.Should().OnlyContain(r => !r.IsSuccess && r.StatusCode == 409);
        results.Should().OnlyContain(r => r.ResponseBody!.Contains("Journal B2 is in use"));
    }

    /// <summary>
    /// Fewer sub-responses than operations, all of them 2xx: the changeset outcome is ambiguous, so
    /// the conservative reading is that nothing can be reported as committed.
    /// </summary>
    [Fact]
    public async Task BatchCreateAsync_FewerSubResponsesThanOperations_DoesNotReportSuccess()
    {
        // Arrange
        ODataClientAdapter adapter = CreateAdapter();
        QueueBatchResponse(HttpStatusCode.OK, "b1", OneCommitted(201, "Created"));

        // Act
        IReadOnlyList<BatchOperationResult> results =
            await adapter.BatchCreateAsync(EntitySet, TwoPayloads(), TestContext.Current.CancellationToken);

        // Assert
        results.Should().HaveCount(2);
        results.Should().OnlyContain(r => !r.IsSuccess);
    }
}
