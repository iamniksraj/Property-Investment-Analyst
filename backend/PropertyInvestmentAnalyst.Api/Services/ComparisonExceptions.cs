namespace PropertyInvestmentAnalyst.Api.Services;

public sealed class ComparisonNotFoundException(Guid id)
    : Exception($"Comparison '{id}' was not found.")
{
    public Guid Id { get; } = id;
}

public sealed class ComparisonConflictException()
    : Exception("The comparison was updated by another request. Reload it before saving again.");

public sealed class ComparisonValidationException(IDictionary<string, string[]> errors)
    : Exception("The comparison request is invalid.")
{
    public IDictionary<string, string[]> Errors { get; } = errors;
}
