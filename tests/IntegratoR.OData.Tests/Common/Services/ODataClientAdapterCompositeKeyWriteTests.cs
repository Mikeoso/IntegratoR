using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using FluentAssertions;
using IntegratoR.Abstractions.Domain.Entities;
using IntegratoR.Abstractions.Interfaces.Authentication;
using IntegratoR.OData.Common.Extensions;
using IntegratoR.OData.Common.Services;
using IntegratoR.OData.Domain.Settings;
using IntegratoR.OData.Interfaces.Services;
using IntegratoR.TestKit.Fakes;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NSubstitute;
using PanoramicData.OData.Client;
using PanoramicData.OData.Client.Exceptions;
using Xunit;

namespace IntegratoR.OData.Tests.Common.Services;

/// <summary>
/// Pins the composite-key WRITE bypass in <see cref="ODataClientAdapter"/>: D365 F&O composite
/// (dictionary) keys cannot be bound by PanoramicData's <c>Key(object)</c> path, so Update/Delete
/// and the batch variants issue raw HTTP requests through the named <c>"ODataClient"</c> client
/// (carrying auth + Polly + BaseAddress). These tests wire a <see cref="FakeHttpMessageHandler"/>
/// as that client's primary handler — the same pattern as
/// <c>AddODataClient_PreservesPathSegment_WhenComposingRelativeRequestUri</c>.
/// </summary>
public sealed class ODataClientAdapterCompositeKeyWriteTests : IDisposable
{
    private const string BaseUrl = "https://lbbw-im-api-management.azure-api.net/fo";
    private const string EntitySet = "TestJournalHeaders";

    // Every provider built by BuildHarness is tracked here and disposed in Dispose() so the named
    // HttpClient/IHttpClientFactory and ILogger they hold are not leaked across tests.
    private readonly List<ServiceProvider> _providers = new();

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

        // Plug the fake as the terminal primary handler for the named client so composite-key
        // writes flow: ODataAuthenticationHandler -> Polly (no-op) -> FakeHttpMessageHandler.
        services.AddHttpClient("ODataClient").ConfigurePrimaryHttpMessageHandler(() => fakeHandler);

        var provider = services.BuildServiceProvider();
        _providers.Add(provider);
        return (provider, fakeHandler);
    }

    private static ODataClientAdapter ResolveAdapter(ServiceProvider provider)
    {
        var client = provider.GetRequiredService<ODataClient>();
        var factory = provider.GetRequiredService<IHttpClientFactory>();
        return new ODataClientAdapter(client, factory);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        foreach (var provider in _providers)
        {
            provider.Dispose();
        }

        _providers.Clear();
    }

    [Fact]
    public async Task UpdateAsync_CompositeKey_EmitsPatchToKeyedUrlWithSerialisedBody()
    {
        // Arrange
        var (provider, handler) = BuildHarness();
        handler.Queue(HttpStatusCode.OK, "{\"dataAreaId\":\"1210\",\"JournalBatchNumber\":\"LNR0000266\",\"Description\":\"Updated\"}");
        var adapter = ResolveAdapter(provider);

        var key = new Dictionary<string, object>
        {
            ["dataAreaId"] = "1210",
            ["JournalBatchNumber"] = "LNR0000266"
        };
        var payload = new Dictionary<string, object> { ["Description"] = "Updated" };

        // Act
        var result = await adapter.UpdateAsync<TestJournalHeader>(EntitySet, key, payload, CancellationToken.None);

        // Assert
        handler.SentRequests.Should().HaveCount(1);
        handler.SentRequests[0].Method.Should().Be(HttpMethod.Patch);
        handler.SentRequests[0].RequestUri!.AbsoluteUri
            .Should().Be($"{BaseUrl}/{EntitySet}(dataAreaId='1210',JournalBatchNumber='LNR0000266')");

        var sentBody = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(handler.SentRequestBodies[0]!);
        sentBody!.Should().ContainKey("Description");
        sentBody["Description"].GetString().Should().Be("Updated");

        result.Description.Should().Be("Updated");
    }

    [Fact]
    public async Task DeleteAsync_CompositeKey_EmitsDeleteToKeyedUrlWithNoBody()
    {
        // Arrange
        var (provider, handler) = BuildHarness();
        handler.Queue(HttpStatusCode.NoContent);
        var adapter = ResolveAdapter(provider);

        var key = new Dictionary<string, object>
        {
            ["dataAreaId"] = "1210",
            ["JournalBatchNumber"] = "LNR0000266"
        };

        // Act
        await adapter.DeleteAsync(EntitySet, key, CancellationToken.None);

        // Assert
        handler.SentRequests.Should().HaveCount(1);
        handler.SentRequests[0].Method.Should().Be(HttpMethod.Delete);
        handler.SentRequests[0].RequestUri!.AbsoluteUri
            .Should().Be($"{BaseUrl}/{EntitySet}(dataAreaId='1210',JournalBatchNumber='LNR0000266')");
        handler.SentRequestBodies[0].Should().BeNull();
    }

    [Fact]
    public async Task BatchUpdate_CompositeKeys_EmitPerItemKeyedUrls()
    {
        // Arrange
        var (provider, handler) = BuildHarness();
        handler.Queue(HttpStatusCode.OK, "{}");
        handler.Queue(HttpStatusCode.OK, "{}");
        var adapter = ResolveAdapter(provider);

        var items = new List<(object Key, IDictionary<string, object> Payload)>
        {
            (new Dictionary<string, object> { ["dataAreaId"] = "1210", ["JournalBatchNumber"] = "LNR0000266" },
                new Dictionary<string, object> { ["Description"] = "A" }),
            (new Dictionary<string, object> { ["dataAreaId"] = "1210", ["JournalBatchNumber"] = "LNR0000267" },
                new Dictionary<string, object> { ["Description"] = "B" })
        };

        // Act
        var results = await adapter.BatchUpdateAsync(EntitySet, items, CancellationToken.None);

        // Assert
        handler.SentRequests.Should().HaveCount(2);
        handler.SentRequests.Should().AllSatisfy(r => r.Method.Should().Be(HttpMethod.Patch));
        handler.SentRequests[0].RequestUri!.AbsoluteUri
            .Should().EndWith("(dataAreaId='1210',JournalBatchNumber='LNR0000266')");
        handler.SentRequests[1].RequestUri!.AbsoluteUri
            .Should().EndWith("(dataAreaId='1210',JournalBatchNumber='LNR0000267')");
        results.Should().HaveCount(2);
        results.Should().AllSatisfy(r => r.IsSuccess.Should().BeTrue());
    }

    [Fact]
    public async Task BatchDelete_CompositeKeys_EmitPerItemKeyedUrls()
    {
        // Arrange
        var (provider, handler) = BuildHarness();
        handler.Queue(HttpStatusCode.NoContent);
        handler.Queue(HttpStatusCode.NoContent);
        var adapter = ResolveAdapter(provider);

        var keys = new List<object>
        {
            new Dictionary<string, object> { ["dataAreaId"] = "1210", ["JournalBatchNumber"] = "LNR0000266" },
            new Dictionary<string, object> { ["dataAreaId"] = "1210", ["JournalBatchNumber"] = "LNR0000267" }
        };

        // Act
        var results = await adapter.BatchDeleteAsync(EntitySet, keys, CancellationToken.None);

        // Assert
        handler.SentRequests.Should().HaveCount(2);
        handler.SentRequests.Should().AllSatisfy(r => r.Method.Should().Be(HttpMethod.Delete));
        handler.SentRequests[0].RequestUri!.AbsoluteUri
            .Should().EndWith("(dataAreaId='1210',JournalBatchNumber='LNR0000266')");
        handler.SentRequests[1].RequestUri!.AbsoluteUri
            .Should().EndWith("(dataAreaId='1210',JournalBatchNumber='LNR0000267')");
        results.Should().HaveCount(2);
        results.Should().AllSatisfy(r => r.IsSuccess.Should().BeTrue());
    }

    public static TheoryData<Dictionary<string, object>, string> KeyValueFormattingCases()
    {
        var guid = Guid.Parse("11111111-2222-3333-4444-555555555555");
        var date = new DateOnly(2026, 6, 30);
        return new TheoryData<Dictionary<string, object>, string>
        {
            // Guid keys are emitted unquoted, matching the filter/read literal formatter.
            { new Dictionary<string, object> { ["RecId"] = guid }, $"(RecId={guid})" },
            // DateOnly keys are emitted as yyyy-MM-dd.
            { new Dictionary<string, object> { ["TransDate"] = date }, "(TransDate=2026-06-30)" },
            // Enum keys are emitted as the D365 qualified-type literal.
            { new Dictionary<string, object> { ["Status"] = SampleStatus.Posted }, "(Status=Microsoft.Dynamics.DataEntities.SampleStatus'Posted')" }
        };
    }

    [Theory]
    [MemberData(nameof(KeyValueFormattingCases))]
    public async Task CompositeKeyWrite_FormatsGuidDateOnlyAndEnumKeyValues(
        Dictionary<string, object> key, string expectedKeySegment)
    {
        // Arrange
        var (provider, handler) = BuildHarness();
        handler.Queue(HttpStatusCode.NoContent);
        var adapter = ResolveAdapter(provider);

        // Act
        await adapter.DeleteAsync(EntitySet, key, CancellationToken.None);

        // Assert — the keyed URL segment must match IntegratoRODataExpressionTranslator.FormatValue.
        handler.SentRequests.Should().HaveCount(1);
        handler.SentRequests[0].RequestUri!.AbsoluteUri
            .Should().Be($"{BaseUrl}/{EntitySet}{expectedKeySegment}");
    }

    [Fact]
    public async Task CompositeKeyDelete_NotFoundResponse_TreatedAsSuccessByService()
    {
        // Arrange — drive end-to-end through ODataService.DeleteAsync (treatNotFoundAsSuccess=true)
        // so a 404 from the bypass is mapped via ODataExceptionHandler to a success Result.
        var (provider, handler) = BuildHarness();
        handler.Queue(HttpStatusCode.NotFound, "{\"error\":{\"message\":\"Not found\"}}");
        var adapter = ResolveAdapter(provider);

        var service = new ODataService<TestJournalHeader>(
            adapter, Substitute.For<ILogger<ODataService<TestJournalHeader>>>());

        var header = new TestJournalHeader
        {
            DataAreaId = "1210",
            JournalBatchNumber = "LNR0000266",
            JournalName = "GenJnl",
            Description = "To delete"
        };

        // Act
        var result = await service.DeleteAsync(header, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        handler.SentRequests[0].Method.Should().Be(HttpMethod.Delete);
    }

    [Fact]
    public async Task CompositeKeyUpdate_NonSuccessResponse_ThrowsODataClientExceptionWithStatusAndBody()
    {
        // Arrange
        var (provider, handler) = BuildHarness();
        const string errorBody = "{\"error\":{\"code\":\"BadRequest\",\"message\":\"Invalid field\"}}";
        handler.Queue(HttpStatusCode.BadRequest, errorBody);
        var adapter = ResolveAdapter(provider);

        var key = new Dictionary<string, object>
        {
            ["dataAreaId"] = "1210",
            ["JournalBatchNumber"] = "LNR0000266"
        };
        var payload = new Dictionary<string, object> { ["Description"] = "x" };

        // Act
        Func<Task> act = () => adapter.UpdateAsync<TestJournalHeader>(EntitySet, key, payload, CancellationToken.None);

        // Assert — the thrown exception carries status, body, and URL so the existing
        // ODataExceptionHandler 400 arm produces a Validation IntegrationError.
        var exception = (await act.Should().ThrowAsync<ODataClientException>()).Which;
        exception.StatusCode.Should().Be(400);
        exception.ResponseBody.Should().Be(errorBody);
        exception.RequestUrl.Should().EndWith("(dataAreaId='1210',JournalBatchNumber='LNR0000266')");
    }

    /// <summary>
    /// A standalone enum used solely to pin the qualified-type key-value formatting in
    /// <see cref="CompositeKeyWrite_FormatsGuidDateOnlyAndEnumKeyValues"/>.
    /// </summary>
    public enum SampleStatus
    {
        None = 0,
        Posted = 1
    }

    /// <summary>
    /// A local composite-key entity mirroring the D365 F&O <c>LedgerJournalHeader</c> wire shape
    /// (camelCase <c>dataAreaId</c> via <c>[JsonPropertyName]</c>, PascalCase
    /// <c>JournalBatchNumber</c>, both <c>[Key]</c>). Defined here so this test project does not
    /// take a dependency on <c>IntegratoR.OData.FO</c>.
    /// </summary>
    [Table("LedgerJournalHeaders")]
    public sealed class TestJournalHeader : BaseEntity<string>
    {
        [Key]
        [JsonPropertyName("dataAreaId")]
        public required string DataAreaId { get; set; }

        [Key]
        [JsonPropertyName("JournalBatchNumber")]
        public required string JournalBatchNumber { get; set; }

        [JsonPropertyName("JournalName")]
        public string? JournalName { get; set; }

        [JsonPropertyName("Description")]
        public string? Description { get; set; }

        public override object[] GetCompositeKey() => [DataAreaId, JournalBatchNumber];
    }
}
