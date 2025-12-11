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
using IntegratoR.RELion.Domain.Settings;
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
using System.Collections.Generic;
using System.IO.IsolatedStorage;
using System.Text;

namespace IntegratoR.SampleFunction.Functions
{
    /// <summary>
    /// Contains all activity functions for processing journal files.
    /// </summary>
    public class JournalActivities(
        ILogger<JournalActivities> logger,
        IMediator mediator,
        IRelionService relionService,
        IOptions<FOSettings> foSettings)
    {
        private readonly ILogger<JournalActivities> _logger = logger;
        private readonly IMediator _mediator = mediator;
        private readonly IRelionService _relionSerivce = relionService;
        private readonly FOSettings _foSettings = foSettings.Value;

        /// <summary>
        /// Reads a blob from Azure Blob Storage.
        /// This is needed because Durable Functions have a ~4-5 MB input size limit.
        /// </summary>
        /// <param name="blobName">The name of the blob to read from the 'input' container.</param>
        /// <returns>The blob content as byte array.</returns>
        [Function(nameof(ReadBlobActivity))]
        public async Task<byte[]> ReadBlobActivity([ActivityTrigger] string blobName)
        {
            _logger.LogInformation("Reading blob {BlobName} from storage...", blobName);

            try
            {
                // Get connection string from environment (AzureWebJobsStorage)
                var connectionString = Environment.GetEnvironmentVariable("AzureWebJobsStorage");

                if (string.IsNullOrEmpty(connectionString))
                {
                    throw new InvalidOperationException(
                        "AzureWebJobsStorage connection string not found in environment variables.");
                }

                // Create blob client
                var blobServiceClient = new BlobServiceClient(connectionString);
                var containerClient = blobServiceClient.GetBlobContainerClient("input");
                var blobClient = containerClient.GetBlobClient(blobName);

                // Check if blob exists
                if (!await blobClient.ExistsAsync())
                {
                    throw new FileNotFoundException($"Blob {blobName} not found in container 'input'.");
                }

                // Download blob content
                using var memoryStream = new MemoryStream();
                await blobClient.DownloadToAsync(memoryStream);

                var content = memoryStream.ToArray();

                _logger.LogInformation(
                    "Successfully read blob {BlobName} ({SizeKB:N2} KB)",
                    blobName,
                    content.Length / 1024.0);

                return content;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Failed to read blob {BlobName}: {Error}",
                    blobName, ex.Message);
                throw;
            }
        }
    
        /// <summary>
        /// Parses the JSON content of a journal file into a list of RelionLedgerJournalLine objects.
        /// </summary>
        /// <param name="jsonContent">The json content</param>
        /// <returns>A list of relion ledger journal lines</returns>
        [Function(nameof(ParseJournalFileActivity))]
        public List<RelionLedgerJournalLine> ParseJournalFileActivity(
            [ActivityTrigger] byte[] jsonContent)
        {
            _logger.LogInformation("Parsing file...");

            // Parse byte array to string and convert to object
            var jsonString = Encoding.UTF8.GetString(jsonContent);

            var wrapper = JsonConvert.DeserializeObject<JournalFileWrapper>(jsonString);
            var lines = wrapper?.Data;

            // Log warning when parsing failed
            if (lines == null || lines.Count == 0)
            {
                _logger.LogWarning("File content is empty or invalid.");
                return [];
            }

            return lines;
        }

        /// <summary>
        /// Creates a new journal header in F&O for the specified company.
        /// </summary>
        /// <param name="company">The legal entity id</param>
        /// <returns></returns>
        [Function(nameof(CreateJournalHeaderActivity))]
        public async Task<LedgerJournalHeader> CreateJournalHeaderActivity([ActivityTrigger] string company)
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
                    company, createHeaderResult.Error);

                // Throw so Durable Functions can retry/handle
                throw new InvalidOperationException(
                    $"Failed to create journal header: {createHeaderResult.Error?.Message}");
            }

            _logger.LogInformation("Created journal header with batch number {BatchNumber}.",
                createHeaderResult.Value?.JournalBatchNumber);

            return createHeaderResult.Value!;
        }

        /// <summary>
        /// Maps RelionLedgerJournalLine objects to LedgerJournalLine entities for F&O.
        /// </summary>
        /// <param name="input">List of relion ledger journals and their corresponding batch number</param>
        /// <returns>A List of mapped LedgerJournalLines</returns>
        [Function(nameof(MapLinesActivity))]
        public async Task<List<LedgerJournalLine>> MapLinesActivity(
            [ActivityTrigger] MapLinesActivityInput input)
        {
            _logger.LogInformation("Mapping {Count} lines for journal {JournalBatchNumber}...", input.Lines.Count, input.JournalBatchNumber);

            var mappedLines = new List<LedgerJournalLine>();

            var headerBatchNumber = input.JournalBatchNumber;
            var lines = input.Lines;

            var dimensionOrder = await _mediator.Send(new GetDimensionOrdersQuery(_foSettings.DimensionFormatName, _foSettings.DimensionHierarchyType));

            if (dimensionOrder.IsFailure)
            {
                var error = dimensionOrder.Error;

                _logger.LogError("Failed to retrieve dimension order: {Error}", dimensionOrder.Error);
                throw new InvalidOperationException($"Failed to retrieve dimension order: {error?.Message}");
            }

            foreach (RelionLedgerJournalLine line in lines)
            {

                _logger.LogDebug("Mapping line with Relion Entry ID {EntryId}...", line.EntryNo);
                var currentCompany = line.RelCompetenceUnit;
                var ifrs = string.Empty;

                if (!string.IsNullOrEmpty(line.ShortcutDimensionCode))
                {
                    ifrs = line.ShortcutDimensionCode;
                }

                var ledgerAccountMapping = await _mediator.Send(new GetLedgerAccountMappingQuery(line.AccountNum, ifrs));

                if (ledgerAccountMapping.IsFailure)
                {
                    var error = new Error(
                        "MapLinesActivity.LedgerAccountMappingFailed",
                        $"Failed to retrieve ledger account mapping for Relion Account {line.AccountNum} and IFRS {ifrs} for EntryNo {line.EntryNo}.",
                        ErrorType.Failure);
                    _logger.LogError("{Error}", error);

                    var command = new CreateRelionErrorProcotolCommand(currentCompany, line.EntryNo.ToString(), error.Message, "MapLinesActivity");
                    await _mediator.Send(command);

                    throw new InvalidOperationException($"Failed to retrieve dimension order: {error?.Message}");
                }

                if (ledgerAccountMapping.Value == null)
                {
                    _logger.LogWarning("No ledger account mapping found for Relion Account {RelionAccount} and IFRS {IFRS}. Skipping line with Entry ID {EntryId}.",
                        line.AccountNum,
                        ifrs,
                        line.EntryNo);
                    continue;
                }

                var mappingResult = ledgerAccountMapping.Value;
                var isExcludedFromImport = mappingResult.ExcludeFromImport;
                var mappingExists = false;

                if (isExcludedFromImport == NoYes.Yes)
                {
                    var relionAccountMapping = await _mediator.Send(new GetRelionLedgerAccountMappingQuery(EntryNo: line.EntryNo));

                    if (relionAccountMapping.IsFailure)
                    {
                        _logger.LogError("Failed to retrieve Relion ledger account mapping for EntryNo {EntryNo}: {Error}",
                            line.EntryNo,
                            relionAccountMapping.Error);
                    }

                    if (!string.IsNullOrEmpty(relionAccountMapping?.Value?.LedgerAccountNo))
                    {
                        mappingExists = true;
                    }
                }

                var financialDimensions = new FinancialDimensionBuilder()
                    .Initialize(dimensionOrder.Value!)
                    .Add("MainAccount", ledgerAccountMapping.Value.MainAccount!)
                    .Add("D_Projekte", line.RelObjectNum)
                    .Add("G_Bewegungsarten", line.MovementType)
                    .Add("H_Partnergesellschaft", line.ICPartnerCode)
                    .Build();

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

                if (!mappingExists && isExcludedFromImport == NoYes.Yes && postingProfile == 0)
                {
                    _logger.LogWarning("Line with Entry ID {EntryId} is marked as excluded from import and has no posting profile. Skipping line.",
                        line.EntryNo);
                    continue;
                }

                if (postingProfile > 0)
                {
                    var postingTypeEnum = RelionBookingType.Purchase;

                    switch (postingProfile)
                    {
                        case 1:
                            postingTypeEnum = RelionBookingType.Purchase;
                            break;
                        case 2:
                            postingTypeEnum = RelionBookingType.Sale;
                            break;
                    }

                    if (string.IsNullOrEmpty(line.PostingGroup))
                    {
                        await _mediator.Send(new CreateRelionErrorProcotolCommand(
                            currentCompany,
                            line.EntryNo.ToString(),
                            $"Missing posting group for Relion Account {line.AccountNum} and IFRS {ifrs} for EntryNo {line.EntryNo}.",
                            "MapLinesActivity"));
                        _logger.LogWarning("Missing posting group for Relion Account {RelionAccount} and IFRS {IFRS}. Skipping line with Entry ID {EntryId}.", line.AccountNum, ifrs, line.EntryNo);
                        continue;
                    }

                    var taxGroup = await _mediator.Send(new GetTaxGroupMappingQuery(postingTypeEnum, line.PostingGroup));

                    if (taxGroup.IsFailure)
                    {
                        var error = new Error(
                            "MapLinesActivity.TaxGroupMappingFailed",
                            $"Failed to retrieve tax group mapping for PostingType {postingTypeEnum} and PostingGroup {line.PostingGroup} for EntryNo {line.EntryNo}.",
                            ErrorType.Failure);
                        _logger.LogError("{Error}", error);
                        var command = new CreateRelionErrorProcotolCommand(currentCompany, line.EntryNo.ToString(), error.Message, "MapLinesActivity");
                        await _mediator.Send(command);
                        continue;
                    }

                    if (string.IsNullOrEmpty(line.VATBusPostingGroup) || string.IsNullOrEmpty(line.VATProdPostingGroup))
                    {
                        var error = new Error(
                            "MapLinesActivity.MissingTaxPostingGroups",
                            $"Missing VAT posting groups for EntryNo {line.EntryNo}. VATBusPostingGroup: '{line.VATBusPostingGroup}', VATProdPostingGroup: '{line.VATProdPostingGroup}'.",
                            ErrorType.Failure);
                        _logger.LogError("{Error}", error);
                        var command = new CreateRelionErrorProcotolCommand(currentCompany, line.EntryNo.ToString(), error.Message, "MapLinesActivity");
                        await _mediator.Send(command);
                        continue;
                    }

                    var itemTaxGroup = await _mediator.Send(new GetItemTaxGroupMappingQuery(line.VATBusPostingGroup, line.VATProdPostingGroup));

                    if (itemTaxGroup.IsFailure)
                    {
                        var error = new Error(
                            "MapLinesActivity.ItemTaxGroupMappingFailed",
                            $"Failed to retrieve item tax group mapping for VATBusPostingGroup {line.VATBusPostingGroup} and VATProdPostingGroup {line.VATProdPostingGroup} for EntryNo {line.EntryNo}.",
                            ErrorType.Failure);
                        _logger.LogError("{Error}", error);
                        continue;
                    }

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

                if (isExcludedFromImport == NoYes.Yes)
                {
                    ledgerJournalLine.CreditAmount = -line.VatAmount;
                }
                else
                {
                    ledgerJournalLine.CreditAmount = line.CreditAmount != 0 ? ledgerJournalLine.CreditAmount - line.VatAmount : 0;
                    ledgerJournalLine.DebitAmount = line.DebitAmount != 0 ? ledgerJournalLine.DebitAmount + line.VatAmount : 0;
                }
                mappedLines.Add(ledgerJournalLine);
            }

            return mappedLines;
        }

        [Function(nameof(CreateJournalLinesActivity))]
        public async Task CreateJournalLinesActivity([ActivityTrigger] List<LedgerJournalLine> lines)
        {
            _logger.LogInformation("Creating {Count} journal lines for batch {JournalBatchNumber}...", lines.Count, lines.First().JournalBatchNumber);

            return;
        }

        [Function(nameof(GetRelionJournalLinesActivity))]
        public async Task<List<RelionLedgerJournalLine>> GetRelionJournalLinesActivity(
            [ActivityTrigger] DateTime importDate)
        {
            _logger.LogInformation("Fetching journal lines from Relion since {ImportDate} UTC", importDate);

            var result = await _relionSerivce.GetNewJournalLinesAsync(importDate);

            if (result.IsFailure)
            {
                _logger.LogError("Failed to fetch journal lines from Relion: {Error}", result.Error);
                throw new InvalidOperationException($"Failed to fetch journal lines: {result.Error?.Message}");
            }

            _logger.LogInformation("Fetched {Count} journal lines from Relion.", result.Value?.Count ?? 0);
            return result.Value!;
        }

        [Function(nameof(WriteJournalLinesToBlobActivity))]
        public WriteJournalLinesActivityResult WriteJournalLinesToBlobActivity(
            [ActivityTrigger] WriteJournalLinesActivityInput input)
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

            return activityResult;
        }
    }
}