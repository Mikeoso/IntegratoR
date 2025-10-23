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

        if (orchestrationInput is null || string.IsNullOrEmpty(orchestrationInput.BlobName) || orchestrationInput.Content is null)
        {
            logger.LogError("Invalid orchestration input: Input, BlobName, or Content is null. Stopping orchestration.");
            return;
        }

        string blobName = orchestrationInput.BlobName;
        byte[] content = orchestrationInput.Content;

        logger.LogInformation("Orchestration started for file {BlobName}.", blobName);

        // 1. Parse the entire file into a structured list of lines.
        var lines = await context.CallActivityAsync<List<RelionLedgerJournalLine>>(
            nameof(JournalActivities.ParseJournalFileActivity),
            content);

        if (lines == null || lines.Count == 0)
        {
            logger.LogWarning("File {BlobName} is empty or failed to parse. Stopping orchestration.", blobName);
            // TODO DDI:Move invalid file to error folder activity could be called here
            return;
        }

        // 2. Group lines by company to enable parallel processing.
        var companyGroups = lines.GroupBy(l => l.RelCompetenceUnit).ToList();
        logger.LogInformation("Found {Count} companies to process in file {BlobName}.", companyGroups.Count, blobName);

        var processingTasks = new List<Task<Result>>();

        // 3. Fan-Out: Start a sub-orchestrator for each company.
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

        // 4. Fan-In: Wait for all parallel company processing tasks to complete.
        await Task.WhenAll(processingTasks);

        // 5. Aggregation: Collect all results and check for failures.
        var results = processingTasks.Select(t => t.Result).ToList();
        var failedTasks = results.Where(r => r.IsFailure).ToList();

        if (failedTasks.Count != 0)
        {
            // Aggregate error messages for better logging and diagnostics
            var aggregatedErrors = string.Join("; ", failedTasks.Select(r => r?.Error?.Message));
            logger.LogError(
                "Processing failed for {FailedCount} of {TotalCount} companies in file {BlobName}. Errors: {Errors}",
                failedTasks.Count,
                results.Count,
                blobName,
                aggregatedErrors);

            // TODO DDI: Hier könnte man eine komplexere Logik implementieren:
            // - Eine Activity aufrufen, die eine Fehlerdatei mit den fehlgeschlagenen Zeilen erstellt.
            // - Eine Activity aufrufen, die nur die erfolgreichen Teile des Prozesses archiviert.
            // - Den ganzen Blob in den Fehlerordner verschieben.
        }
        else
        {
            logger.LogInformation("All {Count} companies for file {BlobName} processed successfully.", results.Count, blobName);
            //TODO DDI: Call archive file activity here
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
    public static async Task<Result> RunCompanyOrchestrator([OrchestrationTrigger] TaskOrchestrationContext context)
    {
        var logger = context.CreateReplaySafeLogger(nameof(RunCompanyOrchestrator));
        var input = context.GetInput<CompanyOrchestratorInput>();

        if (input == null || string.IsNullOrEmpty(input.Company))
        {
            var error = new Error(
                "CompanyOrchestrator.InvalidInput",
                "Input or Company identifier is null or empty.",
                ErrorType.Validation);
            logger.LogError("Invalid input for company orchestrator: {Error}", error);
            return Result.Fail(error);
        }

        logger.LogInformation("Sub-orchestration started for company {Company}.", input.Company);

        // 1. Create the journal header.
        var headerResult = await context.CallActivityAsync<Result<LedgerJournalHeader>>(
            nameof(JournalActivities.CreateJournalHeaderActivity),
            input.Company);

        if (headerResult.IsFailure)
        {
            logger.LogError("Journal header creation failed for company {Company}: {Error}", input.Company, headerResult.Error);
            return headerResult; // Propagate the failure result.
        }

        var journalBatchNumber = headerResult.Value?.JournalBatchNumber;
        if (string.IsNullOrEmpty(journalBatchNumber))
        {
            var error = new Error(
                "CompanyOrchestrator.HeaderMissingBatchNumber",
                "Created journal header is missing a batch number.",
                ErrorType.Failure);
            logger.LogError("Critical error for company {Company}: {Error}", input.Company, error);
            return Result.Fail(error);
        }

        if (input.Lines == null || input.Lines.Count == 0)
        {
            // This is not a failure, just an informational outcome.
            logger.LogInformation("No journal lines provided for processing for company {Company}.", input.Company);
            return Result.Ok();
        }

        // 2. Map the source lines to the target format.
        var mappingInput = new MapLinesActivityInput
        {
            JournalBatchNumber = journalBatchNumber,
            Lines = input.Lines
        };

        var mappedLinesResult = await context.CallActivityAsync<Result<List<LedgerJournalLine>>>(
            nameof(JournalActivities.MapLinesActivity),
            mappingInput);

        if (mappedLinesResult.IsFailure)
        {
            logger.LogError("Error while mapping lines for company {Company}: {Error}", input.Company, mappedLinesResult.Error);
            return mappedLinesResult;
        }

        // 3. Create the journal lines in the target system.
        return await context.CallActivityAsync<Result>(
            nameof(JournalActivities.CreateJournalLinesActivity),
            mappedLinesResult.Value);
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
    public static async Task<Result> ProcessJournalOrchestrator([OrchestrationTrigger] TaskOrchestrationContext context)
    {
        var logger = context.CreateReplaySafeLogger(nameof(ProcessJournalOrchestrator));
        var businessEvent = context.GetInput<HTTPOrchestratorInput>();

        if (businessEvent == null || string.IsNullOrEmpty(businessEvent.BusinessEventLegalEntity) || string.IsNullOrEmpty(businessEvent.BusinessEventId))
        {
            var error = new Error(
                "ProcessJournalOrchestrator.InvalidInput",
                "BusinessEventId or BusinessEventLegalEntity is null or empty.",
                ErrorType.Validation);
            logger.LogError("Invalid input for business event: {Error}", error);
            return Result.Fail(error);
        }

        logger.LogInformation("Orchestration started for business event {BusinessEventId} in legal entity {LegalEntity}.",
            businessEvent.BusinessEventId, businessEvent.BusinessEventLegalEntity);

        try
        {
            // 1. Fetch data from the source system.
            var journalLinesResult = await context.CallActivityAsync<Result<List<RelionLedgerJournalLine>>>(
                nameof(JournalActivities.GetRelionJournalLinesActivity),
                businessEvent.ImportDate);

            if (journalLinesResult.IsFailure)
            {
                logger.LogWarning("Fetching journal lines from Relion failed: {Error}", journalLinesResult.Error);
                return journalLinesResult;
            }

            var journalLines = journalLinesResult.Value;
            if (journalLines == null || journalLines.Count == 0)
            {
                logger.LogInformation("No new journal lines found in Relion since {ImportDate}. Orchestration ending.", businessEvent.ImportDate);
                return Result.Ok();
            }

            logger.LogInformation("Fetched {Count} journal lines from Relion. Uploading to blob storage.", journalLines.Count);

            // 2. Persist the fetched data to a blob to trigger the next stage.
            var blobPayload = new WriteJournalLinesActivityInput
            {
                BlobName = $"relion_journals_{DateTime.UtcNow:yyyyMMddHHmmss}.json",
                Lines = journalLines
            };

            await context.CallActivityAsync(
                nameof(JournalActivities.WriteJournalLinesToBlobActivity),
                blobPayload);

            logger.LogInformation("Upload successful. Blob {BlobName} has been created and queued for processing.", blobPayload.BlobName);
            return Result.Ok();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "An unexpected error occurred during the journal ingestion orchestration: {Message}", ex.Message);
            return Result.Fail(new Error(
                "Orchestration.UnexpectedError",
                $"An unexpected error occurred: {ex.Message}",
                ErrorType.Failure
            ));
        }
    }
}