using System.Net;
using System.Text.Json;
using FluentResults;
using IntegratoR.Abstractions.Common.Batch;
using IntegratoR.Abstractions.Common.CQRS.Commands;
using IntegratoR.Abstractions.Common.CQRS.Queries;
using IntegratoR.Abstractions.Common.Results;
using IntegratoR.OData.FO.Domain.Entities.LedgerJournal;
using IntegratoR.OData.FO.Domain.Enums.LedgerJournals;
using IntegratoR.SampleFunction.Domain.DTOs.SmokeTest;
using MediatR;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;

namespace IntegratoR.SampleFunction.Endpoints;

/// <summary>
/// End-to-end smoke test for the configurable, chunked <c>$batch</c> write path (v3.0.0) against a
/// live D365 F&amp;O sandbox. Under one journal header it exercises, via MediatR batch commands:
/// (1) a chunked atomic batch-create split across several changesets; (2) an atomic-changeset
/// rollback (one good op + one bogus-key op in a single <see cref="BatchFailureMode.Atomic"/>
/// changeset — the good op must NOT persist); (3) a <see cref="BatchFailureMode.ContinueOnError"/>
/// partial accept (good op persists, bogus op collected in the failure list); (4) a batch delete.
/// </summary>
/// <remarks>
/// POST a <see cref="LedgerJournalBatchSmokeTestRequest"/> and read the per-step
/// <see cref="LedgerJournalBatchSmokeTestResponse"/>. Failures are deterministic: bogus operations
/// target a non-existent <c>LineNumber</c> (guaranteed 404), so the test does not depend on D365
/// account validation. The happy-path delete steps are the authoritative cleanup; early-return
/// branches best-effort delete lines then header so the sandbox is not left with orphans. Depends on
/// the composite-key WRITE bypass in <c>ODataClientAdapter</c> and the hand-rolled <c>$batch</c>
/// transport (ADR-0004).
/// </remarks>
public sealed class LedgerJournalBatchSmokeTestTrigger
{
    private readonly IMediator _mediator;
    private readonly ILogger<LedgerJournalBatchSmokeTestTrigger> _logger;

    public LedgerJournalBatchSmokeTestTrigger(IMediator mediator, ILogger<LedgerJournalBatchSmokeTestTrigger> logger)
    {
        _mediator = mediator;
        _logger = logger;
    }

    [Function("LedgerJournalBatchSmokeTest_HTTPTrigger")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Function, "post", Route = "smoke/ledger-journal-batch")] HttpRequestData req,
        CancellationToken cancellationToken)
    {
        LedgerJournalBatchSmokeTestRequest? input;
        try
        {
            input = await req.ReadFromJsonAsync<LedgerJournalBatchSmokeTestRequest>(cancellationToken).ConfigureAwait(false);
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Batch smoke request body was not valid JSON.");
            return await WriteResponse(req, HttpStatusCode.BadRequest,
                Fail("ParseRequest", "SmokeTest.InvalidJson", ErrorType.Validation, "Request body is not valid JSON."),
                cancellationToken).ConfigureAwait(false);
        }

        if (input is null || string.IsNullOrWhiteSpace(input.Company) || string.IsNullOrWhiteSpace(input.JournalName))
        {
            return await WriteResponse(req, HttpStatusCode.BadRequest,
                Fail("ParseRequest", "SmokeTest.MissingFields", ErrorType.Validation, "Company and JournalName are required."),
                cancellationToken).ConfigureAwait(false);
        }

        if (input.LineCount < 2 || input.ChunkSize < 1 || input.ChunkSize > 200)
        {
            return await WriteResponse(req, HttpStatusCode.BadRequest,
                Fail("ParseRequest", "SmokeTest.InvalidRange", ErrorType.Validation, "LineCount must be >= 2 and ChunkSize between 1 and 200."),
                cancellationToken).ConfigureAwait(false);
        }

        var steps = new List<SmokeTestStep>();
        _logger.LogInformation(
            "LedgerJournal BATCH smoke test starting for company {Company} journal {JournalName} (lines {LineCount}, chunk {ChunkSize}).",
            input.Company, input.JournalName, input.LineCount, input.ChunkSize);

        // -----------------------------------------------------------------------------------------
        // 1. Create the parent header (single command — batch lines need a header to hang off).
        // -----------------------------------------------------------------------------------------
        var header = new LedgerJournalHeader
        {
            DataAreaId = input.Company,
            JournalName = input.JournalName,
            Description = $"SMOKEBATCH-{DateTime.UtcNow:yyyyMMddHHmmss}"
        };

        var createHeaderResult = await _mediator
            .Send(new CreateCommand<LedgerJournalHeader>(header), cancellationToken)
            .ConfigureAwait(false);
        steps.Add(BuildStep("CreateHeader", createHeaderResult, onSuccess: r => $"JournalBatchNumber={r.JournalBatchNumber}"));
        if (createHeaderResult.IsFailed)
        {
            return await WriteResponse(req, HttpStatusCode.OK,
                new LedgerJournalBatchSmokeTestResponse(false, null, steps), cancellationToken).ConfigureAwait(false);
        }

        string journalBatchNumber = createHeaderResult.Value.JournalBatchNumber!;

        // -----------------------------------------------------------------------------------------
        // 2. Chunked atomic batch-create: N lines, small MaxOperationsPerChunk forces several
        //    changesets. Proves multi-chunk splitting, global index aggregation, and per-chunk
        //    atomic commit on the happy path.
        // -----------------------------------------------------------------------------------------
        var lines = new List<LedgerJournalLine>();
        for (int i = 0; i < input.LineCount; i++)
        {
            bool debit = i % 2 == 0;
            lines.Add(new LedgerJournalLine
            {
                DataAreaId = input.Company,
                JournalBatchNumber = journalBatchNumber,
                AccountDisplayValue = debit ? input.AccountDisplayValue : input.OffsetAccountDisplayValue,
                AccountType = LedgerJournalACType.Ledger,
                DebitAmount = debit ? input.Amount : 0m,
                CreditAmount = debit ? 0m : input.Amount,
                CurrencyCode = input.CurrencyCode,
                TransDate = new DateTimeOffset(DateTime.UtcNow.Date, TimeSpan.Zero)
            });
        }

        int expectedChunks = (int)Math.Ceiling((double)input.LineCount / input.ChunkSize);
        var createBatchResult = await _mediator
            .Send(new CreateBatchCommand<LedgerJournalLine>(lines,
                new BatchOptions { Mode = BatchFailureMode.Atomic, MaxOperationsPerChunk = input.ChunkSize }), cancellationToken)
            .ConfigureAwait(false);
        steps.Add(BuildBatchStep("BatchCreateLines.Atomic.Chunked", createBatchResult,
            expect: o => o.AllSucceeded
                && o.Total == input.LineCount
                && o.ChunkCount == expectedChunks
                && o.Items.Select(x => x.Index).SequenceEqual(Enumerable.Range(0, input.LineCount)),
            detail: o => $"Total={o.Total}, Chunks={o.ChunkCount} (expected {expectedChunks}), Succeeded={o.Succeeded}"));
        if (createBatchResult.IsFailed)
        {
            await BestEffortCleanup(steps, input.Company, journalBatchNumber, cancellationToken).ConfigureAwait(false);
            return await WriteResponse(req, HttpStatusCode.OK,
                new LedgerJournalBatchSmokeTestResponse(false, journalBatchNumber, steps), cancellationToken).ConfigureAwait(false);
        }

        // -----------------------------------------------------------------------------------------
        // 3. Verify N lines actually landed, and capture the authoritative server-assigned
        //    LineNumbers for the update/delete phases.
        // -----------------------------------------------------------------------------------------
        var afterCreate = await FilterLines(input.Company, journalBatchNumber, cancellationToken).ConfigureAwait(false);
        steps.Add(VerifyCount("VerifyLinesCreated", afterCreate, input.LineCount));

        List<LedgerJournalLine> realLines = afterCreate.IsSuccess
            ? [.. afterCreate.Value.OrderBy(l => l.LineNumber)]
            : [];
        if (realLines.Count < 2)
        {
            await BestEffortCleanup(steps, input.Company, journalBatchNumber, cancellationToken).ConfigureAwait(false);
            return await WriteResponse(req, HttpStatusCode.OK,
                new LedgerJournalBatchSmokeTestResponse(false, journalBatchNumber, steps), cancellationToken).ConfigureAwait(false);
        }

        // -----------------------------------------------------------------------------------------
        // 4. Atomic-changeset ROLLBACK proof: one good update + one bogus-key op in a single atomic
        //    changeset. D365 must roll the whole changeset back, so the good op must NOT persist.
        // -----------------------------------------------------------------------------------------
        LedgerJournalLine atomicTarget = realLines[0];
        string? atomicOriginalText = atomicTarget.TransactionText;
        atomicTarget.TransactionText = "SMOKE-ATOMIC-SHOULD-ROLLBACK";
        var atomicBatch = new List<LedgerJournalLine>
        {
            atomicTarget,
            BogusLine(input, journalBatchNumber, 99999999m, "SMOKE-ATOMIC-BOGUS")
        };
        var atomicResult = await _mediator
            .Send(new UpdateBatchCommand<LedgerJournalLine>(atomicBatch, new BatchOptions { Mode = BatchFailureMode.Atomic }), cancellationToken)
            .ConfigureAwait(false);
        steps.Add(ExpectBatchFailure("BatchUpdateAtomic.ExpectRollback", atomicResult));

        var atomicReread = await GetLine(input.Company, journalBatchNumber, atomicTarget.LineNumber, cancellationToken).ConfigureAwait(false);
        steps.Add(VerifyText("VerifyAtomicRolledBack", atomicReread, atomicOriginalText, "changeset rolled back: text unchanged"));

        // -----------------------------------------------------------------------------------------
        // 5. ContinueOnError PARTIAL accept: one good update + one bogus-key op. The good op must
        //    persist; the bogus op is collected as a failure in the outcome.
        // -----------------------------------------------------------------------------------------
        LedgerJournalLine continueTarget = realLines[1];
        continueTarget.TransactionText = "SMOKE-CONTINUE-OK";
        var continueBatch = new List<LedgerJournalLine>
        {
            continueTarget,
            BogusLine(input, journalBatchNumber, 99999998m, "SMOKE-CONTINUE-BOGUS")
        };
        var continueResult = await _mediator
            .Send(new UpdateBatchCommand<LedgerJournalLine>(continueBatch, new BatchOptions { Mode = BatchFailureMode.ContinueOnError }), cancellationToken)
            .ConfigureAwait(false);
        steps.Add(ExpectPartial("BatchUpdateContinueOnError.ExpectPartial", continueResult, expectedSucceeded: 1, expectedFailed: 1));

        var continueReread = await GetLine(input.Company, journalBatchNumber, continueTarget.LineNumber, cancellationToken).ConfigureAwait(false);
        steps.Add(VerifyText("VerifyContinueApplied", continueReread, "SMOKE-CONTINUE-OK", "good op applied"));

        // -----------------------------------------------------------------------------------------
        // 6. Batch-delete all lines (authoritative cleanup), verify none remain.
        // -----------------------------------------------------------------------------------------
        var linesForDelete = await FilterLines(input.Company, journalBatchNumber, cancellationToken).ConfigureAwait(false);
        if (linesForDelete.IsSuccess)
        {
            var deleteBatchResult = await _mediator
                .Send(new DeleteBatchCommand<LedgerJournalLine>([.. linesForDelete.Value],
                    new BatchOptions { Mode = BatchFailureMode.Atomic }), cancellationToken)
                .ConfigureAwait(false);
            steps.Add(BuildBatchStep("BatchDeleteLines.Atomic", deleteBatchResult,
                expect: o => o.AllSucceeded,
                detail: o => $"Deleted={o.Succeeded}/{o.Total}"));
        }
        else
        {
            steps.Add(BuildStep("BatchDeleteLines.FilterLines", linesForDelete, onSuccess: _ => "filtered"));
        }

        var afterDelete = await FilterLines(input.Company, journalBatchNumber, cancellationToken).ConfigureAwait(false);
        steps.Add(VerifyCount("VerifyLinesDeleted", afterDelete, 0));

        // -----------------------------------------------------------------------------------------
        // 7. Delete the header, verify gone.
        // -----------------------------------------------------------------------------------------
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
        steps.Add(BuildStep("DeleteHeader", deleteHeaderResult, onSuccess: _ => "deleted"));

        var goneResult = await _mediator
            .Send(new GetByKeyQuery<LedgerJournalHeader>([input.Company, journalBatchNumber]), cancellationToken)
            .ConfigureAwait(false);
        steps.Add(VerifyGone("VerifyHeaderDeleted", goneResult));

        bool success = steps.All(s => s.Success);
        _logger.LogInformation("LedgerJournal BATCH smoke test finished. Success={Success}, Steps={StepCount}", success, steps.Count);
        return await WriteResponse(req, HttpStatusCode.OK,
            new LedgerJournalBatchSmokeTestResponse(success, journalBatchNumber, steps), cancellationToken).ConfigureAwait(false);
    }

    private static LedgerJournalLine BogusLine(
        LedgerJournalBatchSmokeTestRequest input, string journalBatchNumber, decimal lineNumber, string text) =>
        new()
        {
            DataAreaId = input.Company,
            JournalBatchNumber = journalBatchNumber,
            LineNumber = lineNumber, // non-existent line — the PATCH/DELETE targets a key D365 doesn't have (404)
            AccountDisplayValue = input.AccountDisplayValue,
            AccountType = LedgerJournalACType.Ledger,
            DebitAmount = 0m,
            CreditAmount = 0m,
            CurrencyCode = input.CurrencyCode,
            TransDate = new DateTimeOffset(DateTime.UtcNow.Date, TimeSpan.Zero),
            TransactionText = text
        };

    private async Task<Result<IEnumerable<LedgerJournalLine>>> FilterLines(
        string company, string journalBatchNumber, CancellationToken cancellationToken) =>
        await _mediator
            .Send(new GetByFilterQuery<LedgerJournalLine>(
                x => x.DataAreaId == company && x.JournalBatchNumber == journalBatchNumber), cancellationToken)
            .ConfigureAwait(false);

    private async Task<Result<LedgerJournalLine>> GetLine(
        string company, string journalBatchNumber, decimal lineNumber, CancellationToken cancellationToken) =>
        await _mediator
            .Send(new GetByKeyQuery<LedgerJournalLine>([company, journalBatchNumber, lineNumber]), cancellationToken)
            .ConfigureAwait(false);

    private static BatchOutcome? GetOutcome(Result<BatchOutcome> result) =>
        result.IsSuccess ? result.Value : (result.GetError() as BatchIntegrationError)?.Outcome;

    private SmokeTestStep BuildBatchStep(
        string name, Result<BatchOutcome> result, Func<BatchOutcome, bool> expect, Func<BatchOutcome, string> detail)
    {
        BatchOutcome? outcome = GetOutcome(result);
        if (result.IsSuccess && outcome is not null && expect(outcome))
        {
            return new SmokeTestStep(name, true, Details: detail(outcome));
        }

        IntegrationError? error = result.GetError();
        _logger.LogWarning(
            "Batch smoke step {Step} did not meet expectation. Code={Code}, Type={Type}, Outcome={Outcome}",
            name, error?.Code ?? "(success)", error?.Type.ToString() ?? "(none)",
            outcome is null ? "none" : $"S={outcome.Succeeded}/F={outcome.Failed}/Chunks={outcome.ChunkCount}");
        return new SmokeTestStep(name, false,
            ErrorCode: error?.Code,
            ErrorType: error?.Type.ToString(),
            ErrorMessage: "Batch step did not meet expectation; see host logs for details.",
            Details: outcome is not null ? detail(outcome) : null);
    }

    private SmokeTestStep ExpectBatchFailure(string name, Result<BatchOutcome> result)
    {
        BatchOutcome? outcome = GetOutcome(result);
        if (result.IsFailed && result.GetError() is BatchIntegrationError)
        {
            return new SmokeTestStep(name, true,
                Details: outcome is null ? "batch failed (no outcome)" : $"failed as expected: Succeeded={outcome.Succeeded}, Failed={outcome.Failed}");
        }

        _logger.LogWarning("Batch smoke step {Step} expected an atomic rollback failure but the batch succeeded.", name);
        return new SmokeTestStep(name, false,
            ErrorMessage: "Expected the atomic batch to fail (rollback), but it reported success.",
            Details: outcome is null ? null : $"Succeeded={outcome.Succeeded}, Failed={outcome.Failed}");
    }

    private SmokeTestStep ExpectPartial(string name, Result<BatchOutcome> result, int expectedSucceeded, int expectedFailed)
    {
        BatchOutcome? outcome = GetOutcome(result);
        if (outcome is not null && outcome.Succeeded == expectedSucceeded && outcome.Failed == expectedFailed)
        {
            BatchItemResult? failure = outcome.Failures.FirstOrDefault();
            return new SmokeTestStep(name, true,
                Details: $"Succeeded={outcome.Succeeded}, Failed={outcome.Failed}, FailStatus={failure?.StatusCode}");
        }

        _logger.LogWarning(
            "Batch smoke step {Step} expected Succeeded={ExpSucceeded}/Failed={ExpFailed} but got {Outcome}.",
            name, expectedSucceeded, expectedFailed, outcome is null ? "no outcome" : $"S={outcome.Succeeded}/F={outcome.Failed}");
        return new SmokeTestStep(name, false,
            ErrorMessage: $"Expected Succeeded={expectedSucceeded}/Failed={expectedFailed}.",
            Details: outcome is null ? "no outcome" : $"Succeeded={outcome.Succeeded}, Failed={outcome.Failed}");
    }

    private SmokeTestStep VerifyCount(string name, Result<IEnumerable<LedgerJournalLine>> result, int expected)
    {
        if (result.IsFailed)
        {
            return BuildStep(name, result, onSuccess: _ => "filtered");
        }

        int count = result.Value.Count();
        return count == expected
            ? new SmokeTestStep(name, true, Details: $"Count={count}")
            : new SmokeTestStep(name, false, Details: $"Expected Count={expected} but found {count}.");
    }

    private SmokeTestStep VerifyText(string name, Result<LedgerJournalLine> reread, string? expectedText, string note)
    {
        if (reread.IsFailed)
        {
            return BuildStep(name, reread, onSuccess: r => $"Text={r.TransactionText}");
        }

        string? actual = reread.Value.TransactionText;
        return actual == expectedText
            ? new SmokeTestStep(name, true, Details: $"{note}: Text={actual ?? "(null)"}")
            : new SmokeTestStep(name, false, Details: $"{note}: expected Text='{expectedText ?? "(null)"}' but read '{actual ?? "(null)"}'.");
    }

    private SmokeTestStep VerifyGone(string name, Result<LedgerJournalHeader> goneResult)
    {
        IntegrationError? goneError = goneResult.GetError();
        if (goneResult.IsFailed && goneError?.Type == ErrorType.NotFound)
        {
            return new SmokeTestStep(name, true, Details: "confirmed gone (NotFound)");
        }

        if (goneResult.IsSuccess)
        {
            return new SmokeTestStep(name, false, Details: "entity still exists after delete");
        }

        return BuildStep(name, goneResult);
    }

    private async Task BestEffortCleanup(
        List<SmokeTestStep> steps, string company, string journalBatchNumber, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(journalBatchNumber))
        {
            return;
        }

        var linesResult = await FilterLines(company, journalBatchNumber, cancellationToken).ConfigureAwait(false);
        if (linesResult.IsSuccess)
        {
            foreach (var line in linesResult.Value)
            {
                var deleteLineResult = await _mediator
                    .Send(new DeleteCommand<LedgerJournalLine>(line), cancellationToken)
                    .ConfigureAwait(false);
                steps.Add(BuildStep($"Cleanup.DeleteLine[{line.LineNumber}]", deleteLineResult, onSuccess: _ => "deleted"));
            }
        }
        else
        {
            steps.Add(BuildStep("Cleanup.FilterLines", linesResult, onSuccess: _ => "filtered"));
        }

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
        steps.Add(BuildStep("Cleanup.DeleteHeader", deleteHeaderResult, onSuccess: _ => "deleted"));
    }

    private SmokeTestStep BuildStep<T>(string name, Result<T> result, Func<T, string>? onSuccess = null)
    {
        if (result.IsSuccess)
        {
            // result.Value can be null on a successful write whose server response carried no body
            // (e.g. an OData 204 No Content), so guard before invoking the onSuccess projector.
            return new SmokeTestStep(name, true, Details: result.Value is not null ? onSuccess?.Invoke(result.Value) : null);
        }

        IntegrationError? error = result.GetError();
        string code = error?.Code ?? "SmokeTest.Unknown";
        string type = error?.Type.ToString() ?? ErrorType.Failure.ToString();
        string serverMessage = error?.Message ?? result.Errors.FirstOrDefault()?.Message ?? "Unknown error";

        _logger.LogWarning("Smoke step {Step} failed. Code={Code}, Type={Type}, Detail={Detail}", name, code, type, serverMessage);
        return new SmokeTestStep(name, false, ErrorCode: code, ErrorType: type, ErrorMessage: "Operation failed; see host logs for details.");
    }

    private static LedgerJournalBatchSmokeTestResponse Fail(string step, string code, ErrorType type, string message) =>
        new(false, null, [new SmokeTestStep(step, false, code, type.ToString(), message)]);

    private static async Task<HttpResponseData> WriteResponse(
        HttpRequestData req, HttpStatusCode status, LedgerJournalBatchSmokeTestResponse body, CancellationToken cancellationToken)
    {
        var response = req.CreateResponse(status);
        await response.WriteAsJsonAsync(body, cancellationToken).ConfigureAwait(false);
        return response;
    }
}
