using System.Collections.Concurrent;
using System.Reflection;
using FluentResults;

namespace IntegratoR.Abstractions.Common.Results;

/// <summary>
/// Creates failed <see cref="Result"/> and <see cref="Result{TValue}"/> instances from an
/// <see cref="IError"/> when the concrete result type is known only through a generic type parameter.
/// </summary>
/// <remarks>
/// The per-type <see cref="MethodInfo"/> for <c>Result.Fail&lt;T&gt;(IError)</c> is cached so the
/// reflection lookup happens at most once per closed result type.
/// </remarks>
public static class ResultFactory
{
    private static readonly ConcurrentDictionary<Type, MethodInfo> FailSingleErrorCache = new();

    /// <summary>
    /// Builds a failed <typeparamref name="TResult"/> carrying <paramref name="error"/>.
    /// </summary>
    /// <typeparam name="TResult">The closed result type to produce.</typeparam>
    /// <param name="error">The error the failed result carries.</param>
    /// <returns>A failed <typeparamref name="TResult"/> carrying <paramref name="error"/>.</returns>
    /// <exception cref="NotSupportedException">
    /// Thrown when <typeparamref name="TResult"/> is neither <see cref="Result"/> nor
    /// <see cref="Result{TValue}"/>.
    /// </exception>
    public static TResult FailFromError<TResult>(IError error) where TResult : IResultBase
    {
        Type resultType = typeof(TResult);
        if (resultType.IsGenericType && resultType.GetGenericTypeDefinition() == typeof(Result<>))
        {
            MethodInfo failMethod = FailSingleErrorCache.GetOrAdd(
                resultType.GetGenericArguments()[0],
                static valueType => typeof(Result)
                    .GetMethods(BindingFlags.Public | BindingFlags.Static)
                    .First(m => m.Name == nameof(Result.Fail)
                        && m.IsGenericMethod
                        && m.GetParameters().Length == 1
                        && m.GetParameters()[0].ParameterType == typeof(IError))
                    .MakeGenericMethod(valueType));

            return (TResult)failMethod.Invoke(null, [error])!;
        }

        if (resultType == typeof(Result))
        {
            return (TResult)(object)Result.Fail(error);
        }

        throw new NotSupportedException(
            $"ResultFactory.FailFromError supports Result and Result<T> only; '{resultType.FullName}' is not supported.");
    }
}
