using Azure.Storage.Blobs;
using IntegratoR.Abstractions.Common.Results;
using IntegratoR.OData.FO.Builders;
using IntegratoR.OData.FO.Domain.Entities.LedgerJournal;
using IntegratoR.OData.FO.Domain.Enums.General;
using IntegratoR.OData.FO.Domain.Enums.LedgerJournals;
using IntegratoR.OData.FO.Domain.Models.Settings;
using IntegratoR.OData.FO.Features.Commands.LedgerJournals.CreateLedgerJournalHeader;
using IntegratoR.OData.FO.Features.Queries.Dimensions.GetDimensionOrder;
using IntegratoR.RELion.Domain.Models;
using IntegratoR.RELion.Features.Queries.Ledger.GetLedgerAccountMapping;
using IntegratoR.RELion.Interfaces.Services;
using IntegratoR.SampleFunction.Domain.DTOs.Activities;
using IntegratoR.SampleFunction.Domain.Entities.LedgerJournal;
using IntegratoR.SampleFunction.Domain.Enums;
using IntegratoR.SampleFunction.Features.Commands.General.CreateRelionErrorProtocol;
using IntegratoR.SampleFunction.Features.Queries.Ledger.GetLedgerAccountMapping;
using IntegratoR.SampleFunction.Features.Queries.Tax.GetItemTaxGroupMapping;
using IntegratoR.SampleFunction.Features.Queries.Tax.GetTaxGroupMapping;
using MediatR;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using System.Text;

namespace IntegratoR.SampleFunction.Functions
{
    /// <summary>
    /// Contains all activity functions for processing journal files.
    /// </summary>
    /// <remarks>
    /// All activities now consistently follow the Result pattern, returning Result{T} instead of throwing exceptions.
    /// This ensures proper error handling and makes the orchestrator flow more explicit and maintainable.
    /// Orchestrators can now handle failures gracefully without try-catch blocks.
    /// </remarks>
    public class JournalActivities(
        ILogger<JournalActivities> logger,
        IMediator mediator,
        IRelionService relionService,
        IOptions<FOSettings> foSettings)
    {
        private readonly ILogger<JournalActivities> _logger = logger;
        private readonly IMediator _mediator = mediator;
        private readonly IRelionService _relionService = relionService;
        private readonly FOSettings _foSettings = foSettings.Value;

        /// <summary>
        /// Reads a blob from Azure Blob Storage.
        /// This is needed because Durable Functions have a ~4-5 MB input size limit.
        /// </summary>
        /// <param name="blobName">The name of the blob to read from the 'input' container.</param>
        /// <returns>
        /// A <see cref="Result{T}"/> containing the blob content as byte array on success,
        /// or an error if the blob cannot be found or read.
        /// </returns>
        [Function(nameof(ReadBlobActivity))]
        public async Task<Result<byte[]>> ReadBlobActivity([ActivityTrigger] string blobName)
        {
            _logger.LogInformation("Reading blob {BlobName} from storage...", blobName);

            try
            {
                // Get connection string from environment (AzureWebJobsStorage)
                var connectionString = Environment.GetEnvironmentVariable("AzureWebJobsStorage");

                if (string.IsNullOrEmpty(connectionString))
                {
                    var error = new Error(
                        "BlobStorage.ConnectionStringNotFound",
                        "AzureWebJobsStorage connection string not found in environment variables.",
                        ErrorType.Failure);

                    _logger.LogError("AzureWebJobsStorage connection string not found");
                    return Result<byte[]>.Fail(error);
                }

                // Create blob client
                var blobServiceClient = new BlobServiceClient(connectionString);
                var containerClient = blobServiceClient.GetBlobContainerClient("input");
                var blobClient = containerClient.GetBlobClient(blobName);

                // Check if blob exists
                if (!await blobClient.ExistsAsync())
                {
                    var error = new Error(
                        "BlobStorage.BlobNotFound",
                        $"Blob {blobName} not found in container 'input'.",
                        ErrorType.NotFound);

                    _logger.LogError("Blob {BlobName} not found in container 'input'", blobName);
                    return Result<byte[]>.Fail(error);
                }

                // Download blob content
                using var memoryStream = new MemoryStream();
                await blobClient.DownloadToAsync(memoryStream);

                var content = memoryStream.ToArray();

                _logger.LogInformation(
                    "Successfully read blob {BlobName} ({SizeKB:N2} KB)",
                    blobName,
                    content.Length / 1024.0);

                return Result<byte[]>.Ok(content);
            }
            catch (Exception ex)
            {
                var error = new Error(
                    "BlobStorage.ReadFailed",
                    $"Failed to read blob {blobName}: {ex.Message}",
                    ErrorType.Failure,
                    ex);

                _logger.LogError(ex, "Failed to read blob {BlobName}", blobName);
                return Result<byte[]>.Fail(error);
            }
        }

        /// <summary>
        /// Parses the JSON content of a journal file into a list of RelionLedgerJournalLine objects.
        /// </summary>
        /// <param name="jsonContent">The json content as byte array.</param>
        /// <returns>
        /// A <see cref="Result{T}"/> containing a list of relion ledger journal lines on success,
        /// or an error if parsing fails.
        /// </returns>
        [Function(nameof(ParseJournalFileActivity))]
        public Result<List<RelionLedgerJournalLine>> ParseJournalFileActivity(
            [ActivityTrigger] byte[] jsonContent)
        {
            _logger.LogInformation("Parsing file...");

            try
            {
                // Parse byte array to string and convert to object
                var jsonString = Encoding.UTF8.GetString(jsonContent);

                var wrapper = JsonConvert.DeserializeObject<JournalFileWrapper>(jsonString);
                var lines = wrapper?.Data;

                // Log warning when parsing returns empty result
                if (lines == null || lines.Count == 0)
                {
                    _logger.LogWarning("File content is empty or invalid.");
                    return Result<List<RelionLedgerJournalLine>>.Ok(new List<RelionLedgerJournalLine>());
                }

                _logger.LogInformation("Successfully parsed {Count} journal lines", lines.Count);
                return Result<List<RelionLedgerJournalLine>>.Ok(lines);
            }
            catch (Exception ex)
            {
                var error = new Error(
                    "JournalFile.ParseFailed",
                    $"Failed to parse journal file: {ex.Message}",
                    ErrorType.Failure,
                    ex);

                _logger.LogError(ex, "Failed to parse journal file");
                return Result<List<RelionLedgerJournalLine>>.Fail(error);
            }
        }

        /// <summary>
        /// Creates a new journal header in F&O for the specified company.
        /// </summary>
        /// <param name="company">The legal entity id.</param>
        /// <returns>
        /// A <see cref="Result{T}"/> containing the created journal header on success,
        /// or an error if creation fails.
        /// </returns>
        [Function(nameof(CreateJournalHeaderActivity))]
        public async Task<Result<LedgerJournalHeader>> CreateJournalHeaderActivity([ActivityTrigger] string company)
        {
            _logger.LogInformation("Creating journal header for company {Company}...", company);

            var newLedgerJournalHeader = new LedgerJournalHeader
            {
                DataAreaId = company,
                JournalName = "RELion",
                Description = $"RELion_{DateTime.Now:yyyyMMddHHmm}"
            };

            var createHeaderResult = await _mediator.Send(
                new CreateLedgerJournalHeaderCommand<LedgerJournalHeader>(newLedgerJournalHeader));

            if (createHeaderResult.IsFailure)
            {
                _logger.LogError("Failed to create journal header for company {Company}: {Error}",
                    company, createHeaderResult.Error?.Message);

                return Result<LedgerJournalHeader>.Fail(createHeaderResult);
            }

            _logger.LogInformation("Created journal header with batch number {BatchNumber}.",
                createHeaderResult.Value?.JournalBatchNumber);

            return createHeaderResult;
        }

        /// <summary>
        /// Maps RelionLedgerJournalLine objects to LedgerJournalLine entities for F&O.
        /// </summary>
        /// <param name="input">List of relion ledger journals and their corresponding batch number.</param>
        /// <returns>
        /// A <see cref="Result{T}"/> containing a list of mapped LedgerJournalLines on success,
        /// or an error if mapping fails critically. Individual line errors are logged but don't fail the entire operation.
        /// </returns>
        [Function(nameof(MapLinesActivity))]
        public async Task<Result<List<LedgerJournalLine>>> MapLinesActivity(
            [ActivityTrigger] MapLinesActivityInput input)
        {
            _logger.LogInformation("Mapping {Count} lines for journal {JournalBatchNumber}...",
                input.Lines.Count, input.JournalBatchNumber);

            try
            {
                var mappedLines = new List<LedgerJournalLine>();
                var headerBatchNumber = input.JournalBatchNumber;
                var lines = input.Lines;

                // Get dimension order configuration
                var dimensionOrder = await _mediator.Send(
                    new GetDimensionOrdersQuery(_foSettings.DimensionFormatName, _foSettings.DimensionHierarchyType));

                if (dimensionOrder.IsFailure)
                {
                    _logger.LogError("Failed to retrieve dimension order: {Error}", dimensionOrder.Error?.Message);
                    return Result<List<LedgerJournalLine>>.Fail(dimensionOrder.Error!);
                }

                foreach (RelionLedgerJournalLine line in lines)
                {
                    _logger.LogDebug("Mapping line with Relion Entry ID {EntryId}...", line.EntryNo);

                    var currentCompany = line.RelCompetenceUnit;
                    var ifrs = string.IsNullOrEmpty(line.ShortcutDimensionCode) ? string.Empty : line.ShortcutDimensionCode;

                    // Retrieve ledger account mapping
                    var ledgerAccountMapping = await _mediator.Send(
                        new GetLedgerAccountMappingQuery(line.AccountNum, ifrs));

                    if (ledgerAccountMapping.IsFailure)
                    {
                        var error = new Error(
                            "MapLinesActivity.LedgerAccountMappingFailed",
                            $"Failed to retrieve ledger account mapping for Relion Account {line.AccountNum} and IFRS {ifrs} for EntryNo {line.EntryNo}.",
                            ErrorType.Failure);

                        _logger.LogWarning("{Error}", error.Message);

                        // Log to error protocol but continue with other lines
                        await _mediator.Send(new CreateRelionErrorProcotolCommand(
                            currentCompany, line.EntryNo.ToString(), error.Message, "MapLinesActivity"));
                        continue;
                    }

                    if (ledgerAccountMapping.Value == null)
                    {
                        _logger.LogWarning(
                            "No ledger account mapping found for Relion Account {RelionAccount} and IFRS {IFRS}. Skipping line with Entry ID {EntryId}.",
                            line.AccountNum, ifrs, line.EntryNo);
                        continue;
                    }

                    var mappingResult = ledgerAccountMapping.Value;
                    var isExcludedFromImport = mappingResult.ExcludeFromImport;
                    var mappingExists = false;

                    // Check for existing Relion account mapping if excluded
                    if (isExcludedFromImport == NoYes.Yes)
                    {
                        var relionAccountMapping = await _mediator.Send(
                            new GetRelionLedgerAccountMappingQuery(EntryNo: line.EntryNo));

                        if (relionAccountMapping.IsFailure)
                        {
                            _logger.LogError(
                                "Failed to retrieve Relion ledger account mapping for EntryNo {EntryNo}: {Error}",
                                line.EntryNo, relionAccountMapping.Error?.Message);
                        }
                        else if (!string.IsNullOrEmpty(relionAccountMapping?.Value?.LedgerAccountNo))
                        {
                            mappingExists = true;
                        }
                    }

                    // Build financial dimensions string
                    var financialDimensions = new FinancialDimensionBuilder()
                        .Initialize(dimensionOrder.Value!)
                        .Add("MainAccount", mappingResult.MainAccount!)
                        .Add("D_Projekte", line.RelObjectNum)
                        .Add("G_Bewegungsarten", line.MovementType)
                        .Add("H_Partnergesellschaft", line.ICPartnerCode)
                        .Build();

                    // Create the journal line entity
                    var ledgerJournalLine = new LedgerJournalLineExtension
                    {
                        DataAreaId = currentCompany,
                        JournalBatchNumber = headerBatchNumber,
                        AccountType = LedgerJournalACType.Ledger,
                        CreditAmount = line.CreditAmount ?? 0m,
                        DebitAmount = line.DebitAmount ?? 0m,
                        CurrencyCode = "EUR",
                        Voucher = line.DocumentNo,
                        TransactionText = line.Description + line.RelDescription,
                        ExchRate = 100,
                        Document = line.DocumentNo,
                        Invoice = line.ExternalDocumentNo,
                        DocumentDate = line.DocumentDate ?? default,
                        TransDate = line.PostingDate,
                        AccountDisplayValue = financialDimensions
                    };

                    var postingProfile = line.PostingType;

                    // Skip lines that are excluded without posting profile
                    if (!mappingExists && isExcludedFromImport == NoYes.Yes && postingProfile == 0)
                    {
                        _logger.LogWarning(
                            "Line with Entry ID {EntryId} is marked as excluded from import and has no posting profile. Skipping line.",
                            line.EntryNo);
                        continue;
                    }

                    // Handle tax group mapping if posting profile exists
                    if (postingProfile > 0)
                    {
                        var postingTypeEnum = postingProfile switch
                        {
                            1 => RelionBookingType.Purchase,
                            2 => RelionBookingType.Sale,
                            _ => RelionBookingType.Purchase
                        };

                        // Validate posting group
                        if (string.IsNullOrEmpty(line.PostingGroup))
                        {
                            await _mediator.Send(new CreateRelionErrorProcotolCommand(
                                currentCompany,
                                line.EntryNo.ToString(),
                                $"Missing posting group for Relion Account {line.AccountNum} and IFRS {ifrs} for EntryNo {line.EntryNo}.",
                                "MapLinesActivity"));

                            _logger.LogWarning(
                                "Missing posting group for Relion Account {RelionAccount} and IFRS {IFRS}. Skipping line with Entry ID {EntryId}.",
                                line.AccountNum, ifrs, line.EntryNo);
                            continue;
                        }

                        // Get tax group mapping
                        var taxGroup = await _mediator.Send(
                            new GetTaxGroupMappingQuery(postingTypeEnum, line.PostingGroup));

                        if (taxGroup.IsFailure)
                        {
                            var error = new Error(
                                "MapLinesActivity.TaxGroupMappingFailed",
                                $"Failed to retrieve tax group mapping for PostingType {postingTypeEnum} and PostingGroup {line.PostingGroup} for EntryNo {line.EntryNo}.",
                                ErrorType.Failure);

                            _logger.LogWarning("{Error}", error.Message);

                            await _mediator.Send(new CreateRelionErrorProcotolCommand(
                                currentCompany, line.EntryNo.ToString(), error.Message, "MapLinesActivity"));
                            continue;
                        }

                        // Validate VAT posting groups
                        if (string.IsNullOrEmpty(line.VATBusPostingGroup) || string.IsNullOrEmpty(line.VATProdPostingGroup))
                        {
                            var error = new Error(
                                "MapLinesActivity.MissingTaxPostingGroups",
                                $"Missing VAT posting groups for EntryNo {line.EntryNo}. VATBusPostingGroup: '{line.VATBusPostingGroup}', VATProdPostingGroup: '{line.VATProdPostingGroup}'.",
                                ErrorType.Failure);

                            _logger.LogWarning("{Error}", error.Message);

                            await _mediator.Send(new CreateRelionErrorProcotolCommand(
                                currentCompany, line.EntryNo.ToString(), error.Message, "MapLinesActivity"));
                            continue;
                        }

                        // Get item tax group mapping
                        var itemTaxGroup = await _mediator.Send(
                            new GetItemTaxGroupMappingQuery(line.VATBusPostingGroup, line.VATProdPostingGroup));

                        if (itemTaxGroup.IsFailure)
                        {
                            var error = new Error(
                                "MapLinesActivity.ItemTaxGroupMappingFailed",
                                $"Failed to retrieve item tax group mapping for VATBusPostingGroup {line.VATBusPostingGroup} and VATProdPostingGroup {line.VATProdPostingGroup} for EntryNo {line.EntryNo}.",
                                ErrorType.Failure);

                            _logger.LogWarning("{Error}", error.Message);
                            continue;
                        }

                        // Apply tax settings based on exclusion status
                        if (isExcludedFromImport == NoYes.Yes)
                        {
                            ledgerJournalLine.SalesTaxCode = itemTaxGroup?.Value?.TaxCode;
                        }
                        else
                        {
                            ledgerJournalLine.SalesTaxGroup = taxGroup?.Value?.TaxGroup;
                            ledgerJournalLine.ItemSalesTaxGroup = itemTaxGroup?.Value?.TaxItemGroup;
                        }
                    }

                    // Adjust amounts based on VAT and exclusion status
                    if (isExcludedFromImport == NoYes.Yes)
                    {
                        ledgerJournalLine.CreditAmount = -line.VatAmount;
                    }
                    else
                    {
                        ledgerJournalLine.CreditAmount = line.CreditAmount != 0
                            ? ledgerJournalLine.CreditAmount - line.VatAmount
                            : 0;
                        ledgerJournalLine.DebitAmount = line.DebitAmount != 0
                            ? ledgerJournalLine.DebitAmount + line.VatAmount
                            : 0;
                    }

                    mappedLines.Add(ledgerJournalLine);
                }

                _logger.LogInformation(
                    "Successfully mapped {MappedCount} of {TotalCount} lines",
                    mappedLines.Count, lines.Count);

                return Result<List<LedgerJournalLine>>.Ok(mappedLines);
            }
            catch (Exception ex)
            {
                var error = new Error(
                    "MapLinesActivity.UnexpectedError",
                    $"Unexpected error during line mapping: {ex.Message}",
                    ErrorType.Failure,
                    ex);

                _logger.LogError(ex, "Unexpected error during line mapping");
                return Result<List<LedgerJournalLine>>.Fail(error);
            }
        }

        /// <summary>
        /// Creates journal lines in F&O.
        /// </summary>
        /// <param name="lines">List of ledger journal lines to create.</param>
        /// <returns>
        /// A <see cref="Result"/> indicating success or failure of the creation operation.
        /// </returns>
        [Function(nameof(CreateJournalLinesActivity))]
        public async Task<Result> CreateJournalLinesActivity([ActivityTrigger] List<LedgerJournalLine> lines)
        {
            if (lines == null || !lines.Any())
            {
                _logger.LogWarning("No lines provided to create");
                return Result.Ok();
            }

            _logger.LogInformation("Creating {Count} journal lines for batch {JournalBatchNumber}...",
                lines.Count, lines.First().JournalBatchNumber);

            // TODO: Implement actual creation logic via MediatR command
            // For now, just return success
            await Task.CompletedTask;
            return Result.Ok();
        }

        /// <summary>
        /// Fetches journal lines from Relion since a specific import date.
        /// </summary>
        /// <param name="importDate">The timestamp to fetch records from.</param>
        /// <returns>
        /// A <see cref="Result{T}"/> containing a list of Relion journal lines on success,
        /// or an error if the fetch fails.
        /// </returns>
        [Function(nameof(GetRelionJournalLinesActivity))]
        public async Task<Result<List<RelionLedgerJournalLine>>> GetRelionJournalLinesActivity(
            [ActivityTrigger] DateTime importDate)
        {
            _logger.LogInformation("Fetching journal lines from Relion since {ImportDate} UTC", importDate);

            var result = await _relionService.GetNewJournalLinesAsync(importDate);

            if (result.IsFailure)
            {
                _logger.LogError("Failed to fetch journal lines from Relion: {Error}", result.Error?.Message);
                return result;
            }

            _logger.LogInformation("Fetched {Count} journal lines from Relion.", result.Value?.Count ?? 0);
            return result;
        }

        /// <summary>
        /// Writes journal lines to a blob in Azure Storage.
        /// </summary>
        /// <param name="input">The input containing blob name and lines to write.</param>
        /// <returns>
        /// A <see cref="Result{T}"/> containing the activity result with blob details on success,
        /// or an error if writing fails.
        /// </returns>
        [Function(nameof(WriteJournalLinesToBlobActivity))]
        public Result<WriteJournalLinesActivityResult> WriteJournalLinesToBlobActivity(
            [ActivityTrigger] WriteJournalLinesActivityInput input)
        {
            try
            {
                var wrapper = new JournalFileWrapper
                {
                    Data = [.. input.Lines]
                };

                var contentBytes = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(wrapper));

                var activityResult = new WriteJournalLinesActivityResult
                {
                    BlobName = input.BlobName,
                    BlobContent = contentBytes
                };

                _logger.LogInformation("Prepared blob content for {BlobName} ({Size} bytes)",
                    input.BlobName, contentBytes.Length);

                return Result<WriteJournalLinesActivityResult>.Ok(activityResult);
            }
            catch (Exception ex)
            {
                var error = new Error(
                    "WriteBlob.SerializationFailed",
                    $"Failed to serialize journal lines: {ex.Message}",
                    ErrorType.Failure,
                    ex);

                _logger.LogError(ex, "Failed to serialize journal lines");
                return Result<WriteJournalLinesActivityResult>.Fail(error);
            }
        }
    }
}