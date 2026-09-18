using System.Net;
using System.Net.Http.Headers;
using System.Text;
using FluentAssertions;
using IntegratoR.Abstractions.Common.Batch;
using IntegratoR.Abstractions.Interfaces.Authentication;
using IntegratoR.OData.Common.Extensions;
using IntegratoR.OData.Common.Services;
using IntegratoR.OData.Domain.Models;
using IntegratoR.OData.Domain.Settings;
using IntegratoR.TestKit.Fakes;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using PanoramicData.OData.Client;
using Xunit;

namespace IntegratoR.OData.Tests.Common.Services;

/// <summary>
/// Pins the atomic <c>$batch</c> wire path in <see cref="ODataClientAdapter"/>: an <c>Atomic</c> batch
/// sends ONE multipart <c>$batch</c> POST (a changeset) and maps the all-or-nothing changeset outcome
/// onto per-operation results — every operation succeeds, or (on a rolled-back changeset) every
/// operation fails.
/// </summary>
public sealed class ODataClientAdapterAtomicBatchTests : IDisposable
{
    private const string BaseUrl = "https://host/data";
    private const string EntitySet = "LedgerJournalHeaders";

    private readonly List<ServiceProvider> _providers = [];

    private (ServiceProvider Provider, FakeHttpMessageHandler Handler) BuildHarness()
    {
        var fakeHandler = new FakeHttpMessageHandler();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IAuthenticator>(Substitute.For<IAuthenticator>());
        services.AddODataClient(options =>
        {
            options.Url = BaseUrl;
            options.Authentication.Mode = AuthenticationMode.ApiKey;
            options.Authentication.ApiManagement.SubscriptionKey = "test-key";
            options.Authentication.ApiManagement.SubscriptionHeaderKey = "Ocp-Apim-Subscription-Key";
            options.Resilience.EnableRetries = false;
            options.Resilience.UseCircuitBreaker = false;
        });
        services.AddHttpClient("ODataClient").ConfigurePrimaryHttpMessageHandler(() => fakeHandler);

        var provider = services.BuildServiceProvider();
        _providers.Add(provider);
        return (provider, fakeHandler);
    }

    private static ODataClientAdapter ResolveAdapter(ServiceProvider provider) =>
        new(provider.GetRequiredService<ODataClient>(), provider.GetRequiredService<IHttpClientFactory>());

    private static string Wire(params string[] lines) => string.Join("\r\n", lines) + "\r\n";

    private static HttpResponseMessage MultipartResponse(string body, string boundary = "resp")
    {
        var content = new StringContent(body, Encoding.UTF8);
        content.Headers.ContentType = MediaTypeHeaderValue.Parse($"multipart/mixed; boundary={boundary}");
        return new HttpResponseMessage(HttpStatusCode.OK) { Content = content };
    }

    private static IReadOnlyList<(object Key, IDictionary<string, object> Payload)> TwoUpdates() =>
    [
        (new Dictionary<string, object> { ["dataAreaId"] = "USMF", ["JournalBatchNumber"] = "B1" },
            new Dictionary<string, object> { ["Description"] = "a" }),
        (new Dictionary<string, object> { ["dataAreaId"] = "USMF", ["JournalBatchNumber"] = "B2" },
            new Dictionary<string, object> { ["Description"] = "b" }),
    ];

    [Fact]
    public async Task BatchUpdate_Atomic_SendsSingleBatchPost_AndMapsChangesetSuccess()
    {
        (ServiceProvider provider, FakeHttpMessageHandler handler) = BuildHarness();
        ODataClientAdapter adapter = ResolveAdapter(provider);

        handler.Queue(MultipartResponse(Wire(
            "--resp",
            "Content-Type: multipart/mixed; boundary=cs",
            "",
            "--cs",
            "Content-Type: application/http",
            "Content-ID: 1",
            "",
            "HTTP/1.1 204 No Content",
            "",
            "--cs",
            "Content-Type: application/http",
            "Content-ID: 2",
            "",
            "HTTP/1.1 204 No Content",
            "",
            "--cs--",
            "--resp--")));

        IReadOnlyList<BatchOperationResult> results =
            await adapter.BatchUpdateAsync(EntitySet, TwoUpdates(), BatchFailureMode.Atomic, CancellationToken.None);

        handler.SentRequests.Should().ContainSingle(because: "an atomic batch is ONE $batch POST");
        handler.SentRequests[0].Method.Should().Be(HttpMethod.Post);
        handler.SentRequests[0].RequestUri!.ToString().Should().EndWith("/data/$batch");
        results.Should().HaveCount(2).And.OnlyContain(r => r.IsSuccess);
    }

    [Fact]
    public async Task BatchUpdate_Atomic_ChangesetRolledBack_MarksEveryItemFailed()
    {
        (ServiceProvider provider, FakeHttpMessageHandler handler) = BuildHarness();
        ODataClientAdapter adapter = ResolveAdapter(provider);

        // A failed changeset collapses to a single error response -> the whole batch rolled back.
        handler.Queue(MultipartResponse(Wire(
            "--resp",
            "Content-Type: multipart/mixed; boundary=cs",
            "",
            "--cs",
            "Content-Type: application/http",
            "",
            "HTTP/1.1 400 Bad Request",
            "Content-Type: application/json",
            "",
            "{\"error\":{\"code\":\"X\",\"message\":\"nope\"}}",
            "--cs--",
            "--resp--")));

        IReadOnlyList<BatchOperationResult> results =
            await adapter.BatchUpdateAsync(EntitySet, TwoUpdates(), BatchFailureMode.Atomic, CancellationToken.None);

        handler.SentRequests.Should().ContainSingle();
        results.Should().HaveCount(2).And.OnlyContain(r => !r.IsSuccess && r.StatusCode == 400);
    }

    [Fact]
    public async Task BatchCreate_Atomic_SendsSingleBatchPost()
    {
        (ServiceProvider provider, FakeHttpMessageHandler handler) = BuildHarness();
        ODataClientAdapter adapter = ResolveAdapter(provider);

        handler.Queue(MultipartResponse(Wire(
            "--resp",
            "Content-Type: multipart/mixed; boundary=cs",
            "",
            "--cs",
            "Content-Type: application/http",
            "Content-ID: 1",
            "",
            "HTTP/1.1 201 Created",
            "",
            "--cs--",
            "--resp--")));

        var payloads = new List<IDictionary<string, object>>
        {
            new Dictionary<string, object> { ["dataAreaId"] = "USMF", ["JournalName"] = "GenJrn" },
        };

        IReadOnlyList<BatchOperationResult> results =
            await adapter.BatchCreateAsync(EntitySet, payloads, BatchFailureMode.Atomic, CancellationToken.None);

        handler.SentRequests.Should().ContainSingle();
        handler.SentRequests[0].RequestUri!.ToString().Should().EndWith("/data/$batch");
        results.Should().ContainSingle(r => r.IsSuccess);
    }

    /// <summary>
    /// The atomic body is assembled as text, so a key value carrying CR or LF would terminate the
    /// embedded <c>METHOD url HTTP/1.1</c> request line early and inject headers — or a forged
    /// request — into that part. The per-item path is shielded by <see cref="Uri"/>; this one is not.
    /// </summary>
    [Theory]
    [InlineData("USMF') HTTP/1.1\r\nX-Injected: 1\r\n\r\n{\"evil\":\"body\"}")]
    [InlineData("USMF\r\nX-Injected: 1")]
    [InlineData("USMF\n")]
    public async Task BatchUpdate_Atomic_CompositeKeyValueWithControlCharacters_ThrowsAndSendsNothing(
        string tainted)
    {
        (ServiceProvider provider, FakeHttpMessageHandler handler) = BuildHarness();
        ODataClientAdapter adapter = ResolveAdapter(provider);

        var items = new List<(object Key, IDictionary<string, object> Payload)>
        {
            (new Dictionary<string, object> { ["dataAreaId"] = tainted, ["JournalBatchNumber"] = "B1" },
                new Dictionary<string, object> { ["Description"] = "a" })
        };

        Func<Task> act = () =>
            adapter.BatchUpdateAsync(EntitySet, items, BatchFailureMode.Atomic, CancellationToken.None);

        await act.Should().ThrowAsync<ArgumentException>().WithMessage("*control characters*");
        handler.SentRequests.Should().BeEmpty(because: "the guard runs before anything is sent");
    }

    /// <summary>
    /// For a batch create the entity set is the whole relative URL, so it reaches the request line
    /// with nothing around it. The only production caller passes a <c>[Table]</c>-derived name, but
    /// the adapter is public API and the parameter is a bare string.
    /// </summary>
    [Fact]
    public async Task BatchCreate_Atomic_EntitySetWithControlCharacters_ThrowsAndSendsNothing()
    {
        (ServiceProvider provider, FakeHttpMessageHandler handler) = BuildHarness();
        ODataClientAdapter adapter = ResolveAdapter(provider);

        var payloads = new List<IDictionary<string, object>>
        {
            new Dictionary<string, object> { ["Description"] = "a" }
        };

        Func<Task> act = () => adapter.BatchCreateAsync(
            "LedgerJournalHeaders\r\nX-Injected: 1", payloads, BatchFailureMode.Atomic, CancellationToken.None);

        await act.Should().ThrowAsync<ArgumentException>().WithMessage("*not a valid OData identifier*");
        handler.SentRequests.Should().BeEmpty();
    }

    [Fact]
    public async Task BatchDelete_Atomic_ScalarKeyWithControlCharacters_ThrowsAndSendsNothing()
    {
        (ServiceProvider provider, FakeHttpMessageHandler handler) = BuildHarness();
        ODataClientAdapter adapter = ResolveAdapter(provider);

        var keys = new List<object> { "B1') HTTP/1.1\r\nX-Injected: 1" };

        Func<Task> act = () =>
            adapter.BatchDeleteAsync(EntitySet, keys, BatchFailureMode.Atomic, CancellationToken.None);

        await act.Should().ThrowAsync<ArgumentException>().WithMessage("*control characters*");
        handler.SentRequests.Should().BeEmpty();
    }

    /// <summary>
    /// Fewer sub-responses than operations, all of them 2xx: the changeset did not commit and nothing
    /// in the response says which operation failed, so no operation may carry a success status.
    /// </summary>
    [Fact]
    public async Task BatchUpdate_Atomic_UnreconcilableResponse_DoesNotReportASuccessStatus()
    {
        (ServiceProvider provider, FakeHttpMessageHandler handler) = BuildHarness();
        ODataClientAdapter adapter = ResolveAdapter(provider);

        handler.Queue(MultipartResponse(Wire(
            "--resp",
            "Content-Type: multipart/mixed; boundary=cs",
            "",
            "--cs",
            "Content-Type: application/http",
            "Content-ID: 1",
            "",
            "HTTP/1.1 204 No Content",
            "",
            "--cs--",
            "--resp--")));

        IReadOnlyList<BatchOperationResult> results =
            await adapter.BatchUpdateAsync(EntitySet, TwoUpdates(), BatchFailureMode.Atomic, CancellationToken.None);

        results.Should().HaveCount(2).And.OnlyContain(r => !r.IsSuccess);
        results.Should().OnlyContain(r => r.StatusCode == 502,
            because: "borrowing the 204 would stamp a success code on a failed operation");
    }

    /// <inheritdoc />
    public void Dispose()
    {
        foreach (ServiceProvider provider in _providers)
        {
            provider.Dispose();
        }
    }
}
