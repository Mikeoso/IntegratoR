using IntegratoR.Abstractions.Common.Results;
using IntegratoR.Abstractions.Interfaces.Queries;
using IntegratoR.SampleFunction.Domain.Entities.Tax;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IntegratoR.SampleFunction.Features.Queries.Tax.GetItemTaxGroupMapping
{
    public record GetItemTaxGroupMappingQuery(string ItemTaxBusinessBookingGroup, string ItemTaxProductBookingGroup) : ICacheableQuery<Result<ItemTaxGroupMapping>>
    {
        public string CacheKey => GenerateCacheKey();

        public TimeSpan? CacheDuration => TimeSpan.FromMinutes(30);

        public string GenerateCacheKey()
        {
            return $"{nameof(GetItemTaxGroupMapping)}-{ItemTaxBusinessBookingGroup}-{ItemTaxProductBookingGroup}";
        }

        public object[] GetCacheKeyValues()
        {
            return new object[] { nameof(GetItemTaxGroupMapping), ItemTaxBusinessBookingGroup, ItemTaxProductBookingGroup };
        }

        public IReadOnlyDictionary<string, object> GetLoggingContext()
        {
            return new Dictionary<string, object>
            {
                { "ItemTaxBusinessBookingGroup", ItemTaxBusinessBookingGroup },
                { "ItemTaxProductBookingGroup", ItemTaxProductBookingGroup }
            };
        }
    }
}
