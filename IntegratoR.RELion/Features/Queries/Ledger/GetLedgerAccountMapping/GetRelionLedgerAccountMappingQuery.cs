using IntegratoR.Abstractions.Common.Results;
using IntegratoR.Abstractions.Interfaces.Queries;
using IntegratoR.RELion.Domain.Models;

namespace IntegratoR.RELion.Features.Queries.Ledger.GetLedgerAccountMapping
{
    public record GetRelionLedgerAccountMappingQuery(int EntryNo) : ICacheableQuery<Result<RelionLedgerAccountMapping>>
    {
        public string CacheKey => GenerateCacheKey();

        public TimeSpan? CacheDuration => TimeSpan.FromMinutes(30);

        public string GenerateCacheKey()
        {
            return $"{EntryNo}";
        }

        public object[] GetCacheKeyValues()
        {
            return new object[] { EntryNo };
        }
    }
}
