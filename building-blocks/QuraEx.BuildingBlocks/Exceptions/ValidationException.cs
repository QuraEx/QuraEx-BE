using FluentValidation.Results;

namespace QuraEx.BuildingBlocks.Exceptions;

public sealed class ValidationException(IEnumerable<ValidationFailure> failures)
    : Exception("One or more validation failures occurred.")
{
    public IReadOnlyList<ValidationFailure> Failures { get; } = failures.ToList().AsReadOnly();
}
