using IntegratoR.Abstractions.Common.Results;
using IntegratoR.Abstractions.Interfaces.Queries;
using IntegratoR.SampleFunction.Domain.Entities.Ledger;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IntegratoR.SampleFunction.Features.Queries.Ledger.GetLedgerAccountMapping
{
    public record GetLedgerAccountMappingQuery(string LedgerAccount, string IFRS = "") : ICacheableQuery<Result<LedgerAccountMapping>>
    {
        public string CacheKey => GenerateCacheKey();

        public TimeSpan? CacheDuration => TimeSpan.FromMinutes(30);

        public string GenerateCacheKey()
        {
            return $"{nameof(GetLedgerAccountMappingQuery)}-{LedgerAccount}-{IFRS}";
        }

        public object[] GetCacheKeyValues()
        {
            return new object[] { nameof(GetLedgerAccountMappingQuery), LedgerAccount, IFRS };
        }

        public IReadOnlyDictionary<string, object> GetLoggingContext()
        {
            return new Dictionary<string, object>
            {
                { "LedgerAccount", LedgerAccount },
                { "IFRS", IFRS }
            };
        }
    }
}
