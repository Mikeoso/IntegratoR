using FluentResults;
using IntegratoR.Abstractions.Common.Results;
using IntegratoR.OData.FO.Domain.Entities.LedgerJournal;
using IntegratoR.RELion.Domain.Models;
using IntegratoR.SampleFunction.Domain.DTOs.Activities;
using IntegratoR.SampleFunction.Domain.DTOs.Orchestrators;
using IntegratoR.SampleFunction.Functions;
using Microsoft.Azure.Functions.Worker;
using Microsoft.DurableTask;
using Microsoft.Extensions.Logging;

// FILE-LEVEL DOCUMENTATION
// ---------------------------------------------------------------------------------------------
// <remarks>
// This file contains the core business process workflows for journal processing, implemented
// as a set of Durable Functions orchestrators. These orchestrators define the reliable,
// long-running, and stateful sequences of operations, showcasing advanced patterns like
// fan-out/fan-in for parallel processing and sub-orchestrations for modularity.
//
// Updated to use Result pattern consistently: Activities now return Result{T} instead of throwing
// exceptions, enabling graceful error handling without try-catch blocks.
// </remarks>
// ---------------------------------------------------------------------------------------------
namespace IntegratoR.SampleFunction.Orchestrators;

/// <summary>
/// Provides a logical grouping for all Durable Functions orchestrators responsible for
/// the end-to-end processing of financial journal files. This class orchestrates a series
/// of activities and sub-orchestrations to ensure a resilient and scalable workflow.
/// </summary>
public static class JournalOrchestrators
{
    /// <summary>
    /// The main orchestrator for processing a journal file from blob storage. It coordinates
    /// the parsing of the file and fans out the processing to sub-orchestrators for each company.
    /// </summary>
    /// <param name="context">The context object provided by the Durable Functions framework, containing trigger input and methods for invoking activities.</param>
    /// <returns>A task that represents the completion of the orchestration.</returns>
    /// <remarks>
    /// This orchestrator follows a classic Fan-Out/Fan-In pattern:
    /// 1.  **Fan-Out:** After parsing the initial file, it groups all journal lines by company
    ///     and starts a new `RunCompanyOrchestrator` sub-orchestration for each company to run in parallel.
    /// 2.  **Fan-In:** It waits for all parallel sub-orchestrations to complete.
    /// 3.  **Aggregation:** It determines the final status (success or failure) and prepares for
    ///     the final archival or error handling step.
    ///
    /// Now uses Result pattern: All activity calls return Result{T}, eliminating the need for try-catch blocks.
    /// </remarks>
    [Function(nameof(ProcessJournalFileOrchestrator))]
    public static async Task ProcessJournalFileOrchestrator([OrchestrationTrigger] TaskOrchestrationContext context)
    {
        var logger = context.CreateReplaySafeLogger(nameof(ProcessJournalFileOrchestrator));

        // Get input from the blob trigger that started this orchestration.
        var orchestrationInput = context.GetInput<BlobOrchestratorInput>();

        if (orchestrationInput is null || string.IsNullOrEmpty(orchestrationInput.BlobName))
        {
            logger.LogError("Invalid orchestration input: BlobName is null. Stopping orchestration.");
            return;
        }

        string blobName = orchestrationInput.BlobName;

        logger.LogInformation("Orchestration started for file {BlobName}.", blobName);

        // STEP 1: Read the blob content from storage (avoids Durable Functions size limits)
        var contentResult = await context.CallActivityAsync<Result<byte[]>>(
            nameof(JournalActivities.ReadBlobActivity),
            blobName);

        if (contentResult.IsFailed)
        {
            logger.LogError("Failed to read blob {BlobName}: {Error}",
                blobName, contentResult.GetError()?.Message);
            return;
        }

        logger.LogInformation("Read {SizeKB:N2} KB from blob {BlobName}",
            contentResult.Value!.Length / 1024.0, blobName);

        // STEP 2: Parse the file content into structured lines
        var linesResult = await context.CallActivityAsync<Result<List<RelionLedgerJournalLine>>>(
            nameof(JournalActivities.ParseJournalFileActivity),
            contentResult.Value);

        if (linesResult.IsFailed)
        {
            logger.LogError("Failed to parse blob {BlobName}: {Error}",
                blobName, linesResult.GetError()?.Message);
            return;
        }

        var lines = linesResult.Value;

        if (lines == null || lines.Count == 0)
        {
            logger.LogWarning("File {BlobName} is empty or failed to parse. Stopping orchestration.", blobName);
            return;
        }

        // STEP 3: Group lines by company to enable parallel processing
        var companyGroups = lines.GroupBy(l => l.RelCompetenceUnit).ToList();
        logger.LogInformation("Found {Count} companies to process in file {BlobName}.",
            companyGroups.Count, blobName);

        var processingTasks = new List<Task<Result>>();

        // STEP 4: Fan-Out - Start a sub-orchestrator for each company
        foreach (var group in companyGroups)
        {
            var companyJournal = new CompanyOrchestratorInput
            {
                Company = group.Key,
                Lines = [.. group]
            };

            var subOrchestrationTask = context.CallSubOrchestratorAsync<Result>(
                nameof(RunCompanyOrchestrator),
                companyJournal);
            processingTasks.Add(subOrchestrationTask);
        }

        // STEP 5: Fan-In - Wait for all parallel company processing tasks
        await Task.WhenAll(processingTasks);

        // STEP 6: Aggregation - Collect results and determine overall status
        var results = processingTasks.Select(t => t.Result).ToList();
        var failedTasks = results.Where(r => r.IsFailed).ToList();

        if (failedTasks.Count != 0)
        {
            var aggregatedErrors = string.Join("; ", failedTasks.Select(r => r?.GetError()?.Message));
            logger.LogError(
                "Processing failed for {FailedCount} of {TotalCount} companies in file {BlobName}. Errors: {Errors}",
                failedTasks.Count,
                results.Count,
                blobName,
                aggregatedErrors);
        }
        else
        {
            logger.LogInformation("All {Count} companies for file {BlobName} processed successfully.",
                results.Count, blobName);
        }
    }

    /// <summary>
    /// A sub-orchestrator that handles the processing workflow for a single company's journal data.
    /// </summary>
    /// <param name="context">The context object provided by the Durable Functions framework.</param>
    /// <returns>A <see cref="Result"/> indicating the success or failure of the operation for this company.</returns>
    /// <remarks>
    /// This workflow ensures that operations for a single company occur in the correct sequence:
    /// 1.  Create a journal header in the target system.
    /// 2.  Map the source journal lines to the target format using the new header's batch number.
    /// 3.  Create the mapped journal lines in the target system as a batch.
    ///
    /// Now uses Result pattern: All activity calls return Result{T}, enabling graceful error handling
    /// without exceptions. The orchestrator can make informed decisions based on success/failure.
    /// </remarks>
    [Function(nameof(RunCompanyOrchestrator))]
    public static async Task<Result> RunCompanyOrchestrator([OrchestrationTrigger] TaskOrchestrationContext context)
    {
        var logger = context.CreateReplaySafeLogger(nameof(RunCompanyOrchestrator));
        var input = context.GetInput<CompanyOrchestratorInput>();

        if (input == null || string.IsNullOrEmpty(input.Company))
        {
            var error = new IntegrationError(
                "CompanyOrchestrator.InvalidInput",
                "Input or Company identifier is null or empty.",
                ErrorType.Validation);

            logger.LogError("Invalid input for company orchestrator: {Error}", error.Message);
            return Result.Fail(error);
        }

        logger.LogInformation("Sub-orchestration started for company {Company}.", input.Company);

        // STEP 1: Create journal header
        var headerResult = await context.CallActivityAsync<Result<LedgerJournalHeader>>(
            nameof(JournalActivities.CreateJournalHeaderActivity),
            input.Company);

        if (headerResult.IsFailed)
        {
            logger.LogError("Failed to create journal header for company {Company}: {Error}",
                input.Company, headerResult.GetError()?.Message);
            return Result.Fail(headerResult.Errors);
        }

        var journalBatchNumber = headerResult.Value?.JournalBatchNumber;

        if (string.IsNullOrEmpty(journalBatchNumber))
        {
            var error = new IntegrationError(
                "CompanyOrchestrator.MissingBatchNumber",
                "Created journal header is missing batch number.",
                ErrorType.Failure);

            logger.LogError("Created journal header is missing batch number for company {Company}.", input.Company);
            return Result.Fail(error);
        }

        logger.LogInformation(
            "Created journal header with batch number {BatchNumber} for company {Company}.",
            journalBatchNumber, input.Company);

        if (input.Lines == null || input.Lines.Count == 0)
        {
            logger.LogInformation("No journal lines to process for company {Company}.", input.Company);
            return Result.Ok(); // Success - no lines to process
        }

        // STEP 2: Map source lines to target format
        var mappingInput = new MapLinesActivityInput
        {
            JournalBatchNumber = journalBatchNumber,
            Lines = input.Lines
        };

        var mappedLinesResult = await context.CallActivityAsync<Result<List<LedgerJournalLine>>>(
            nameof(JournalActivities.MapLinesActivity),
            mappingInput);

        if (mappedLinesResult.IsFailed)
        {
            logger.LogError(
                "Failed to map lines for company {Company}: {Error}",
                input.Company, mappedLinesResult.GetError()?.Message);
            return Result.Fail(mappedLinesResult.Errors);
        }

        var mappedLines = mappedLinesResult.Value;

        if (mappedLines == null || mappedLines.Count == 0)
        {
            logger.LogWarning(
                "No lines were successfully mapped for company {Company}. Skipping creation.",
                input.Company);
            return Result.Ok(); // Partial success - some lines may have been skipped
        }

        logger.LogInformation(
            "Mapped {Count} lines for company {Company}. Creating in F&O...",
            mappedLines.Count, input.Company);

        // STEP 3: Create journal lines in F&O
        var createResult = await context.CallActivityAsync<Result>(
            nameof(JournalActivities.CreateJournalLinesActivity),
            mappedLines);

        if (createResult.IsFailed)
        {
            logger.LogError(
                "Failed to create journal lines for company {Company}: {Error}",
                input.Company, createResult.GetError()?.Message);
            return createResult;
        }

        logger.LogInformation(
            "Successfully processed {Count} lines for company {Company}.",
            mappedLines.Count, input.Company);

        return Result.Ok();
    }

    /// <summary>
    /// An orchestrator triggered by a business event to fetch, persist, and queue journal data for processing.
    /// </summary>
    /// <param name="context">The context object provided by the Durable Functions framework.</param>
    /// <returns>A task representing the completion of the orchestration.</returns>
    /// <remarks>
    /// This workflow acts as the ingestion point for the entire journal processing system. Its responsibilities are:
    /// 1.  Fetch new journal lines from the source system (Relion) based on a given date.
    /// 2.  If new lines are found, serialize them and write them to a blob in Azure Storage.
    /// 3.  The creation of this new blob will subsequently trigger the main `ProcessJournalFileOrchestrator` to begin processing.
    ///
    /// Now uses Result pattern for all activity calls, enabling graceful error handling.
    /// </remarks>
    [Function(nameof(ProcessJournalOrchestrator))]
    public static async Task ProcessJournalOrchestrator([OrchestrationTrigger] TaskOrchestrationContext context)
    {
        var logger = context.CreateReplaySafeLogger(nameof(ProcessJournalOrchestrator));

        var orchestrationInput = context.GetInput<HTTPOrchestratorInput>();

        if (orchestrationInput is null)
        {
            logger.LogError("Invalid orchestration input: Input is null. Stopping orchestration.");
            return;
        }

        logger.LogInformation("Orchestration started for import date {ImportDate}.", orchestrationInput.ImportDate);

        // STEP 1: Fetch lines from Relion
        var linesResult = await context.CallActivityAsync<Result<List<RelionLedgerJournalLine>>>(
            nameof(JournalActivities.GetRelionJournalLinesActivity),
            orchestrationInput.ImportDate);

        if (linesResult.IsFailed)
        {
            logger.LogError("Failed to fetch lines from Relion: {Error}", linesResult.GetError()?.Message);
            return;
        }

        var lines = linesResult.Value;

        if (lines == null || lines.Count == 0)
        {
            logger.LogInformation("No new journal lines found in Relion since {ImportDate}.",
                orchestrationInput.ImportDate);
            return;
        }

        logger.LogInformation("Fetched {Count} journal lines from Relion.", lines.Count);

        // STEP 2: Write lines to blob storage
        var blobName = $"relion-import-{orchestrationInput.EventId:N}.json";

        var writeBlobInput = new WriteJournalLinesActivityInput
        {
            BlobName = blobName,
            Lines = lines
        };

        var writeBlobResult = await context.CallActivityAsync<Result<WriteJournalLinesActivityResult>>(
            nameof(JournalActivities.WriteJournalLinesToBlobActivity),
            writeBlobInput);

        if (writeBlobResult.IsFailed)
        {
            logger.LogError("Failed to write blob {BlobName}: {Error}",
                blobName, writeBlobResult.GetError()?.Message);
            return;
        }

        logger.LogInformation(
            "Successfully wrote {Count} lines to blob {BlobName}. Blob trigger will start main processing.",
            lines.Count, blobName);
    }
}
