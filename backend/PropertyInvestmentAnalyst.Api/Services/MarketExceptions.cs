namespace PropertyInvestmentAnalyst.Api.Services;

public sealed class MarketNotFoundException(Guid id)
    : Exception($"Market observation '{id}' was not found.")
{
    public Guid Id { get; } = id;
}

public sealed class MarketValidationException(IDictionary<string, string[]> errors)
    : Exception("The market-data request is invalid.")
{
    public IDictionary<string, string[]> Errors { get; } = errors;
}
