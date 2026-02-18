using FluentResults;

namespace IntegratoR.Abstractions.Common.Results;

/// <summary>
/// Extension methods that bridge API gaps between the original custom Result pattern
/// and FluentResults, providing ergonomic access to <see cref="IntegrationError"/>.
/// </summary>
public static class ResultExtensions
{
    /// <summary>
    /// Returns the first <see cref="IntegrationError"/> from the result's error list,
    /// or <c>null</c> if the result has no <see cref="IntegrationError"/>.
    /// Replaces the former <c>result.Error</c> singular accessor.
    /// </summary>
    public static IntegrationError? GetError(this IResultBase result)
    {
        return result.Errors.OfType<IntegrationError>().FirstOrDefault();
    }

    /// <summary>
    /// Pattern-matches on a generic <see cref="Result{T}"/>, invoking <paramref name="onSuccess"/>
    /// with the value when successful, or <paramref name="onFailure"/> with the first
    /// <see cref="IntegrationError"/> when failed.
    /// </summary>
    public static TOut Match<T, TOut>(
        this Result<T> result,
        Func<T, TOut> onSuccess,
        Func<IntegrationError, TOut> onFailure)
    {
        if (result.IsSuccess)
            return onSuccess(result.Value);

        var error = result.GetError()
            ?? new IntegrationError("Unknown", result.Errors.FirstOrDefault()?.Message ?? "Unknown error", ErrorType.Failure);

        return onFailure(error);
    }

    /// <summary>
    /// Pattern-matches on a non-generic <see cref="Result"/>, invoking <paramref name="onSuccess"/>
    /// when successful, or <paramref name="onFailure"/> with the first <see cref="IntegrationError"/>
    /// when failed.
    /// </summary>
    public static TOut Match<TOut>(
        this Result result,
        Func<TOut> onSuccess,
        Func<IntegrationError, TOut> onFailure)
    {
        if (result.IsSuccess)
            return onSuccess();

        var error = result.GetError()
            ?? new IntegrationError("Unknown", result.Errors.FirstOrDefault()?.Message ?? "Unknown error", ErrorType.Failure);

        return onFailure(error);
    }
}
