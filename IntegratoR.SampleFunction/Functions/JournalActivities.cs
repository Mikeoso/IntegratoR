using IntegratoR.Abstractions.Common.Result;
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
using IntegratoR.SampleFunction.Features.Commands.General.CreateRelionErrorEntry;
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
        public async Task<Result<LedgerJournalHeader>> CreateJournalHeaderActivity([ActivityTrigger] string company)
        {
            _logger.LogInformation("Creating journal header for company {Company}...", company);

            // Define new journal header command
            var newLedgerJournalHeader = new LedgerJournalHeader
            {
                DataAreaId = company,
                JournalName = "RELion",
                Description = $"RELion_{DateTime.Now:yyyyMMddHHmm}"
            };
            var createHeaderResult = await _mediator.Send(new CreateLedgerJournalHeaderCommand<LedgerJournalHeader>(newLedgerJournalHeader));

            if (createHeaderResult.IsFailure)
            {
                _logger.LogError("Failed to create journal header for company {Company}: {Error}", company, createHeaderResult.Error);
                return Result<LedgerJournalHeader>.Fail(createHeaderResult);
            }

            _logger.LogInformation("Created journal header with batch number {BatchNumber}.", createHeaderResult.Value?.JournalBatchNumber);
            return Result<LedgerJournalHeader>.Ok(createHeaderResult?.Value!);
        }

        /// <summary>
        /// Maps RelionLedgerJournalLine objects to LedgerJournalLine entities for F&O.
        /// </summary>
        /// <param name="input">List of relion ledger journals and their corresponding batch number</param>
        /// <returns>A List of mapped LedgerJournalLines</returns>
        [Function(nameof(MapLinesActivity))]
        public async Task<Result<List<LedgerJournalLine>>> MapLinesActivity(
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
                return Result<List<LedgerJournalLine>>.Fail(error!);
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

                    return Result<List<LedgerJournalLine>>.Fail(error!);
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

            return Result<List<LedgerJournalLine>>.Ok(mappedLines);
        }

        [Function(nameof(CreateJournalLinesActivity))]
        public async Task<Result> CreateJournalLinesActivity([ActivityTrigger] List<LedgerJournalLine> lines)
        {
            _logger.LogInformation("Creating {Count} journal lines for batch {JournalBatchNumber}...", lines.Count, lines.First().JournalBatchNumber);

            return Result.Ok();
        }

        [Function(nameof(GetRelionJournalLinesActivity))]
        public async Task<Result<List<RelionLedgerJournalLine>>> GetRelionJournalLinesActivity([ActivityTrigger] DateTime importDate)
        {
            _logger.LogInformation("Fetching journal lines from Relion since {ImportDate} UTC", importDate);

            var result = await _relionSerivce.GetNewJournalLinesAsync(importDate);

            if (result.IsFailure)
            {
                _logger.LogError("Failed to fetch journal lines from Relion: {Error}", result.Error);
                return Result<List<RelionLedgerJournalLine>>.Fail(result);
            }
            _logger.LogInformation("Fetched {Count} journal lines from Relion.", result.Value?.Count ?? 0);
            return Result<List<RelionLedgerJournalLine>>.Ok(result.Value!); ;
        }

        [Function(nameof(WriteJournalLinesToBlobActivity))]
        public WriteJournalLinesActivityResult WriteJournalLinesToBlobActivity([ActivityTrigger] WriteJournalLinesActivityInput input)
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