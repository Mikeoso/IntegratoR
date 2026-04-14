using System.Net;
using System.Text.Json;
using FluentResults;
using IntegratoR.Abstractions.Common.CQRS.Commands;
using IntegratoR.Abstractions.Common.CQRS.Queries;
using IntegratoR.Abstractions.Common.Results;
using IntegratoR.OData.FO.Domain.Entities.LedgerJournal;
using IntegratoR.OData.FO.Domain.Enums.General;
using IntegratoR.OData.FO.Domain.Enums.LedgerJournals;
using IntegratoR.SampleFunction.Domain.DTOs.SmokeTest;
using MediatR;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;

namespace IntegratoR.SampleFunction.Endpoints;

/// <summary>
/// Full end-to-end smoke test for <c>LedgerJournalHeader</c> and <c>LedgerJournalLine</c>
/// CRUD paths. Exercises the create / get-by-key / filter / update / count / delete flow
/// through MediatR against a live D365 F&amp;O sandbox so consumers can confirm the
/// <c>[JsonPropertyName]</c>-aware filter translator (PR #86 / v1.3.3) and the
/// <c>PropertyNameResolver</c>-based payload builder (PR #86 / v1.3.3) work end-to-end.
/// </summary>
/// <remarks>
/// POST a <see cref="LedgerJournalSmokeTestRequest"/> body. The trigger returns a
/// <see cref="LedgerJournalSmokeTestResponse"/> with per-step outcomes. If any step fails
/// the trigger halts forward progress but still best-effort runs the cleanup steps so the
/// sandbox isn't left with orphan records.
/// </remarks>
public sealed class LedgerJournalSmokeTestTrigger
{
    private readonly IMediator _mediator;
    private readonly ILogger<LedgerJournalSmokeTestTrigger> _logger;

    public LedgerJournalSmokeTestTrigger(IMediator mediator, ILogger<LedgerJournalSmokeTestTrigger> logger)
    {
        _mediator = mediator;
        _logger = logger;
    }

    [Function("LedgerJournalSmokeTest_HTTPTrigger")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "smoke/ledger-journal")] HttpRequestData req,
        CancellationToken cancellationToken)
    {
        LedgerJournalSmokeTestRequest? input;
        try
        {
            input = await req.ReadFromJsonAsync<LedgerJournalSmokeTestRequest>(cancellationToken).ConfigureAwait(false);
        }
        catch (JsonException ex)
        {
            return await WriteResponse(req, HttpStatusCode.BadRequest, new LedgerJournalSmokeTestResponse(
                Success: false,
                CreatedJournalBatchNumber: null,
                Steps: [new LedgerJournalSmokeTestStep("ParseRequest", false, "SmokeTest.InvalidJson", ErrorType.Validation.ToString(), ex.Message)]),
                cancellationToken).ConfigureAwait(false);
        }

        if (input is null || string.IsNullOrWhiteSpace(input.Company) || string.IsNullOrWhiteSpace(input.JournalName))
        {
            return await WriteResponse(req, HttpStatusCode.BadRequest, new LedgerJournalSmokeTestResponse(
                Success: false,
                CreatedJournalBatchNumber: null,
                Steps: [new LedgerJournalSmokeTestStep("ParseRequest", false, "SmokeTest.MissingFields", ErrorType.Validation.ToString(), "Company and JournalName are required.")]),
                cancellationToken).ConfigureAwait(false);
        }

        var steps = new List<LedgerJournalSmokeTestStep>();
        string? journalBatchNumber = null;
        decimal? debitLineNumber = null;
        decimal? creditLineNumber = null;

        _logger.LogInformation(
            "LedgerJournal smoke test starting for company {Company} with journal {JournalName}.",
            input.Company, input.JournalName);

        // ---------------------------------------------------------------------------------
        // 1. Create header
        // ---------------------------------------------------------------------------------
        var header = new LedgerJournalHeader
        {
            DataAreaId = input.Company,
            JournalName = input.JournalName,
            Description = $"SMOKETEST-{DateTime.UtcNow:yyyyMMddHHmmss}"
        };

        var createHeaderResult = await _mediator
            .Send(new CreateCommand<LedgerJournalHeader>(header), cancellationToken)
            .ConfigureAwait(false);

        steps.Add(BuildStep("CreateHeader", createHeaderResult,
            onSuccess: r => $"JournalBatchNumber={r.JournalBatchNumber}"));

        if (createHeaderResult.IsFailed)
        {
            return await WriteResponse(req, HttpStatusCode.OK,
                new LedgerJournalSmokeTestResponse(false, null, steps), cancellationToken).ConfigureAwait(false);
        }

        journalBatchNumber = createHeaderResult.Value.JournalBatchNumber;

        // ---------------------------------------------------------------------------------
        // 2. Get header by composite key — exercises ODataService.BuildCompositeKeyObject
        //    which uses PropertyNameResolver to serialise `dataAreaId` (camelCase) as part
        //    of the key payload.
        // ---------------------------------------------------------------------------------
        var getByKeyResult = await _mediator
            .Send(new GetByKeyQuery<LedgerJournalHeader>([input.Company, journalBatchNumber!]), cancellationToken)
            .ConfigureAwait(false);

        steps.Add(BuildStep("GetHeaderByKey", getByKeyResult,
            onSuccess: r => $"IsPosted={r.IsPosted}, Description={r.Description}"));

        if (getByKeyResult.IsFailed)
        {
            await BestEffortCleanup(steps, input.Company, journalBatchNumber, cancellationToken).ConfigureAwait(false);
            return await WriteResponse(req, HttpStatusCode.OK,
                new LedgerJournalSmokeTestResponse(false, journalBatchNumber, steps), cancellationToken).ConfigureAwait(false);
        }

        // ---------------------------------------------------------------------------------
        // 3. Filter by DataAreaId + JournalBatchNumber — THE regression test for v1.3.3.
        //    This must emit `$filter=JournalBatchNumber eq '...' and dataAreaId eq '...'`
        //    with camelCase `dataAreaId`. Pre-fix this returned empty results and the
        //    whole smoke test would stop here.
        // ---------------------------------------------------------------------------------
        var filterHeaderResult = await _mediator
            .Send(new GetByFilterQuery<LedgerJournalHeader>(
                x => x.DataAreaId == input.Company && x.JournalBatchNumber == journalBatchNumber),
                cancellationToken)
            .ConfigureAwait(false);

        steps.Add(BuildStep("FilterHeaderByDataAreaId", filterHeaderResult,
            onSuccess: r => $"MatchedCount={r.Count()}"));

        if (filterHeaderResult.IsFailed || !filterHeaderResult.Value.Any())
        {
            steps[^1] = steps[^1] with { Success = false, Details = steps[^1].Details + " (expected at least 1 match)" };
            await BestEffortCleanup(steps, input.Company, journalBatchNumber, cancellationToken).ConfigureAwait(false);
            return await WriteResponse(req, HttpStatusCode.OK,
                new LedgerJournalSmokeTestResponse(false, journalBatchNumber, steps), cancellationToken).ConfigureAwait(false);
        }

        // ---------------------------------------------------------------------------------
        // 4. Create a balanced pair of journal lines (debit + credit).
        // ---------------------------------------------------------------------------------
        var debitLine = new LedgerJournalLine
        {
            DataAreaId = input.Company,
            JournalBatchNumber = journalBatchNumber!,
            AccountDisplayValue = input.AccountDisplayValue,
            AccountType = LedgerJournalACType.Ledger,
            DebitAmount = input.Amount,
            CreditAmount = 0m,
            CurrencyCode = input.CurrencyCode,
            TransDate = new DateTimeOffset(DateTime.UtcNow.Date, TimeSpan.Zero)
        };

        var createDebitResult = await _mediator
            .Send(new CreateCommand<LedgerJournalLine>(debitLine), cancellationToken)
            .ConfigureAwait(false);

        steps.Add(BuildStep("CreateDebitLine", createDebitResult,
            onSuccess: r => $"LineNumber={r.LineNumber}"));

        if (createDebitResult.IsSuccess)
        {
            debitLineNumber = createDebitResult.Value.LineNumber;
        }

        if (createDebitResult.IsSuccess)
        {
            var creditLine = new LedgerJournalLine
            {
                DataAreaId = input.Company,
                JournalBatchNumber = journalBatchNumber!,
                AccountDisplayValue = input.OffsetAccountDisplayValue,
                AccountType = LedgerJournalACType.Ledger,
                DebitAmount = 0m,
                CreditAmount = input.Amount,
                CurrencyCode = input.CurrencyCode,
                TransDate = new DateTimeOffset(DateTime.UtcNow.Date, TimeSpan.Zero)
            };

            var createCreditResult = await _mediator
                .Send(new CreateCommand<LedgerJournalLine>(creditLine), cancellationToken)
                .ConfigureAwait(false);

            steps.Add(BuildStep("CreateCreditLine", createCreditResult,
                onSuccess: r => $"LineNumber={r.LineNumber}"));

            if (createCreditResult.IsSuccess)
            {
                creditLineNumber = createCreditResult.Value.LineNumber;
            }
        }

        // ---------------------------------------------------------------------------------
        // 5. Filter lines by DataAreaId + JournalBatchNumber — second camelCase filter
        //    regression test, this time for LedgerJournalLine.
        // ---------------------------------------------------------------------------------
        var filterLinesResult = await _mediator
            .Send(new GetByFilterQuery<LedgerJournalLine>(
                x => x.DataAreaId == input.Company && x.JournalBatchNumber == journalBatchNumber),
                cancellationToken)
            .ConfigureAwait(false);

        steps.Add(BuildStep("FilterLinesByDataAreaId", filterLinesResult,
            onSuccess: r => $"MatchedCount={r.Count()}"));

        // ---------------------------------------------------------------------------------
        // 6. Update header description — exercises the update-payload builder through
        //    CreatePayload which uses PropertyNameResolver for wire-name resolution.
        // ---------------------------------------------------------------------------------
        if (getByKeyResult.IsSuccess)
        {
            var toUpdate = getByKeyResult.Value;
            toUpdate.Description = $"{toUpdate.Description}-UPDATED";

            var updateResult = await _mediator
                .Send(new UpdateCommand<LedgerJournalHeader>(toUpdate), cancellationToken)
                .ConfigureAwait(false);

            steps.Add(BuildStep("UpdateHeader", updateResult,
                onSuccess: r => $"Description={r.Description}"));
        }

        // ---------------------------------------------------------------------------------
        // Cleanup — delete lines and header best-effort.
        // ---------------------------------------------------------------------------------
        await BestEffortCleanup(steps, input.Company, journalBatchNumber, cancellationToken).ConfigureAwait(false);

        var success = steps.All(s => s.Success);
        _logger.LogInformation(
            "LedgerJournal smoke test finished. Success={Success}, Steps={StepCount}",
            success, steps.Count);

        return await WriteResponse(req, HttpStatusCode.OK,
            new LedgerJournalSmokeTestResponse(success, journalBatchNumber, steps), cancellationToken)
            .ConfigureAwait(false);
    }

    private async Task BestEffortCleanup(
        List<LedgerJournalSmokeTestStep> steps,
        string company,
        string? journalBatchNumber,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(journalBatchNumber))
        {
            return;
        }

        // Re-fetch lines so the delete uses the authoritative server-side composite keys.
        var linesResult = await _mediator
            .Send(new GetByFilterQuery<LedgerJournalLine>(
                x => x.DataAreaId == company && x.JournalBatchNumber == journalBatchNumber),
                cancellationToken)
            .ConfigureAwait(false);

        if (linesResult.IsSuccess)
        {
            foreach (var line in linesResult.Value)
            {
                var deleteLineResult = await _mediator
                    .Send(new DeleteCommand<LedgerJournalLine>(line), cancellationToken)
                    .ConfigureAwait(false);

                steps.Add(BuildStep($"Cleanup.DeleteLine[{line.LineNumber}]", deleteLineResult,
                    onSuccess: _ => "deleted"));
            }
        }
        else
        {
            steps.Add(new LedgerJournalSmokeTestStep(
                "Cleanup.FilterLines",
                false,
                linesResult.GetError()?.Code,
                linesResult.GetError()?.Type.ToString(),
                linesResult.GetError()?.Message));
        }

        // Delete the header last so the lines exist under it until cleaned up.
        var headerToDelete = new LedgerJournalHeader
        {
            DataAreaId = company,
            JournalBatchNumber = journalBatchNumber,
            JournalName = "placeholder", // required field, not used by DELETE
            Description = "placeholder"
        };

        var deleteHeaderResult = await _mediator
            .Send(new DeleteCommand<LedgerJournalHeader>(headerToDelete), cancellationToken)
            .ConfigureAwait(false);

        steps.Add(BuildStep("Cleanup.DeleteHeader", deleteHeaderResult,
            onSuccess: _ => "deleted"));
    }

    private static LedgerJournalSmokeTestStep BuildStep<T>(
        string name,
        Result<T> result,
        Func<T, string>? onSuccess = null)
    {
        if (result.IsSuccess)
        {
            return new LedgerJournalSmokeTestStep(
                name,
                Success: true,
                Details: onSuccess?.Invoke(result.Value));
        }

        var error = result.GetError();
        return new LedgerJournalSmokeTestStep(
            name,
            Success: false,
            ErrorCode: error?.Code,
            ErrorType: error?.Type.ToString(),
            ErrorMessage: error?.Message);
    }

    private static async Task<HttpResponseData> WriteResponse(
        HttpRequestData req,
        HttpStatusCode status,
        LedgerJournalSmokeTestResponse body,
        CancellationToken cancellationToken)
    {
        var response = req.CreateResponse(status);
        await response.WriteAsJsonAsync(body, cancellationToken).ConfigureAwait(false);
        return response;
    }
}
