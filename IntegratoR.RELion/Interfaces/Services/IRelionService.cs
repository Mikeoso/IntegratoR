using IntegratoR.Abstractions.Common.Results;
using IntegratoR.RELion.Domain.Models;

namespace IntegratoR.RELion.Interfaces.Services;

/// <summary>
/// Defines a contract for fetching data specifically from the Relion service.
/// </summary>
public interface IRelionService
{
    /// <summary>
    /// Fetches new journal lines from Relion that have been created or modified since a specific timestamp.
    /// </summary>
    /// <param name="since">The timestamp to fetch records from.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A Result containing a list of journal lines on success, or an error on failure.</returns>
    Task<Result<List<RelionLedgerJournalLine>>> GetNewJournalLinesAsync(DateTime since, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves all ledger account mappings from Relion.
    /// </summary>
    Task<Result<RelionLedgerAccountMapping>> GetLedgerAccountMappingsAsync(int entryNo, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves details for a specific company by its name.
    /// </summary>
    Task<Result<RelionCompany>> GetCompanyByNameAsync(string companyName, CancellationToken cancellationToken = default);
}
