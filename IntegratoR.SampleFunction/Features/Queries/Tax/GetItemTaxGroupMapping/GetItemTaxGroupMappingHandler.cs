using IntegratoR.Abstractions.Common.Results;
using IntegratoR.Abstractions.Interfaces.Services;
using IntegratoR.SampleFunction.Domain.Entities.Tax;
using IntegratoR.SampleFunction.Features.Queries.Tax.GetItemTaxGroupMapping;
using MediatR;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IntegratoR.SampleFunction.Features.Queries.Tax.GetItemTaxGroupMapping
{
    public class GetItemTaxGroupMappingHandler : IRequestHandler<GetItemTaxGroupMappingQuery, Result<ItemTaxGroupMapping>>
    {
        private readonly ILogger<GetItemTaxGroupMappingHandler> _logger;
        private readonly IService<ItemTaxGroupMapping> _itemTaxGroupMappingService;
        public GetItemTaxGroupMappingHandler(ILogger<GetItemTaxGroupMappingHandler> logger, IService<ItemTaxGroupMapping> itemTaxGroupMappingService)
        {
            _logger = logger;
            _itemTaxGroupMappingService = itemTaxGroupMappingService;
        }

        public async Task<Result<ItemTaxGroupMapping>> Handle(GetItemTaxGroupMappingQuery request, CancellationToken cancellationToken)
        {
            _logger.LogInformation("Querying ItemTaxGroupMapping for BusinessBookingGroup: {BusinessBookingGroup}, ProductBookingGroup: {ProductBookingGroup}",
                request.ItemTaxBusinessBookingGroup,
                request.ItemTaxProductBookingGroup);

            var result = await _itemTaxGroupMappingService.FindAsync(
                x => x.RelionTaxBusinessGroup == request.ItemTaxBusinessBookingGroup &&
                     x.RelionTaxProductGroup == request.ItemTaxProductBookingGroup,
                cancellationToken);

            if (result.IsFailure)
            {
                return Result<ItemTaxGroupMapping>.Fail(result);
            }

            var itemTaxGroupMapping = result?.Value?.FirstOrDefault();

            return Result<ItemTaxGroupMapping>.Ok(itemTaxGroupMapping!);
        }
    }
}
