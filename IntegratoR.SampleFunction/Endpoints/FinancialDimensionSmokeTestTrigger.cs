using System.Net;
using FluentResults;
using IntegratoR.Abstractions.Common.Results;
using IntegratoR.OData.FO.Domain.Enums.Dimensions;
using IntegratoR.OData.FO.Domain.Models.FinancialDimensions;
using IntegratoR.OData.FO.Features.Queries.Dimensions.GetDimensionOrder;
using IntegratoR.SampleFunction.Domain.DTOs.SmokeTest;
using MediatR;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;

namespace IntegratoR.SampleFunction.Endpoints;

/// <summary>
/// Smoke test for the <c>GetDimensionOrdersQuery</c> MediatR handler. Exercises the
/// two underlying D365 F&amp;O read paths (<c>DimensionIntegrationFormat</c> composite-key
/// filter + <c>DimensionParameters</c> find-all) against a live sandbox so consumers can
/// confirm the custom query handler, generic OData services, and the
/// <c>[JsonPropertyName]</c>-aware filter translator all cooperate on a read surface that
/// does NOT depend on a company context.
/// </summary>
/// <remarks>
/// <para>
/// POST a <see cref="FinancialDimensionSmokeTestRequest"/> body with the
/// <c>DimensionFormatName</c> and <c>DimensionHierarchyType</c> that exist in the target
/// sandbox. The trigger invokes <c>GetDimensionOrdersQuery</c>, unpacks the
/// <see cref="DimensionFormat"/> result, and returns the delimiter and ordered segments
/// it parsed out of the D365 response.
/// </para>
/// <para>
/// Read-only by design: this trigger never writes to the sandbox, so it has no cleanup
/// steps and is safe to run repeatedly without leaving orphan records.
/// </para>
/// </remarks>
public sealed class FinancialDimensionSmokeTestTrigger
{
    private readonly IMediator _mediator;
    private readonly ILogger<FinancialDimensionSmokeTestTrigger> _logger;

    public FinancialDimensionSmokeTestTrigger(
        IMediator mediator,
        ILogger<FinancialDimensionSmokeTestTrigger> logger)
    {
        _mediator = mediator;
        _logger = logger;
    }

    [Function("FinancialDimensionSmokeTest_HTTPTrigger")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "smoke/financial-dimensions")] HttpRequestData req,
        CancellationToken cancellationToken)
    {
        FinancialDimensionSmokeTestRequest? input;
        try
        {
            input = await req.ReadFromJsonAsync<FinancialDimensionSmokeTestRequest>(cancellationToken).ConfigureAwait(false);
        }
        catch (System.Text.Json.JsonException ex)
        {
            return await WriteResponse(req, HttpStatusCode.BadRequest, new FinancialDimensionSmokeTestResponse(
                Success: false,
                Delimiter: null,
                Segments: null,
                Steps: [new LedgerJournalSmokeTestStep("ParseRequest", false, "SmokeTest.InvalidJson", ErrorType.Validation.ToString(), ex.Message)]),
                cancellationToken).ConfigureAwait(false);
        }

        if (input is null || string.IsNullOrWhiteSpace(input.DimensionFormatName))
        {
            return await WriteResponse(req, HttpStatusCode.BadRequest, new FinancialDimensionSmokeTestResponse(
                Success: false,
                Delimiter: null,
                Segments: null,
                Steps: [new LedgerJournalSmokeTestStep("ParseRequest", false, "SmokeTest.MissingFields", ErrorType.Validation.ToString(), "DimensionFormatName is required.")]),
                cancellationToken).ConfigureAwait(false);
        }

        var steps = new List<LedgerJournalSmokeTestStep>();

        _logger.LogInformation(
            "FinancialDimension smoke test starting for format '{DimensionFormatName}' of type '{HierarchyType}'.",
            input.DimensionFormatName, input.HierarchyType);

        // ---------------------------------------------------------------------------------
        // 1. GetDimensionOrdersQuery — chains DimensionIntegrationFormat find + DimensionParameters find
        // ---------------------------------------------------------------------------------
        var queryResult = await _mediator
            .Send(new GetDimensionOrdersQuery(input.DimensionFormatName, input.HierarchyType), cancellationToken)
            .ConfigureAwait(false);

        steps.Add(BuildStep("GetDimensionOrders", queryResult,
            onSuccess: r => $"Delimiter='{r.Delimiter}', Segments=[{string.Join(", ", r.Segments)}]"));

        string? delimiter = null;
        IReadOnlyList<string>? segments = null;
        if (queryResult.IsSuccess)
        {
            delimiter = queryResult.Value.Delimiter;
            segments = queryResult.Value.Segments;
        }

        var success = steps.All(s => s.Success);
        _logger.LogInformation(
            "FinancialDimension smoke test finished. Success={Success}, Steps={StepCount}",
            success, steps.Count);

        return await WriteResponse(req, HttpStatusCode.OK,
            new FinancialDimensionSmokeTestResponse(success, delimiter, segments, steps), cancellationToken)
            .ConfigureAwait(false);
    }

    private static LedgerJournalSmokeTestStep BuildStep<T>(
        string name,
        Result<T> result,
        Func<T, string>? onSuccess = null)
    {
        if (result.IsSuccess)
        {
            return new LedgerJournalSmokeTestStep(
                name,
                Success: true,
                Details: onSuccess?.Invoke(result.Value));
        }

        var error = result.GetError();
        return new LedgerJournalSmokeTestStep(
            name,
            Success: false,
            ErrorCode: error?.Code,
            ErrorType: error?.Type.ToString(),
            ErrorMessage: error?.Message);
    }

    private static async Task<HttpResponseData> WriteResponse(
        HttpRequestData req,
        HttpStatusCode status,
        FinancialDimensionSmokeTestResponse body,
        CancellationToken cancellationToken)
    {
        var response = req.CreateResponse(status);
        await response.WriteAsJsonAsync(body, cancellationToken).ConfigureAwait(false);
        return response;
    }
}
