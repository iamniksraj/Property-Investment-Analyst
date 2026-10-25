using PropertyInvestmentAnalyst.Api.Services.MarketData;

namespace PropertyInvestmentAnalyst.Api.Contracts;

public sealed class MarketImportRunDto
{
    public required string ProviderKey { get; init; }
    public required string Status { get; init; }
    public required DateTimeOffset StartedUtc { get; init; }
    public required DateTimeOffset CompletedUtc { get; init; }
    public DateOnly? ObservedFrom { get; init; }
    public DateOnly? ObservedTo { get; init; }
    public int Inserted { get; init; }
    public int Updated { get; init; }
    public int Skipped { get; init; }
    public int Duplicates { get; init; }
    public int Invalid { get; init; }
    public int ErrorCount { get; init; }
    public required string Summary { get; init; }
}

public sealed class MarketProviderStatusResponse
{
    public required bool HasGenuineLiveFeed { get; init; }
    public required bool AllowManualRefresh { get; init; }
    public required string IngestionPolicy { get; init; }
    public required int GenuineObservationCount { get; init; }
    public required int SyntheticObservationCount { get; init; }
    public DateOnly? NewestObservationDate { get; init; }
    public DateTimeOffset? LastSuccessfulImportUtc { get; init; }
    public MarketImportRunDto? LastImport { get; init; }
    public required IReadOnlyList<MarketProviderDescriptor> Providers { get; init; }
    public required string Notes { get; init; }
}

public sealed class MarketProviderRefreshResponse
{
    public required string FetchStatus { get; init; }
    public required string Message { get; init; }
    public required MarketImportResult Import { get; init; }
    public required MarketImportRunDto Run { get; init; }
}

public sealed class MarketProviderRefreshRequest
{
    public DateOnly? ObservedFrom { get; set; }
    public DateOnly? ObservedTo { get; set; }
}
