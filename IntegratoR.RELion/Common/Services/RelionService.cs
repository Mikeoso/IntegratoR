using IntegratoR.Abstractions.Common.Results;
using IntegratoR.RELion.Domain.DTOs;
using IntegratoR.RELion.Domain.Models;
using IntegratoR.RELion.Domain.Settings;
using IntegratoR.RELion.Interfaces.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using System.Text;

namespace IntegratoR.RELion.Common.Services;

public class RelionService : IRelionService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<RelionService> _logger;
    private readonly RelionSettings _settings;

    private const int PageSize = 500;
    private const string LEDGER_JOURNAL_ENDPOINT = "/api/aareon/universalapi/v1.0/companies({0})/universalRequests?$expand=entitySet";

    public RelionService(IHttpClientFactory httpClientFactory, ILogger<RelionService> logger, IOptions<RelionSettings> settings)
    {
        _httpClient = httpClientFactory.CreateClient("RelionApiClient");
        _logger = logger;
        _settings = settings.Value;
    }

    #region API Calls
    public async Task<Result<List<RelionLedgerJournalLine>>> GetNewJournalLinesAsync(DateTime since, CancellationToken cancellationToken = default)
    {
        var allLines = new List<RelionLedgerJournalLine>();
        var moreRows = true;
        var recordsToSkip = 0;

        var journalLineFields = new List<string> { "1", "54", "53", "6", "7", "486", "482", "4", "56", "3", "5052251", "72", "48", "49", "64", "65", "43", "55", "5052493", "5052305" };

        _logger.LogInformation("Starting to fetch all pages for new journal lines from Relion since {SinceDate}.", since);

        var companyId = await GetCompanyByNameAsync(_settings.Company, cancellationToken);

        if (companyId.IsFailure)
        {
            var error = new Error(
                "RelionService.GetLedgerAccountMappingsAsync.CompanyNotFound",
                $"Failed to retrieve company information for {_settings.Company}.",
                ErrorType.Failure);
            return Result<List<RelionLedgerJournalLine>>.Fail(error);
        }

        while (moreRows)
        {
            var filter = new RelionRequestFilter
            {
                FieldNumber = "2000000001",
                Filter = true,
                Value = $">{since:O}"
            };

            var pageResult = await QueryAsync<RelionLedgerJournalLine>(
                companyId?.Value?.Id!,
                "17",
                new List<RelionRequestFilter> { filter },
                journalLineFields,
                cancellationToken,
                PageSize,
                recordsToSkip);

            if (pageResult.IsFailure)
            {
                return Result<List<RelionLedgerJournalLine>>.Fail(pageResult);
            }

            var (lines, hasMore) = pageResult.Value;
            if (lines is null || !lines.Any())
            {
                moreRows = false;
                continue;
            }

            allLines.AddRange(lines);
            recordsToSkip += PageSize;
            moreRows = hasMore;
        }

        _logger.LogInformation("Finished fetching data. Total lines retrieved: {TotalCount}", allLines.Count);
        return Result<List<RelionLedgerJournalLine>>.Ok(allLines);
    }

    public async Task<Result<RelionLedgerAccountMapping>> GetLedgerAccountMappingsAsync(int entryNo, CancellationToken cancellationToken = default)
    {
        var mappingFields = new List<string> { "1", "2" };
        _logger.LogInformation("Starting to fetch all ledger account mappings from Relion.");

        var filter = new RelionRequestFilter
        {
            FieldNumber = "1",
            Filter = true,
            Value = $"={entryNo}"
        };
        var companyId = await GetCompanyByNameAsync(_settings.Company, cancellationToken);

        if (companyId.IsFailure)
        {
            var error = new Error(
                "RelionService.GetLedgerAccountMappingsAsync.CompanyNotFound",
                $"Failed to retrieve company information for {_settings.Company}.",
                ErrorType.Failure);
            return Result<RelionLedgerAccountMapping>.Fail(error);
        }

        var pageResult = await QueryAsync<RelionLedgerAccountMapping>(
            companyId?.Value?.Id!,
            "253",
            new List<RelionRequestFilter>() { filter },
            mappingFields,
            cancellationToken,
            PageSize,
            0);

        if (pageResult.IsFailure)
        {
            var error = new Error(
                "RelionService.GetLedgerAccountMappingsAsync.Failed",
                $"Failed to retrieve ledger account mappings from Relion for EntryNo {entryNo}.",
                ErrorType.Failure);

            return Result<RelionLedgerAccountMapping>.Fail(error);
        }

        var mapping = pageResult.Value.Lines.FirstOrDefault();

        if (mapping == null)
        {
            return Result<RelionLedgerAccountMapping>.Ok(new RelionLedgerAccountMapping
            {
                LedgerAccountNo = string.Empty,
                TaxAccountNo = string.Empty
            });
        }
        _logger.LogInformation("Retrieved Relion Ledger Account Mapping for EntryNo: {EntryNo}", entryNo);
        return Result<RelionLedgerAccountMapping>.Ok(mapping);
    }

    public async Task<Result<RelionCompany>> GetCompanyByNameAsync(string companyName, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Fetching details for company {CompanyName}.", companyName);
        try
        {
            var response = await _httpClient.GetAsync($"{_settings.Url}/api/v2.0/companies", cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return Result<RelionCompany>.Fail(new Error("Relion.ApiError", $"API returned status code {response.StatusCode}.", ErrorType.Failure));
            }

            var companiesWrapper = JsonConvert.DeserializeObject<RelionCompanyDataWrapper<RelionCompany>>(await response.Content.ReadAsStringAsync(cancellationToken));
            var company = companiesWrapper?.Data.FirstOrDefault(c => c.Name.Equals(companyName, StringComparison.OrdinalIgnoreCase));

            if (company is null)
            {
                return Result<RelionCompany>.Fail(new Error("Relion.CompanyNotFound", $"Company with name '{companyName}' not found.", ErrorType.NotFound));
            }

            return Result<RelionCompany>.Ok(company);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An unexpected error occurred while fetching company data for {CompanyName}.", companyName);
            return Result<RelionCompany>.Fail(new Error("Relion.Exception", ex.Message, ErrorType.Failure, ex));
        }
    }
    #endregion

    #region Private Methods
    /// <summary>
    /// Async Query to Relion with paging support.
    /// </summary>
    private async Task<Result<(List<T> Lines, bool MoreRows)>> QueryAsync<T>(
        string companyId,
        string tableNumber,
        List<RelionRequestFilter> filters,
        List<string> responseFields,
        CancellationToken cancellationToken,
        int top,
        int skip)
    {
        try
        {
            filters.Add(new RelionRequestFilter
            {
                SubOperation = "DONE",
                ResponseFields = string.Join('|', responseFields)
            });

            var payload = new RelionRequest
            {
                TableNumber = tableNumber,
                Top = top,
                Skip = skip,
                EntitySet = filters
            };

            var jsonPayload = JsonConvert.SerializeObject(payload, new JsonSerializerSettings
            {
                DefaultValueHandling = DefaultValueHandling.Ignore
            });

            var relionCompany = await GetCompanyByNameAsync(_settings.Company);

            if (relionCompany.IsFailure)
            {
                _logger.LogError("Failed to retrieve company information: {Error}", relionCompany.Error);
                return Result<(List<T>, bool)>.Fail(relionCompany);
            }

            var baseUrl = _settings.Url + string.Format(LEDGER_JOURNAL_ENDPOINT, companyId);

            var response = await _httpClient.PostAsync(baseUrl, new StringContent(jsonPayload, Encoding.UTF8, "application/json"), cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogError("Failed to fetch data from Relion. Status: {StatusCode}, Response: {Response}", response.StatusCode, errorContent);
                return Result<(List<T>, bool)>.Fail(new Error("Relion.ApiError", $"API returned status code {response.StatusCode}.", ErrorType.Failure));
            }

            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            var responsePayload = JsonConvert.DeserializeObject<RelionResponsePayload>(content);
            var dataEntity = responsePayload?.EntitySet.FirstOrDefault(e => !string.IsNullOrEmpty(e.EncodedResponseJson));

            if (dataEntity is null)
            {
                return Result<(List<T>, bool)>.Ok((new List<T>(), false));
            }

            var decodedJson = Encoding.UTF8.GetString(Convert.FromBase64String(dataEntity.EncodedResponseJson!));
            var wrapper = JsonConvert.DeserializeObject<RelionDataWrapper<T>>(decodedJson);

            return Result<(List<T>, bool)>.Ok((wrapper?.Data ?? new List<T>(), dataEntity.MoreRows));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An unexpected error occurred in QueryAsync for table {TableNumber}.", tableNumber);
            return Result<(List<T>, bool)>.Fail(new Error("Relion.Exception", ex.Message, ErrorType.Failure, ex));
        }
    }
}
#endregion
