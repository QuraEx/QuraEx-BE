using FluentValidation;
using MediatR;
using Microsoft.Extensions.Logging;

namespace QuraEx.BuildingBlocks.Behaviors;

/// <summary>Runs all FluentValidation validators for the request before the handler.
/// Returns a Failure result instead of throwing when using Result-returning handlers.</summary>
public sealed class ValidationBehavior<TRequest, TResponse>(
    IEnumerable<IValidator<TRequest>> validators,
    ILogger<ValidationBehavior<TRequest, TResponse>> logger)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        if (!validators.Any())
        {
            return await next();
        }

        var context = new ValidationContext<TRequest>(request);
        var failures = validators
            .Select(v => v.Validate(context))
            .SelectMany(r => r.Errors)
            .Where(f => f is not null)
            .ToList();

        if (failures.Count == 0)
        {
            return await next();
        }

        logger.LogWarning(
            "Validation failed for {RequestType}: {Errors}",
            typeof(TRequest).Name,
            string.Join("; ", failures.Select(f => $"{f.PropertyName}: {f.ErrorMessage}")));

        throw new Exceptions.ValidationException(failures);
    }
}
