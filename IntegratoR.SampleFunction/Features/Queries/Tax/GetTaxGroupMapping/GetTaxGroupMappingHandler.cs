using IntegratoR.Abstractions.Common.Results;
using IntegratoR.Abstractions.Interfaces.Services;
using IntegratoR.SampleFunction.Domain.Entities.Tax;
using IntegratoR.SampleFunction.Features.Queries.Tax.GetTaxGroupMapping;
using MediatR;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IntegratoR.SampleFunction.Features.Queries.Tax.GetTaxGroupMapping
{
    public class GetTaxGroupMappingHandler : IRequestHandler<GetTaxGroupMappingQuery, Result<TaxGroupMapping>>
    {
        private readonly ILogger<GetTaxGroupMappingHandler> _logger;
        private readonly IService<TaxGroupMapping> _taxGroupMappingService;

        public GetTaxGroupMappingHandler(ILogger<GetTaxGroupMappingHandler> logger, IService<TaxGroupMapping> taxGroupMappingService)
        {
            _logger = logger;
            _taxGroupMappingService = taxGroupMappingService;
        }

        public async Task<Result<TaxGroupMapping>> Handle(GetTaxGroupMappingQuery request, CancellationToken cancellationToken)
        {
            _logger.LogInformation("Querying TaxGroupMapping for BookingType: {BookingType}, BookingGroup: {BookingGroup}",
                request.BookingType,
                request.BookingGroup);

            var result = await _taxGroupMappingService.FindAsync(
                x => x.RelionBookingType == request.BookingType &&
                     x.RelionBusinessGroup == request.BookingGroup,
                cancellationToken);

            if (result.IsFailure)
            {
                return Result<TaxGroupMapping>.Fail(result);
            }

            var taxGroupMapping = result?.Value?.FirstOrDefault();
            return Result<TaxGroupMapping>.Ok(taxGroupMapping!);
        }
    }
}
