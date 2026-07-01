using FluentResults;
using FluentValidation;
using FluentValidation.Results;
using IntegratoR.Abstractions.Common.Results;
using MediatR;

namespace IntegratoR.Application.Common.Behaviours;

/// <summary>
/// Provides a MediatR pipeline behaviour that runs all registered FluentValidation validators before a request reaches its handler.
/// </summary>
/// <typeparam name="TRequest">The type of the MediatR request being validated.</typeparam>
/// <typeparam name="TResponse">The type of the response from the request handler.</typeparam>
/// <remarks>Validators registered as <c>AbstractValidator&lt;TRequest&gt;</c> are discovered from the DI container; on failure the pipeline is short-circuited with a validation error.</remarks>
public class ValidationBehaviour<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
    where TResponse : IResultBase
{
    private readonly IEnumerable<IValidator<TRequest>> _validators;

    /// <summary>
    /// Initializes a new instance of the <see cref="ValidationBehaviour{TRequest, TResponse}"/> class.
    /// </summary>
    /// <param name="validators">The validators registered for <typeparamref name="TRequest"/>, supplied by the DI container.</param>
    public ValidationBehaviour(IEnumerable<IValidator<TRequest>> validators)
    {
        _validators = validators;
    }

    /// <summary>
    /// Asynchronously validates the request, then either forwards it or short-circuits with a validation error.
    /// </summary>
    /// <param name="request">The incoming request to validate.</param>
    /// <param name="next">A delegate that invokes the next behaviour or the request handler.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>The response from the next handler when validation succeeds; otherwise a failed result carrying the first validation error with code <c>Validation.Error</c>.</returns>
    /// <remarks>Failed results are built via <see cref="ResultFactory.FailFromError{TResult}"/> so the correct closed type (<see cref="Result{TValue}"/> or non-generic <see cref="Result"/>) is returned for any command or query.</remarks>
    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        if (!_validators.Any())
        {
            return await next().ConfigureAwait(false);
        }

        var context = new ValidationContext<TRequest>(request);

        // Execute all validators sequentially (so async FluentValidation rules run) and aggregate
        // their failures, forwarding the CancellationToken to each validator.
        var validationFailures = new List<ValidationFailure>();
        foreach (var validator in _validators)
        {
            var validationResult = await validator.ValidateAsync(context, cancellationToken).ConfigureAwait(false);
            validationFailures.AddRange(validationResult.Errors.Where(validationFailure => validationFailure is not null));
        }

        if (validationFailures.Any())
        {
            // By default, we return the first validation error. This simplifies client error handling.
            var firstFailure = validationFailures.First();
            var error = new IntegrationError("Validation.Error", firstFailure.ErrorMessage, ErrorType.Validation);

            return ResultFactory.FailFromError<TResponse>(error);
        }

        return await next().ConfigureAwait(false);
    }
}
