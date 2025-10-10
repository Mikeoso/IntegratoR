using FluentValidation;
using IntegratoR.Abstractions.Common.Results;
using IntegratoR.Abstractions.Interfaces.Results;
using MediatR;

namespace IntegratoR.Application.Common.Behaviours;

// FILE-LEVEL DOCUMENTATION
// ---------------------------------------------------------------------------------------------
// <remarks>
// This file defines the validation pipeline behavior, acting as the application's first line of
// defense. By integrating FluentValidation directly into the MediatR pipeline, we ensure that
// no command or query with invalid data ever reaches the core business logic. This is crucial
// for maintaining data integrity and system security, especially for endpoints exposed via
// Azure Functions.
// </remarks>
// ---------------------------------------------------------------------------------------------

/// <summary>
/// A MediatR pipeline behavior that enforces a fail-fast validation strategy. It intercepts
/// incoming requests and runs all associated FluentValidation validators before the request
/// reaches its handler.
/// </summary>
/// <typeparam name="TRequest">The type of the MediatR request being validated.</typeparam>
/// <typeparam name="TResponse">The type of the response, which must implement <see cref="IResult"/>.</typeparam>
/// <remarks>
/// This behavior decouples validation logic from business logic. Command and query handlers can
/// operate under the assumption that the request data they receive is valid, adhering to the
/// Single Responsibility Principle.
///
/// To use this, a developer simply creates a class that inherits from <c>AbstractValidator&lt;TRequest&gt;</c>
/// for any command or query. The dependency injection container will automatically discover and
/// inject the validator(s), and this behavior will execute them. If validation fails, the pipeline
/// is short-circuited, saving system resources.
/// </remarks>
public class ValidationBehaviour<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
    where TResponse : IResult
{
    private readonly IEnumerable<IValidator<TRequest>> _validators;

    /// <summary>
    /// Initializes a new instance of the <see cref="ValidationBehaviour{TRequest, TResponse}"/> class.
    /// </summary>
    /// <param name="validators">
    /// An enumeration of validators for the request type <typeparamref name="TRequest"/>.
    /// This is provided by the dependency injection container, which collects all registered validators for the request.
    /// </param>
    public ValidationBehaviour(IEnumerable<IValidator<TRequest>> validators)
    {
        _validators = validators;
    }

    /// <summary>
    /// Intercepts and handles an incoming request, performing validation and either proceeding
    /// to the next handler or returning a validation error immediately.
    /// </summary>
    /// <param name="request">The incoming request object to be validated.</param>
    /// <param name="next">A delegate representing the next action in the pipeline.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>
    /// A task whose result is either the response from the next handler in the pipeline (on success)
    /// or a failure <see cref="IResult"/> containing the first validation error found.
    /// </returns>
    /// <remarks>
    /// This handler's logic follows a clear flow:
    /// 1. If no validators are registered for the request, it passes through immediately.
    /// 2. It runs all registered validators and collects any validation failures.
    /// 3. If failures exist, it short-circuits the pipeline and returns a standardized validation error.
    /// 4. If validation succeeds, it calls the next delegate in the pipeline.
    ///
    /// The complex reflection block for handling failures is necessary to dynamically create the correct
    /// type of failed <see cref="IResult"/>, as <typeparamref name="TResponse"/> can be either the generic
    /// <see cref="Result{TValue}"/> or the non-generic <see cref="Result"/>. This allows the behavior
    /// to be universally applied to any command or query.
    /// </remarks>
    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        // If no validators are registered for this request type, skip validation.
        if (!_validators.Any())
        {
            return await next();
        }

        var context = new ValidationContext<TRequest>(request);

        // Execute all validators and aggregate their failures.
        var validationFailures = _validators
            .Select(validator => validator.Validate(context))
            .SelectMany(validationResult => validationResult.Errors)
            .Where(validationFailure => validationFailure is not null)
            .ToList();

        if (validationFailures.Any())
        {
            // By default, we return the first validation error. This simplifies client error handling.
            var firstFailure = validationFailures.First();
            var error = new Error("Validation.Error", firstFailure.ErrorMessage, ErrorType.Validation);

            // Dynamically create the correct type of failed Result (generic or non-generic).
            var resultType = typeof(TResponse);
            if (resultType.IsGenericType && resultType.GetGenericTypeDefinition() == typeof(Result<>))
            {
                var genericType = resultType.GetGenericArguments()[0];
                var failMethod = typeof(Result<>).MakeGenericType(genericType)
                  .GetMethod(nameof(Result<object>.Fail), new[] { typeof(Error) });

                return (TResponse)failMethod!.Invoke(null, new object[] { error })!;
            }
            else
            {
                // This handles the non-generic Result case for commands that don't return a value.
                return (TResponse)(object)Result.Fail(error);
            }
        }

        // If validation was successful, proceed to the next behavior or the handler.
        return await next();
    }
}