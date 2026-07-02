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

    /// <inheritdoc />
    public void Dispose()
    {
        foreach (ServiceProvider provider in _providers)
        {
            provider.Dispose();
        }
    }
}
