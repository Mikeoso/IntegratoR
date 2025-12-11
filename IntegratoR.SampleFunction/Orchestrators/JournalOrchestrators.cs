using IntegratoR.Abstractions.Common.Results;
using IntegratoR.OData.FO.Domain.Entities.LedgerJournal;
using IntegratoR.RELion.Domain.Models;
using IntegratoR.SampleFunction.Domain.DTOs.Activities;
using IntegratoR.SampleFunction.Domain.DTOs.Orchestrators;
using IntegratoR.SampleFunction.Domain.Entities;
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
        byte[] content;
        try
        {
            content = await context.CallActivityAsync<byte[]>(
                nameof(JournalActivities.ReadBlobActivity),
                blobName);

            logger.LogInformation("Read {SizeKB:N2} KB from blob {BlobName}",
                content.Length / 1024.0, blobName);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to read blob {BlobName}: {Error}", blobName, ex.Message);
            return;
        }

        // STEP 2: Parse the file content into structured lines
        var lines = await context.CallActivityAsync<List<RelionLedgerJournalLine>>(
            nameof(JournalActivities.ParseJournalFileActivity),
            content);

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
        var failedTasks = results.Where(r => r.IsFailure).ToList();

        if (failedTasks.Count != 0)
        {
            var aggregatedErrors = string.Join("; ", failedTasks.Select(r => r?.Error?.Message));
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
    /// </remarks>
    [Function(nameof(RunCompanyOrchestrator))]
    public static async Task RunCompanyOrchestrator([OrchestrationTrigger] TaskOrchestrationContext context)
    {
        var logger = context.CreateReplaySafeLogger(nameof(RunCompanyOrchestrator));
        var input = context.GetInput<CompanyOrchestratorInput>();

        if (input == null || string.IsNullOrEmpty(input.Company))
        {
            logger.LogError("Invalid input for company orchestrator: Input or Company is null.");
            throw new ArgumentException("Input or Company identifier is null or empty.");
        }

        logger.LogInformation("Sub-orchestration started for company {Company}.", input.Company);

        try
        {
            // STEP 1: Create journal header (throws on error)
            var header = await context.CallActivityAsync<LedgerJournalHeader>(
                nameof(JournalActivities.CreateJournalHeaderActivity),
                input.Company);

            var journalBatchNumber = header?.JournalBatchNumber;

            if (string.IsNullOrEmpty(journalBatchNumber))
            {
                logger.LogError("Created journal header is missing batch number for company {Company}.", input.Company);
                throw new InvalidOperationException("Created journal header is missing batch number.");
            }

            logger.LogInformation(
                "Created journal header with batch number {BatchNumber} for company {Company}.",
                journalBatchNumber, input.Company);

            if (input.Lines == null || input.Lines.Count == 0)
            {
                logger.LogInformation("No journal lines to process for company {Company}.", input.Company);
                return; // Success - no lines to process
            }

            // STEP 2: Map source lines to target format (throws on error)
            var mappingInput = new MapLinesActivityInput
            {
                JournalBatchNumber = journalBatchNumber,
                Lines = input.Lines
            };

            var mappedLines = await context.CallActivityAsync<List<LedgerJournalLine>>(
                nameof(JournalActivities.MapLinesActivity),
                mappingInput);

            if (mappedLines == null || mappedLines.Count == 0)
            {
                logger.LogWarning(
                    "No lines were successfully mapped for company {Company}. Skipping creation.",
                    input.Company);
                return;
            }

            logger.LogInformation(
                "Mapped {Count} lines for company {Company}. Creating in F&O...",
                mappedLines.Count, input.Company);

            // STEP 3: Create journal lines in F&O (throws on error)
            await context.CallActivityAsync(
                nameof(JournalActivities.CreateJournalLinesActivity),
                mappedLines);

            logger.LogInformation(
                "Successfully processed {Count} lines for company {Company}.",
                mappedLines.Count, input.Company);
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                "Failed to process company {Company}: {Error}",
                input.Company, ex.Message);

            // Re-throw so the parent orchestrator knows about the failure
            throw;
        }
    }

    /// <summary>
    /// An orchestrator triggered by a business event to fetch, persist, and queue journal data for processing.
    /// </summary>
    /// <param name="context">The context object provided by the Durable Functions framework.</param>
    /// <returns>A <see cref="Result"/> indicating the success or failure of the data ingestion.</returns>
    /// <remarks>
    /// This workflow acts as the ingestion point for the entire journal processing system. Its responsibilities are:
    /// 1.  Fetch new journal lines from the source system (Relion) based on a given date.
    /// 2.  If new lines are found, serialize them and write them to a blob in Azure Storage.
    /// 3.  The creation of this new blob will subsequently trigger the main `ProcessJournalFileOrchestrator` to begin processing.
    /// </remarks>
    [Function(nameof(ProcessJournalOrchestrator))]
    public static async Task ProcessJournalOrchestrator([OrchestrationTrigger] TaskOrchestrationContext context)
    {
        var logger = context.CreateReplaySafeLogger(nameof(ProcessJournalFileOrchestrator));

        var orchestrationInput = context.GetInput<BlobOrchestratorInput>();

        if (orchestrationInput is null || string.IsNullOrEmpty(orchestrationInput.BlobName))
        {
            logger.LogError("Invalid orchestration input: BlobName is null. Stopping orchestration.");
            return;
        }

        string blobName = orchestrationInput.BlobName;
        logger.LogInformation("Orchestration started for file {BlobName}.", blobName);

        // STEP 1: Read blob from storage (avoids Durable Functions size limits)
        byte[] content;
        try
        {
            content = await context.CallActivityAsync<byte[]>(
                nameof(JournalActivities.ReadBlobActivity),
                blobName);

            logger.LogInformation("Read {SizeKB:N2} KB from blob {BlobName}",
                content.Length / 1024.0, blobName);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to read blob {BlobName}: {Error}", blobName, ex.Message);
            return;
        }

        // STEP 2: Parse file content
        List<RelionLedgerJournalLine> lines;
        try
        {
            lines = await context.CallActivityAsync<List<RelionLedgerJournalLine>>(
                nameof(JournalActivities.ParseJournalFileActivity),
                content);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to parse blob {BlobName}: {Error}", blobName, ex.Message);
            return;
        }

        if (lines == null || lines.Count == 0)
        {
            logger.LogWarning("File {BlobName} is empty or failed to parse. Stopping orchestration.", blobName);
            return;
        }

        // STEP 3: Group by company
        var companyGroups = lines.GroupBy(l => l.RelCompetenceUnit).ToList();
        logger.LogInformation("Found {Count} companies to process in file {BlobName}.",
            companyGroups.Count, blobName);

        // STEP 4: Fan-Out - Start sub-orchestrator for each company
        var processingTasks = new List<Task>();

        foreach (var group in companyGroups)
        {
            var companyJournal = new CompanyOrchestratorInput
            {
                Company = group.Key,
                Lines = [.. group]
            };

            var subOrchestrationTask = context.CallSubOrchestratorAsync(
                nameof(RunCompanyOrchestrator),
                companyJournal);

            processingTasks.Add(subOrchestrationTask);
        }

        // STEP 5: Fan-In - Wait for all companies (handle exceptions)
        var results = await Task.WhenAll(processingTasks.Select(async task =>
        {
            try
            {
                await task;
                return (Success: true, Error: (string?)null);
            }
            catch (Exception ex)
            {
                return (Success: false, Error: ex.Message);
            }
        }));

        // STEP 6: Aggregation
        var failedCount = results.Count(r => !r.Success);

        if (failedCount > 0)
        {
            var errors = string.Join("; ", results.Where(r => !r.Success).Select(r => r.Error));
            logger.LogError(
                "Processing failed for {FailedCount} of {TotalCount} companies in file {BlobName}. Errors: {Errors}",
                failedCount,
                results.Length,
                blobName,
                errors);
        }
        else
        {
            logger.LogInformation(
                "All {Count} companies for file {BlobName} processed successfully.",
                results.Length, blobName);
        }
    }
}