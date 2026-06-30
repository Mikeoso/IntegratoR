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
/// <see cref="LedgerJournalSmokeTestResponse"/> with per-step outcomes. On the happy path
/// the ordered Update/Delete/verify steps perform the authoritative cleanup (lines then
/// header, with a re-read confirming the header is gone). If an early step fails the trigger
/// halts forward progress but still best-effort runs the cleanup steps so the sandbox isn't
/// left with orphan records. The Update/Delete steps depend on the composite-key WRITE
/// bypass in <c>ODataClientAdapter</c> (PR-B); without it they fail against D365.
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
        [HttpTrigger(AuthorizationLevel.Function, "post", Route = "smoke/ledger-journal")] HttpRequestData req,
        CancellationToken cancellationToken)
    {
        LedgerJournalSmokeTestRequest? input;
        try
        {
            input = await req.ReadFromJsonAsync<LedgerJournalSmokeTestRequest>(cancellationToken).ConfigureAwait(false);
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Smoke request body was not valid JSON.");
            return await WriteResponse(req, HttpStatusCode.BadRequest, new LedgerJournalSmokeTestResponse(
                Success: false,
                CreatedJournalBatchNumber: null,
                Steps: [new SmokeTestStep("ParseRequest", false, "SmokeTest.InvalidJson", ErrorType.Validation.ToString(), "Request body is not valid JSON.")]),
                cancellationToken).ConfigureAwait(false);
        }

        if (input is null || string.IsNullOrWhiteSpace(input.Company) || string.IsNullOrWhiteSpace(input.JournalName))
        {
            return await WriteResponse(req, HttpStatusCode.BadRequest, new LedgerJournalSmokeTestResponse(
                Success: false,
                CreatedJournalBatchNumber: null,
                Steps: [new SmokeTestStep("ParseRequest", false, "SmokeTest.MissingFields", ErrorType.Validation.ToString(), "Company and JournalName are required.")]),
                cancellationToken).ConfigureAwait(false);
        }

        var steps = new List<SmokeTestStep>();
        string? journalBatchNumber = null;

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
        //    Depends on PR-B's composite-key WRITE bypass: without it the UpdateCommand
        //    against this composite-key entity routes through PanoramicData's single-key
        //    path and fails (*.NotFound).
        // ---------------------------------------------------------------------------------
        string? expectedUpdatedDescription = null;
        if (getByKeyResult.IsSuccess)
        {
            var toUpdate = getByKeyResult.Value;
            toUpdate.Description = $"{toUpdate.Description}-UPDATED";

            var updateResult = await _mediator
                .Send(new UpdateCommand<LedgerJournalHeader>(toUpdate), cancellationToken)
                .ConfigureAwait(false);

            steps.Add(BuildStep("UpdateHeader", updateResult,
                onSuccess: r => $"Description={r.Description}"));

            // Only assert the persisted value when the update actually succeeded; otherwise
            // VerifyHeaderUpdated would re-read the original description and emit a misleading
            // failure that double-counts the UpdateHeader failure.
            if (updateResult.IsSuccess)
            {
                expectedUpdatedDescription = toUpdate.Description;
            }
        }

        // ---------------------------------------------------------------------------------
        // 6A. VerifyHeaderUpdated — re-read the header by its D365 composite key
        //     ([Company, journalBatchNumber]) and assert the Description change persisted.
        //     Only meaningful when UpdateHeader ran (expectedUpdatedDescription is set).
        // ---------------------------------------------------------------------------------
        if (expectedUpdatedDescription is not null)
        {
            var rereadHeaderResult = await _mediator
                .Send(new GetByKeyQuery<LedgerJournalHeader>([input.Company, journalBatchNumber!]), cancellationToken)
                .ConfigureAwait(false);

            if (rereadHeaderResult.IsSuccess && rereadHeaderResult.Value.Description == expectedUpdatedDescription)
            {
                steps.Add(new SmokeTestStep(
                    "VerifyHeaderUpdated",
                    Success: true,
                    Details: $"Description={rereadHeaderResult.Value.Description}"));
            }
            else if (rereadHeaderResult.IsSuccess)
            {
                steps.Add(new SmokeTestStep(
                    "VerifyHeaderUpdated",
                    Success: false,
                    Details: $"Expected Description='{expectedUpdatedDescription}' but read '{rereadHeaderResult.Value.Description}'."));
            }
            else
            {
                steps.Add(BuildStep("VerifyHeaderUpdated", rereadHeaderResult,
                    onSuccess: r => $"Description={r.Description}"));
            }
        }

        // ---------------------------------------------------------------------------------
        // 6B. UpdateLine + VerifyLineUpdated — re-fetch the lines, set TransactionText
        //     (wire `Text`; has IgnoreOnCreate but NOT IgnoreOnUpdate, so updatable),
        //     update, then re-read the line by its composite key and assert.
        // ---------------------------------------------------------------------------------
        var linesForUpdate = await _mediator
            .Send(new GetByFilterQuery<LedgerJournalLine>(
                x => x.DataAreaId == input.Company && x.JournalBatchNumber == journalBatchNumber),
                cancellationToken)
            .ConfigureAwait(false);

        if (linesForUpdate.IsSuccess && linesForUpdate.Value.Any())
        {
            var lineToUpdate = linesForUpdate.Value.First();
            lineToUpdate.TransactionText = "SMOKE-UPDATED";

            var updateLineResult = await _mediator
                .Send(new UpdateCommand<LedgerJournalLine>(lineToUpdate), cancellationToken)
                .ConfigureAwait(false);

            steps.Add(BuildStep("UpdateLine", updateLineResult,
                onSuccess: r => $"Text={r.TransactionText}"));

            // Only re-read + assert when the update succeeded; otherwise VerifyLineUpdated would
            // read the pre-update text and emit a misleading failure on top of UpdateLine.
            if (updateLineResult.IsSuccess)
            {
                var rereadLineResult = await _mediator
                    .Send(new GetByKeyQuery<LedgerJournalLine>(
                        [input.Company, journalBatchNumber!, lineToUpdate.LineNumber]),
                        cancellationToken)
                    .ConfigureAwait(false);

                if (rereadLineResult.IsSuccess && rereadLineResult.Value.TransactionText == "SMOKE-UPDATED")
                {
                    steps.Add(new SmokeTestStep(
                        "VerifyLineUpdated",
                        Success: true,
                        Details: $"Text={rereadLineResult.Value.TransactionText}"));
                }
                else if (rereadLineResult.IsSuccess)
                {
                    steps.Add(new SmokeTestStep(
                        "VerifyLineUpdated",
                        Success: false,
                        Details: $"Expected Text='SMOKE-UPDATED' but read '{rereadLineResult.Value.TransactionText}'."));
                }
                else
                {
                    steps.Add(BuildStep("VerifyLineUpdated", rereadLineResult,
                        onSuccess: r => $"Text={r.TransactionText}"));
                }
            }
        }
        else if (linesForUpdate.IsFailed)
        {
            steps.Add(BuildStep("UpdateLine.FilterLines", linesForUpdate,
                onSuccess: _ => "filtered"));
        }
        else
        {
            // Lines were created in step 4, so an empty result is a genuine failure.
            steps.Add(new SmokeTestStep(
                "UpdateLine",
                Success: false,
                Details: "No lines found to update (expected at least 1)."));
        }

        // ---------------------------------------------------------------------------------
        // 6C. Delete lines THEN header. D365 rejects deleting a header that still has child
        //     lines, so lines must go first. This is now the authoritative cleanup on the
        //     happy path (BestEffortCleanup is only retained on the early-return failure
        //     branches above).
        // ---------------------------------------------------------------------------------
        var linesForDelete = await _mediator
            .Send(new GetByFilterQuery<LedgerJournalLine>(
                x => x.DataAreaId == input.Company && x.JournalBatchNumber == journalBatchNumber),
                cancellationToken)
            .ConfigureAwait(false);

        if (linesForDelete.IsSuccess)
        {
            foreach (var line in linesForDelete.Value)
            {
                var deleteLineResult = await _mediator
                    .Send(new DeleteCommand<LedgerJournalLine>(line), cancellationToken)
                    .ConfigureAwait(false);

                steps.Add(BuildStep($"DeleteLine[{line.LineNumber}]", deleteLineResult,
                    onSuccess: _ => "deleted"));
            }
        }
        else
        {
            steps.Add(BuildStep("DeleteLines.FilterLines", linesForDelete,
                onSuccess: _ => "filtered"));
        }

        var headerToDelete = new LedgerJournalHeader
        {
            DataAreaId = input.Company,
            JournalBatchNumber = journalBatchNumber,
            JournalName = "placeholder", // required field, not used by DELETE
            Description = "placeholder"
        };

        var deleteHeaderResult = await _mediator
            .Send(new DeleteCommand<LedgerJournalHeader>(headerToDelete), cancellationToken)
            .ConfigureAwait(false);

        steps.Add(BuildStep("DeleteHeader", deleteHeaderResult,
            onSuccess: _ => "deleted"));

        // ---------------------------------------------------------------------------------
        // 6D. VerifyHeaderDeleted — re-read the header; a NotFound result confirms the
        //     delete succeeded, anything still present is a failure.
        // ---------------------------------------------------------------------------------
        var goneResult = await _mediator
            .Send(new GetByKeyQuery<LedgerJournalHeader>([input.Company, journalBatchNumber!]), cancellationToken)
            .ConfigureAwait(false);

        IntegrationError? goneError = goneResult.GetError();
        if (goneResult.IsFailed && goneError?.Type == ErrorType.NotFound)
        {
            steps.Add(new SmokeTestStep(
                "VerifyHeaderDeleted",
                Success: true,
                Details: "confirmed gone (NotFound)"));
        }
        else if (goneResult.IsSuccess)
        {
            steps.Add(new SmokeTestStep(
                "VerifyHeaderDeleted",
                Success: false,
                Details: "entity still exists after delete"));
        }
        else
        {
            // A non-NotFound failure (e.g. a transient error on the re-read) — route through
            // BuildStep so the full detail is logged server-side and the error code/type fall
            // back to sane values for non-IntegrationError failures.
            steps.Add(BuildStep("VerifyHeaderDeleted", goneResult));
        }

        var success = steps.All(s => s.Success);
        _logger.LogInformation(
            "LedgerJournal smoke test finished. Success={Success}, Steps={StepCount}",
            success, steps.Count);

        return await WriteResponse(req, HttpStatusCode.OK,
            new LedgerJournalSmokeTestResponse(success, journalBatchNumber, steps), cancellationToken)
            .ConfigureAwait(false);
    }

    private async Task BestEffortCleanup(
        List<SmokeTestStep> steps,
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
            steps.Add(BuildStep("Cleanup.FilterLines", linesResult,
                onSuccess: _ => "filtered"));
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

    private SmokeTestStep BuildStep<T>(
        string name,
        Result<T> result,
        Func<T, string>? onSuccess = null)
    {
        if (result.IsSuccess)
        {
            return new SmokeTestStep(
                name,
                Success: true,
                Details: onSuccess?.Invoke(result.Value));
        }

        IntegrationError? error = result.GetError();
        string code = error?.Code ?? "SmokeTest.Unknown";
        string type = error?.Type.ToString() ?? ErrorType.Failure.ToString();
        string serverMessage = error?.Message ?? result.Errors.FirstOrDefault()?.Message ?? "Unknown error";

        _logger.LogWarning(
            "Smoke step {Step} failed. Code={Code}, Type={Type}, Detail={Detail}",
            name, code, type, serverMessage);

        return new SmokeTestStep(
            name,
            Success: false,
            ErrorCode: code,
            ErrorType: type,
            ErrorMessage: "Operation failed; see host logs for details.");
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
