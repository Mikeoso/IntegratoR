using FluentResults;

namespace IntegratoR.Abstractions.Common.Results;

/// <summary>
/// Provides ergonomic access to <see cref="IntegrationError"/> on FluentResults results.
/// </summary>
public static class ResultExtensions
{
    /// <summary>
    /// Gets the first <see cref="IntegrationError"/> from the result's error list, or
    /// <see langword="null"/> if the result carries none.
    /// </summary>
    public static IntegrationError? GetError(this IResultBase result)
    {
        return result.Errors.OfType<IntegrationError>().FirstOrDefault();
    }

    /// <summary>
    /// Pattern-matches on a <see cref="Result{T}"/>, invoking <paramref name="onSuccess"/> with the
    /// value when successful, or <paramref name="onFailure"/> with the first
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
    /// Pattern-matches on a <see cref="Result"/>, invoking <paramref name="onSuccess"/> when
    /// successful, or <paramref name="onFailure"/> with the first <see cref="IntegrationError"/>
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
