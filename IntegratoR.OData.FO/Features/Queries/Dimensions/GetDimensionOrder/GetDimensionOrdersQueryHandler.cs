using FluentResults;
using IntegratoR.Abstractions.Common.Results;
using IntegratoR.Abstractions.Interfaces.Services;
using IntegratoR.OData.FO.Common.Extensions;
using IntegratoR.OData.FO.Domain.Entities.Dimensions;
using IntegratoR.OData.FO.Domain.Enums.Dimensions;
using IntegratoR.OData.FO.Domain.Enums.General;
using IntegratoR.OData.FO.Domain.Models.FinancialDimensions;
using IntegratoR.OData.Interfaces.Services;
using MediatR;
using Microsoft.Extensions.Logging;

namespace IntegratoR.OData.FO.Features.Queries.Dimensions.GetDimensionOrder;

/// <summary>
/// Handles <see cref="GetDimensionOrdersQuery"/> by resolving the active D365 F&amp;O dimension
/// format that matches the requested format name and hierarchy type, then returning its ordered
/// segments and delimiter.
/// </summary>
public class GetDimensionOrdersQueryHandler : IRequestHandler<GetDimensionOrdersQuery, Result<DimensionFormat>>
{
    private readonly ILogger<GetDimensionOrdersQueryHandler> _logger;
    private readonly IODataService<DimensionParameters> _dimensionParametersService;
    private readonly IService<DimensionIntegrationFormat> _dimensionIntegrationFormatService;

    /// <summary>
    /// Initializes a new instance of the <see cref="GetDimensionOrdersQueryHandler"/> class.
    /// </summary>
    public GetDimensionOrdersQueryHandler(ILogger<GetDimensionOrdersQueryHandler> logger, IODataService<DimensionParameters> dimensionParametersService, IService<DimensionIntegrationFormat> dimensionIntegrationFormatService)
    {
        _logger = logger;
        _dimensionParametersService = dimensionParametersService;
        _dimensionIntegrationFormatService = dimensionIntegrationFormatService;
    }

    /// <inheritdoc/>
    /// <param name="request">The query specifying the dimension format name and hierarchy type.</param>
    /// <param name="cancellationToken">A token that cancels the outbound OData requests.</param>
    /// <returns>
    /// A successful <see cref="Result{T}"/> containing the resolved <see cref="DimensionFormat"/>;
    /// a failed result with <c>ErrorType.NotFound</c> when the singleton parameter row is missing,
    /// or the underlying errors when a lookup fails.
    /// </returns>
    public async Task<Result<DimensionFormat>> Handle(GetDimensionOrdersQuery request, CancellationToken cancellationToken)
    {
        var dimensionFormatName = request.DimensionFormat;
        var dimensionHierarchyType = request.HierarchyType;

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

        var dimensionParameters = await _dimensionParametersService.FindAllAsync(cancellationToken).ConfigureAwait(false);

        if (dimensionParameters.IsFailed)
        {
            // Same rationale as above: surface the real underlying error from the
            // DimensionParameters service instead of rewriting it.
            return Result.Fail<DimensionFormat>(dimensionParameters.Errors);
        }

        // Guard against an empty DimensionParameters response. D365 stores this as a
        // singleton-row entity so this is unexpected in practice, but if FindAll returns an
        // empty collection (e.g. the entity set exists but the row was never seeded) the
        // delimiter would be null and DimensionSegmentDelimiterExtensions.GetCharValue throws
        // ArgumentOutOfRangeException on the default arm. Failing explicitly with NotFound
        // gives the caller an actionable diagnostic instead of an obscure exception.
        var dimensionParametersRecord = dimensionParameters.Value?.FirstOrDefault();
        if (dimensionParametersRecord is null)
        {
            return Result.Fail<DimensionFormat>(new IntegrationError(
                "DimensionParameters.NotFound",
                "DimensionParameters returned no records — the singleton parameter row is missing in this environment.",
                ErrorType.NotFound));
        }

        DimensionSegmentDelimiter? dimensionDelimiter = dimensionParametersRecord.DimensionSegmentDelimiter;
        var dimensionOrder = financialDimensionFormat?.FinancialDimensionFormat?.Split(dimensionDelimiter.GetCharValue()).ToList();

        var dimensionFormat = new DimensionFormat
        {
            Delimiter = dimensionDelimiter.GetCharValue().ToString(),
            Segments = dimensionOrder ?? new List<string>()
        };

        return Result.Ok(dimensionFormat);
    }
}
