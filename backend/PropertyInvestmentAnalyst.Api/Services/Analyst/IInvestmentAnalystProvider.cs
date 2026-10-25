using PropertyInvestmentAnalyst.Api.Contracts;

namespace PropertyInvestmentAnalyst.Api.Services.Analyst;

public interface IInvestmentAnalystProvider
{
    string Key { get; }
    string DisplayName { get; }
    string Model { get; }
    bool IsConfigured { get; }

    Task<AnalystProviderResult> CompleteAsync(AnalystProviderRequest request, CancellationToken cancellationToken);
}
