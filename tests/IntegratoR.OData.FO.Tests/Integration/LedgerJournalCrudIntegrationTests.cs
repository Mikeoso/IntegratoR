using FluentAssertions;
using FluentResults;
using IntegratoR.Abstractions.Common.CQRS.Commands;
using IntegratoR.Abstractions.Common.CQRS.Queries;
using IntegratoR.Abstractions.Common.Results;
using IntegratoR.OData.FO.Domain.Entities.LedgerJournal;
using IntegratoR.OData.FO.Tests.Integration.Fakes;
using IntegratoR.TestKit.Assertions;
using MediatR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace IntegratoR.OData.FO.Tests.Integration;

/// <summary>
/// An automated end-to-end integration test that drives the full create -&gt; read -&gt; update -&gt;
/// delete pipeline for a D365 F&amp;O <see cref="LedgerJournalHeader"/> (and its
/// <see cref="LedgerJournalLine"/> children) through <see cref="IMediator"/> against an in-process
/// stateful fake OData endpoint (<see cref="StatefulD365Handler"/>). It exercises the
/// composite-key WRITE bypass (PATCH/DELETE to keyed URLs) and proves each read observes the prior
/// write — the automated equivalent of a live JFI smoke run.
/// </summary>
public sealed class LedgerJournalCrudIntegrationTests : IDisposable
{
    private const string DataAreaId = "1210";

    private readonly StatefulD365Handler _fake;
    private readonly ServiceProvider _provider;
    private readonly IMediator _mediator;

    public LedgerJournalCrudIntegrationTests()
    {
        _fake = new StatefulD365Handler();

        IConfiguration config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ODataSettings:Url"] = "https://fake.local/data",
                ["ODataSettings:Authentication:Mode"] = "ApiKey",
                ["ODataSettings:Authentication:ApiManagement:SubscriptionKey"] = "test-key",
                ["ODataSettings:Authentication:ApiManagement:SubscriptionHeaderKey"] = "Ocp-Apim-Subscription-Key",
                ["ODataSettings:Resilience:EnableRetries"] = "false",
                ["ODataSettings:Resilience:UseCircuitBreaker"] = "false",
            })
            .Build();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddIntegratoR(config);

        // Swap the named client's primary handler for the stateful fake AFTER AddIntegratoR so
        // requests flow: ODataAuthenticationHandler -> Polly (no-op) -> fake.
        services.AddHttpClient("ODataClient").ConfigurePrimaryHttpMessageHandler(() => _fake);

        _provider = services.BuildServiceProvider();
        _mediator = _provider.GetRequiredService<IMediator>();
    }

    /// <inheritdoc />
    public void Dispose() => _provider.Dispose();

    [Fact]
    public async Task FullCrudCycle_DrivesCreateReadUpdateDelete_ThroughCompositeKeyBypass()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        // 1. CREATE the header — JournalBatchNumber is server-assigned (IgnoreOnCreate).
        var header = new LedgerJournalHeader
        {
            DataAreaId = DataAreaId,
            JournalName = "GenJrn",
            Description = "SMOKETEST",
        };

        Result<LedgerJournalHeader> createHeaderResult =
            await _mediator.Send(new CreateCommand<LedgerJournalHeader>(header), cancellationToken);

        createHeaderResult.Should().BeSuccessful();
        createHeaderResult.Value.JournalBatchNumber.Should().NotBeNullOrEmpty();
        string jbn = createHeaderResult.Value.JournalBatchNumber!;

        // 2. READ the header back by composite key (routes through a $filter, not a keyed GET).
        Result<LedgerJournalHeader> getHeaderResult =
            await _mediator.Send(new GetByKeyQuery<LedgerJournalHeader>([DataAreaId, jbn]), cancellationToken);

        getHeaderResult.Should().BeSuccessful();
        getHeaderResult.Value.Description.Should().Be("SMOKETEST");

        // 3. READ by filter — the camelCase dataAreaId filter must emit dataAreaId eq '1210'.
        Result<IEnumerable<LedgerJournalHeader>> filterHeaderResult = await _mediator.Send(
            new GetByFilterQuery<LedgerJournalHeader>(
                x => x.DataAreaId == DataAreaId && x.JournalBatchNumber == jbn),
            cancellationToken);

        filterHeaderResult.Should().BeSuccessful();
        filterHeaderResult.Value.Should().ContainSingle();

        // 4. CREATE two lines (debit then credit). LineNumber is server-assigned (IgnoreOnCreate).
        //    AccountDisplayValue/TransDate are required by the compiler contract even though
        //    IgnoreOnCreate strips them from the create payload.
        var debitLine = new LedgerJournalLine
        {
            DataAreaId = DataAreaId,
            JournalBatchNumber = jbn,
            AccountDisplayValue = "110110",
            DebitAmount = 100m,
            CreditAmount = 0m,
            CurrencyCode = "EUR",
            TransDate = DateTimeOffset.UtcNow,
        };

        var creditLine = new LedgerJournalLine
        {
            DataAreaId = DataAreaId,
            JournalBatchNumber = jbn,
            AccountDisplayValue = "110120",
            DebitAmount = 0m,
            CreditAmount = 100m,
            CurrencyCode = "EUR",
            TransDate = DateTimeOffset.UtcNow,
        };

        Result<LedgerJournalLine> createDebitResult =
            await _mediator.Send(new CreateCommand<LedgerJournalLine>(debitLine), cancellationToken);
        Result<LedgerJournalLine> createCreditResult =
            await _mediator.Send(new CreateCommand<LedgerJournalLine>(creditLine), cancellationToken);

        createDebitResult.Should().BeSuccessful();
        createCreditResult.Should().BeSuccessful();
        createDebitResult.Value.LineNumber.Should().Be(1m);
        createCreditResult.Value.LineNumber.Should().Be(2m);
        decimal lineNumber = createDebitResult.Value.LineNumber;

        // 5. READ the lines by filter — both lines belong to the batch.
        Result<IEnumerable<LedgerJournalLine>> filterLinesResult = await _mediator.Send(
            new GetByFilterQuery<LedgerJournalLine>(
                x => x.DataAreaId == DataAreaId && x.JournalBatchNumber == jbn),
            cancellationToken);

        filterLinesResult.Should().BeSuccessful();
        filterLinesResult.Value.Should().HaveCount(2);

        // 6. UPDATE the header (composite-key bypass PATCH) and re-read to prove persistence.
        header.JournalBatchNumber = jbn;
        header.Description = "SMOKETEST-UPDATED";

        Result<LedgerJournalHeader> updateHeaderResult =
            await _mediator.Send(new UpdateCommand<LedgerJournalHeader>(header), cancellationToken);

        updateHeaderResult.Should().BeSuccessful();

        Result<LedgerJournalHeader> rereadHeaderResult =
            await _mediator.Send(new GetByKeyQuery<LedgerJournalHeader>([DataAreaId, jbn]), cancellationToken);

        rereadHeaderResult.Should().BeSuccessful();
        rereadHeaderResult.Value.Description.Should().Be("SMOKETEST-UPDATED");

        // 7. UPDATE a line (composite-key bypass PATCH) and re-read by key to prove persistence.
        debitLine.LineNumber = lineNumber;
        debitLine.TransactionText = "SMOKE-UPDATED";

        Result<LedgerJournalLine> updateLineResult =
            await _mediator.Send(new UpdateCommand<LedgerJournalLine>(debitLine), cancellationToken);

        updateLineResult.Should().BeSuccessful();

        Result<LedgerJournalLine> rereadLineResult =
            await _mediator.Send(new GetByKeyQuery<LedgerJournalLine>([DataAreaId, jbn, lineNumber]), cancellationToken);

        rereadLineResult.Should().BeSuccessful();
        rereadLineResult.Value.TransactionText.Should().Be("SMOKE-UPDATED");

        // 8. DELETE each line, then the header (composite-key bypass DELETE).
        Result<LedgerJournalLine> deleteDebitResult =
            await _mediator.Send(new DeleteCommand<LedgerJournalLine>(debitLine), cancellationToken);
        Result<LedgerJournalLine> deleteCreditResult =
            await _mediator.Send(new DeleteCommand<LedgerJournalLine>(new LedgerJournalLine
            {
                DataAreaId = DataAreaId,
                JournalBatchNumber = jbn,
                LineNumber = createCreditResult.Value.LineNumber,
                AccountDisplayValue = "110120",
                CurrencyCode = "EUR",
                TransDate = DateTimeOffset.UtcNow,
            }), cancellationToken);

        deleteDebitResult.Should().BeSuccessful();
        deleteCreditResult.Should().BeSuccessful();

        Result<LedgerJournalHeader> deleteHeaderResult =
            await _mediator.Send(new DeleteCommand<LedgerJournalHeader>(header), cancellationToken);

        deleteHeaderResult.Should().BeSuccessful();

        // 9. READ the header again — it must now be gone (NotFound after the delete).
        Result<LedgerJournalHeader> goneResult =
            await _mediator.Send(new GetByKeyQuery<LedgerJournalHeader>([DataAreaId, jbn]), cancellationToken);

        goneResult.Should().BeFailed();
        goneResult.Should().HaveErrorType(ErrorType.NotFound);

        // The bypass PATCH and DELETE must have hit the keyed composite-key URLs.
        string headerKeySegment = $"(dataAreaId='{DataAreaId}',JournalBatchNumber='{jbn}')";
        _fake.Requests.Should().Contain(r =>
            r.Method == HttpMethod.Patch && r.Url.Contains(headerKeySegment));
        _fake.Requests.Should().Contain(r =>
            r.Method == HttpMethod.Delete && r.Url.Contains(headerKeySegment));
        _fake.Requests.Should().Contain(r =>
            r.Method == HttpMethod.Patch
            && r.Url.Contains($"dataAreaId='{DataAreaId}'")
            && r.Url.Contains($"JournalBatchNumber='{jbn}'")
            && r.Url.Contains("LineNumber="));
    }
}
