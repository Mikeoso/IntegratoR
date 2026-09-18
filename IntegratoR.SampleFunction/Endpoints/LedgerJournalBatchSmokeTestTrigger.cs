using System.Net;
using System.Text.Json;
using FluentResults;
using IntegratoR.Abstractions.Common.CQRS.Commands;
using IntegratoR.Abstractions.Common.CQRS.Queries;
using IntegratoR.Abstractions.Common.Results;
using IntegratoR.OData.FO.Domain.Entities.LedgerJournal;
using IntegratoR.OData.FO.Features.Commands.LedgerJournals.CreateLedgerJournalHeader;
using IntegratoR.OData.Interfaces.Services;
using IntegratoR.SampleFunction.Domain.DTOs.SmokeTest;
using MediatR;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;

namespace IntegratoR.SampleFunction.Endpoints;

/// <summary>
/// Live smoke test for the OData <c>$batch</c> write path: creates several
/// <c>LedgerJournalHeader</c> rows in one batch, confirms on the server that they exist, then
/// deletes them in one batch and confirms they are gone.
/// </summary>
/// <remarks>
/// This is the release gate for the 1.3.7 hotfix. Batch writes previously threw
/// <c>InvalidOperationException</c> ("This operation is not supported for a relative URI") while
/// serialising the request, so nothing reached D365 at all. Passing this trigger against a real
/// sandbox proves the hand-rolled multipart body is one D365 actually accepts — the unit tests pin
/// the wire format, but only a live run proves the server agrees with it.
/// <para>
/// Verification deliberately reads back from the server rather than trusting the batch response: a
/// create that silently applied nothing would otherwise look identical to a successful one.
/// </para>
/// </remarks>
public sealed class LedgerJournalBatchSmokeTestTrigger
{
    private readonly IMediator _mediator;
    private readonly IODataBatchService<LedgerJournalHeader> _batchService;
    private readonly ILogger<LedgerJournalBatchSmokeTestTrigger> _logger;

    /// <param name="batchService">
    /// Injected directly, against the usual "endpoints go through IMediator" rule: this line has no
    /// batch-delete command, and the single-entity <c>DeleteCommand</c> cannot delete a composite-key
    /// row here — PanoramicData cannot address one, D365 answers 404, and
    /// <c>treatNotFoundAsSuccess</c> reports that as success. Cleanup through the command would
    /// therefore leave orphans behind while claiming to have removed them.
    /// </param>
    public LedgerJournalBatchSmokeTestTrigger(
        IMediator mediator,
        IODataBatchService<LedgerJournalHeader> batchService,
        ILogger<LedgerJournalBatchSmokeTestTrigger> logger)
    {
        _mediator = mediator;
        _batchService = batchService;
        _logger = logger;
    }

    [Function("LedgerJournalBatchSmokeTest_HTTPTrigger")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "smoke/ledger-journal-batch")] HttpRequestData req,
        CancellationToken cancellationToken)
    {
        var steps = new List<SmokeTestStep>();

        LedgerJournalBatchSmokeTestRequest? input;
        try
        {
            input = await req.ReadFromJsonAsync<LedgerJournalBatchSmokeTestRequest>(cancellationToken)
                .ConfigureAwait(false);
        }
        catch (JsonException ex)
        {
            steps.Add(new SmokeTestStep("ParseRequest", false, "SmokeTest.InvalidJson",
                ErrorType.Validation.ToString(), ex.Message));
            return await WriteResponse(req, HttpStatusCode.BadRequest,
                new LedgerJournalBatchSmokeTestResponse(false, null, steps), cancellationToken).ConfigureAwait(false);
        }

        // The lower bound is load-bearing, not cosmetic: two or more rows is what makes this a batch,
        // and it is also what keeps the "found.Count > 0" guard below from ever skipping the delete
        // step on a run that still reports success. The upper bound stops an anonymous endpoint
        // aiming a huge changeset at a customer sandbox — D365 caps one at roughly 200 operations,
        // and this line has no chunking.
        if (input is null
            || string.IsNullOrWhiteSpace(input.Company)
            || string.IsNullOrWhiteSpace(input.JournalName)
            || input.HeaderCount is < 2 or > 20)
        {
            steps.Add(new SmokeTestStep("ParseRequest", false, "SmokeTest.MissingFields",
                ErrorType.Validation.ToString(),
                "Company and JournalName are required, and HeaderCount must be between 2 and 20."));
            return await WriteResponse(req, HttpStatusCode.BadRequest,
                new LedgerJournalBatchSmokeTestResponse(false, null, steps), cancellationToken).ConfigureAwait(false);
        }

        // "BATCH-" + 14-char timestamp + "-" is already 21 characters, so the cut has to leave enough
        // GUID for two runs in the same second not to share descriptions and delete each other's rows.
        string runId = $"BATCH-{DateTime.UtcNow:yyyyMMddHHmmss}-{Guid.NewGuid():N}"[..30];
        string[] descriptions = Enumerable
            .Range(1, input.HeaderCount)
            .Select(i => $"{runId}-{i}")
            .ToArray();

        _logger.LogInformation(
            "LedgerJournal batch smoke test starting: {Count} headers in company {Company}, run {RunId}.",
            input.HeaderCount, input.Company, runId);

        // -----------------------------------------------------------------------------------
        // 1. Create every header in one $batch. Before the fix this threw during serialisation.
        // -----------------------------------------------------------------------------------
        List<LedgerJournalHeader> headers = descriptions
            .Select(description => new LedgerJournalHeader
            {
                DataAreaId = input.Company,
                JournalName = input.JournalName,
                Description = description
            })
            .ToList();

        // CreateLedgerJournalHeadersCommand derives from CreateBatchCommand and its handler calls
        // IODataBatchService.AddBatchAsync — the exact path the reported production stack trace took.
        // The generic batch handlers do not exist on this line (they arrived in 2.0.0), so this is
        // also the only batch command that resolves here.
        Result createResult = await _mediator
            .Send(new CreateLedgerJournalHeadersCommand<LedgerJournalHeader>(headers), cancellationToken)
            .ConfigureAwait(false);

        steps.Add(BuildStep("CreateBatch", createResult, $"{headers.Count} headers submitted"));

        // Deliberately no early return on a failed create. "Create failed" does not prove "nothing
        // was written": SendAtomicBatchAsync also reports every operation failed when the response
        // cannot be parsed, and D365 may have committed the changeset regardless. Falling through
        // turns an unknown state into a verified one and still cleans up. The run cannot go green
        // either way — steps.All already sees the failed CreateBatch step.

        // -----------------------------------------------------------------------------------
        // 2. Read back from the server. A batch that applied nothing would otherwise look the
        //    same as one that worked, which is exactly the failure mode being guarded against.
        // -----------------------------------------------------------------------------------
        (List<LedgerJournalHeader> found, SmokeTestStep? createLookupFailure) =
            await FindByDescriptions("VerifyCreated", input.Company, descriptions, cancellationToken)
                .ConfigureAwait(false);

        bool allCreated = createLookupFailure is null && found.Count == descriptions.Length;

        steps.Add(createLookupFailure ?? (allCreated
            ? new SmokeTestStep("VerifyCreated", true,
                Details: $"found {found.Count}/{descriptions.Length}")
            : new SmokeTestStep("VerifyCreated", false, "SmokeTest.CountMismatch",
                ErrorType.Failure.ToString(),
                $"Expected {descriptions.Length} header(s), found {found.Count}.")));

        // -----------------------------------------------------------------------------------
        // 3. Delete whatever was created in one $batch — the batch delete path, and the cleanup.
        // -----------------------------------------------------------------------------------
        // Batch delete, not one at a time: it is the only path here that can address a composite key
        // at all. Exercising it also closes the gap the unit tests alone would leave.
        if (found.Count > 0)
        {
            Result deleteResult = await _batchService
                .DeleteBatchAsync(found, cancellationToken)
                .ConfigureAwait(false);

            steps.Add(BuildStep("DeleteBatch", deleteResult, $"{found.Count} headers deleted"));
        }

        // -----------------------------------------------------------------------------------
        // 4. Confirm the sandbox is clean, so a green run never leaves orphans behind.
        // -----------------------------------------------------------------------------------
        (List<LedgerJournalHeader> remaining, SmokeTestStep? deleteLookupFailure) =
            await FindByDescriptions("VerifyDeleted", input.Company, descriptions, cancellationToken)
                .ConfigureAwait(false);

        // An empty result only means "clean" when the lookup actually finished. FindByDescriptions
        // returns early on a failed query, so treating a bare count of zero as proof would turn the
        // release gate green on a run that never verified anything.
        bool verifiedClean = deleteLookupFailure is null && remaining.Count == 0;

        if (deleteLookupFailure is not null)
        {
            steps.Add(deleteLookupFailure);
        }
        else if (verifiedClean)
        {
            steps.Add(new SmokeTestStep("VerifyDeleted", true, Details: "no rows remain"));
        }
        else
        {
            // The composite keys themselves, not just the run marker: an operator can delete these
            // directly instead of hunting by Description. Logged as well, so the list survives a
            // response that never reaches the caller.
            string orphanKeys = string.Join(", ", remaining.Select(header =>
                $"(dataAreaId='{header.DataAreaId}',JournalBatchNumber='{header.JournalBatchNumber}')"));

            _logger.LogWarning(
                "Batch smoke test {RunId} left {OrphanCount} header(s) behind in {Company}: {OrphanKeys}",
                runId, remaining.Count, input.Company, orphanKeys);

            steps.Add(new SmokeTestStep("VerifyDeleted", false, "SmokeTest.OrphansRemain",
                ErrorType.Failure.ToString(),
                $"{remaining.Count} header(s) still present: {orphanKeys}"));
        }

        bool success = allCreated && verifiedClean && steps.All(step => step.Success);

        return await WriteResponse(req, HttpStatusCode.OK,
            new LedgerJournalBatchSmokeTestResponse(success, runId, steps), cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Looks each description up individually. Batch creates return no server-generated keys, so the
    /// unique per-run Description is the only handle on the rows that were just written.
    /// </summary>
    /// <returns>
    /// The rows found, and a step describing a **query** failure — <c>null</c> when every lookup
    /// completed. The caller may judge the row count only against a <c>null</c> failure: after an
    /// early return the count is partial and proves nothing either way.
    /// </returns>
    private async Task<(List<LedgerJournalHeader> Found, SmokeTestStep? QueryFailure)> FindByDescriptions(
        string stepName,
        string company,
        IReadOnlyList<string> descriptions,
        CancellationToken cancellationToken)
    {
        var found = new List<LedgerJournalHeader>();

        foreach (string description in descriptions)
        {
            Result<IEnumerable<LedgerJournalHeader>> result = await _mediator
                .Send(new GetByFilterQuery<LedgerJournalHeader>(
                        header => header.DataAreaId == company && header.Description == description),
                    cancellationToken)
                .ConfigureAwait(false);

            if (result.IsFailed)
            {
                IntegrationError? error = result.GetError();
                return (found, new SmokeTestStep(
                    stepName, false, error?.Code, error?.Type.ToString(), error?.Message));
            }

            found.AddRange(result.Value);
        }

        return (found, null);
    }

    private static SmokeTestStep BuildStep(string name, Result result, string successDetails)
    {
        if (result.IsSuccess)
        {
            return new SmokeTestStep(name, Success: true, Details: successDetails);
        }

        IntegrationError? error = result.GetError();
        return new SmokeTestStep(
            name,
            Success: false,
            ErrorCode: error?.Code,
            ErrorType: error?.Type.ToString(),
            ErrorMessage: error?.Message);
    }

    private static async Task<HttpResponseData> WriteResponse(
        HttpRequestData req,
        HttpStatusCode status,
        LedgerJournalBatchSmokeTestResponse body,
        CancellationToken cancellationToken)
    {
        var response = req.CreateResponse(status);
        await response.WriteAsJsonAsync(body, cancellationToken).ConfigureAwait(false);
        return response;
    }
}
