using System.Net;
using System.Text.Json;
using FluentResults;
using IntegratoR.Abstractions.Common.CQRS.Commands;
using IntegratoR.Abstractions.Common.CQRS.Queries;
using IntegratoR.Abstractions.Common.Results;
using IntegratoR.OData.FO.Domain.Entities.LedgerJournal;
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
    private readonly ILogger<LedgerJournalBatchSmokeTestTrigger> _logger;

    public LedgerJournalBatchSmokeTestTrigger(
        IMediator mediator,
        ILogger<LedgerJournalBatchSmokeTestTrigger> logger)
    {
        _mediator = mediator;
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

        if (input is null
            || string.IsNullOrWhiteSpace(input.Company)
            || string.IsNullOrWhiteSpace(input.JournalName)
            || input.HeaderCount < 2)
        {
            steps.Add(new SmokeTestStep("ParseRequest", false, "SmokeTest.MissingFields",
                ErrorType.Validation.ToString(),
                "Company and JournalName are required, and HeaderCount must be at least 2."));
            return await WriteResponse(req, HttpStatusCode.BadRequest,
                new LedgerJournalBatchSmokeTestResponse(false, null, steps), cancellationToken).ConfigureAwait(false);
        }

        string runId = $"BATCH-{DateTime.UtcNow:yyyyMMddHHmmss}-{Guid.NewGuid():N}"[..24];
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

        Result createResult = await _mediator
            .Send(new CreateBatchCommand<LedgerJournalHeader>(headers), cancellationToken)
            .ConfigureAwait(false);

        steps.Add(BuildStep("CreateBatch", createResult, $"{headers.Count} headers submitted"));

        if (createResult.IsFailed)
        {
            return await WriteResponse(req, HttpStatusCode.OK,
                new LedgerJournalBatchSmokeTestResponse(false, runId, steps), cancellationToken).ConfigureAwait(false);
        }

        // -----------------------------------------------------------------------------------
        // 2. Read back from the server. A batch that applied nothing would otherwise look the
        //    same as one that worked, which is exactly the failure mode being guarded against.
        // -----------------------------------------------------------------------------------
        (List<LedgerJournalHeader> found, SmokeTestStep verifyStep) =
            await FindByDescriptions("VerifyCreated", input.Company, descriptions, cancellationToken)
                .ConfigureAwait(false);
        steps.Add(verifyStep);

        bool allCreated = found.Count == descriptions.Length;

        // -----------------------------------------------------------------------------------
        // 3. Delete whatever was created in one $batch — the batch delete path, and the cleanup.
        // -----------------------------------------------------------------------------------
        if (found.Count > 0)
        {
            Result deleteResult = await _mediator
                .Send(new DeleteBatchCommand<LedgerJournalHeader>(found), cancellationToken)
                .ConfigureAwait(false);

            steps.Add(BuildStep("DeleteBatch", deleteResult, $"{found.Count} headers deleted"));
        }

        // -----------------------------------------------------------------------------------
        // 4. Confirm the sandbox is clean, so a green run never leaves orphans behind.
        // -----------------------------------------------------------------------------------
        (List<LedgerJournalHeader> remaining, SmokeTestStep cleanupStep) =
            await FindByDescriptions("VerifyDeleted", input.Company, descriptions, cancellationToken)
                .ConfigureAwait(false);

        steps.Add(remaining.Count == 0
            ? new SmokeTestStep("VerifyDeleted", true, Details: "no rows remain")
            : new SmokeTestStep("VerifyDeleted", false, "SmokeTest.OrphansRemain", ErrorType.Failure.ToString(),
                $"{remaining.Count} header(s) still present — search Description for '{runId}'."));

        bool success = allCreated
            && remaining.Count == 0
            && steps.All(step => step.Success);

        return await WriteResponse(req, HttpStatusCode.OK,
            new LedgerJournalBatchSmokeTestResponse(success, runId, steps), cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Looks each description up individually. Batch creates return no server-generated keys, so the
    /// unique per-run Description is the only handle on the rows that were just written.
    /// </summary>
    private async Task<(List<LedgerJournalHeader> Found, SmokeTestStep Step)> FindByDescriptions(
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

        bool complete = found.Count == descriptions.Count;
        return (found, new SmokeTestStep(
            stepName,
            complete,
            complete ? null : "SmokeTest.CountMismatch",
            complete ? null : ErrorType.Failure.ToString(),
            complete ? null : $"Expected {descriptions.Count} header(s), found {found.Count}.",
            $"found {found.Count}/{descriptions.Count}"));
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
