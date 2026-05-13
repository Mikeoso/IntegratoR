using FluentResults;
using IntegratoR.Abstractions.Common.Results;
using IntegratoR.Abstractions.Interfaces.Services;
using IntegratoR.OData.FO.Common.Extensions;
using IntegratoR.OData.FO.Domain.Entities.Dimensions;
using IntegratoR.OData.FO.Domain.Enums.General;
using IntegratoR.OData.FO.Domain.Models.FinancialDimensions;
using IntegratoR.OData.Interfaces.Services;
using MediatR;
using Microsoft.Extensions.Logging;

namespace IntegratoR.OData.FO.Features.Queries.Dimensions.GetDimensionOrder;

public class GetDimensionOrdersQueryHandler : IRequestHandler<GetDimensionOrdersQuery, Result<DimensionFormat>>
{
    private readonly ILogger<GetDimensionOrdersQueryHandler> _logger;
    private readonly IODataService<DimensionParameters> _dimensionParametersService;
    private readonly IService<DimensionIntegrationFormat> _dimensionIntegrationFormatService;

    public GetDimensionOrdersQueryHandler(ILogger<GetDimensionOrdersQueryHandler> logger, IODataService<DimensionParameters> dimensionParametersService, IService<DimensionIntegrationFormat> dimensionIntegrationFormatService)
    {
        _logger = logger;
        _dimensionParametersService = dimensionParametersService;
        _dimensionIntegrationFormatService = dimensionIntegrationFormatService;
    }

    public async Task<Result<DimensionFormat>> Handle(GetDimensionOrdersQuery request, CancellationToken cancellationToken)
    {
        var dimensionFormatName = request.dimensionFormat;
        var dimensionHierarchyType = request.hierarchyType;

        _logger.LogInformation("Fetching dimension format '{DimensionFormatName}' of type '{DimensionHierarchyType}' from F&O.", dimensionFormatName, dimensionHierarchyType);

        var dimensionFormats = await _dimensionIntegrationFormatService.FindAsync(
            x => x.DimensionFormatName == dimensionFormatName &&
            x.DimensionFormatType == dimensionHierarchyType &&
            x.IsActive == NoYes.Yes, cancellationToken).ConfigureAwait(false);

        if (dimensionFormats.IsFailed)
        {
            // Propagate the underlying errors verbatim so the consumer sees the real cause
            // (e.g. entity set not found, authentication failure, APIM rejection) instead of
            // a generic "No Data returned by the query" that hides the diagnostics.
            return Result.Fail<DimensionFormat>(dimensionFormats.Errors);
        }
        var financialDimensionFormat = dimensionFormats.Value?.FirstOrDefault();

        var dimensionParameters = await _dimensionParametersService.FindAll(cancellationToken).ConfigureAwait(false);

        if (dimensionParameters.IsFailed)
        {
            // Same rationale as above: surface the real underlying error from the
            // DimensionParameters service instead of rewriting it.
            return Result.Fail<DimensionFormat>(dimensionParameters.Errors);
        }

        var dimensionDelimiter = dimensionParameters.Value?.FirstOrDefault()?.DimensionSegmentDelimiter;
        var dimensionOrder = dimensionFormats.Value?.FirstOrDefault()?.FinancialDimensionFormat?.Split(dimensionDelimiter.GetCharValue()).ToList();

        var dimensionFormat = new DimensionFormat
        {
            Delimiter = dimensionDelimiter.GetCharValue().ToString(),
            Segments = dimensionOrder ?? new List<string>()
        };

        return Result.Ok(dimensionFormat);
    }
}
