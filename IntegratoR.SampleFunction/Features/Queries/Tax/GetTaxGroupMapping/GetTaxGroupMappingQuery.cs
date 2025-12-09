using IntegratoR.Abstractions.Common.Results;
using IntegratoR.Abstractions.Interfaces.Queries;
using IntegratoR.SampleFunction.Domain.Entities.Tax;
using IntegratoR.SampleFunction.Domain.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IntegratoR.SampleFunction.Features.Queries.Tax.GetTaxGroupMapping
{
    public record GetTaxGroupMappingQuery(RelionBookingType BookingType, string BookingGroup) : ICacheableQuery<Result<TaxGroupMapping>>
    {
        public string CacheKey => GenerateCacheKey();

        public TimeSpan? CacheDuration => TimeSpan.FromMinutes(30);

        public string GenerateCacheKey()
        {
            return $"{nameof(GetTaxGroupMappingQuery)}-{BookingType}-{BookingGroup}";
        }

        public object[] GetCacheKeyValues()
        {
            return new object[] { nameof(GetTaxGroupMappingQuery), BookingType, BookingGroup };
        }

        public IReadOnlyDictionary<string, object> GetLoggingContext()
        {
            return new Dictionary<string, object>
            {
                { "BookingType", BookingType },
                { "BookingGroup", BookingGroup }
            };
        }
    }
}
