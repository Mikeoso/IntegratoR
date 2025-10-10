using IntegratoR.Abstractions.Common.Results;
using IntegratoR.Abstractions.Interfaces.Results;
using IntegratoR.Abstractions.Interfaces.Services;
using IntegratoR.OData.FO.Common.Extensions;
using IntegratoR.OData.FO.Domain.Entities.Dimensions;
using IntegratoR.OData.FO.Domain.Enums.General;
using IntegratoR.OData.FO.Domain.Models.FinancialDimensions;
using IntegratoR.OData.Interfaces.Services;
using MediatR;
using Microsoft.Extensions.Logging;

namespace IntegratoR.OData.FO.Features.Queries.Dimensions.GetDimensionOrder;

public class GetDimensionOrdersQueryHandler : IRequestHandler<GetDimensionOrdersQuery, IResult<DimensionFormat>>
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

    public async Task<IResult<DimensionFormat>> Handle(GetDimensionOrdersQuery request, CancellationToken cancellationToken)
    {
        var dimensionFormatName = request.dimensionFormat;
        var dimensionHierarchyType = request.hierarchyType;

        _logger.LogInformation("Fetching dimension format '{DimensionFormatName}' of type '{DimensionHierarchyType}' from F&O.", dimensionFormatName, dimensionHierarchyType);

        var dimensionFormats = await _dimensionIntegrationFormatService.FindAsync(
            x => x.DimensionFormatName == dimensionFormatName &&
            x.DimensionFormatType == dimensionHierarchyType &&
            x.IsActive == NoYes.Yes, cancellationToken);

        if (dimensionFormats.IsFailure)
        {
            return Result<DimensionFormat>.Fail(new Error(
                $"DimensionParameters.QueryFailed",
                $"No Data returned by the query",
                ErrorType.Failure));
        }
        var financialDimensionFormat = dimensionFormats.Value?.FirstOrDefault();

        var dimensionParameters = await _dimensionParametersService.FindAll(cancellationToken);

        if (dimensionParameters.IsFailure)
        {
            return Result<DimensionFormat>.Fail(new Error(
                $"DimensionParameters.QueryFailed",
                $"No Data returned by the query",
                ErrorType.Failure));
        }

        var dimensionDelimiter = dimensionParameters.Value?.FirstOrDefault()?.DimensionSegmentDelimiter;
        var dimensionOrder = dimensionFormats.Value?.FirstOrDefault()?.FinancialDimensionFormat?.Split(dimensionDelimiter.GetCharValue()).ToList();

        var dimensionFormat = new DimensionFormat
        {
            Delimiter = dimensionDelimiter.GetCharValue().ToString(),
            Segments = dimensionOrder ?? new List<string>()
        };

        return Result<DimensionFormat>.Ok(dimensionFormat);
    }
}
