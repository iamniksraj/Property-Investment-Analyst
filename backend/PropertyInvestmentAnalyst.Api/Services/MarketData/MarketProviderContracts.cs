using PropertyInvestmentAnalyst.Api.Contracts;
using PropertyInvestmentAnalyst.Api.Domain;

namespace PropertyInvestmentAnalyst.Api.Services.MarketData;

public sealed class MarketProviderDescriptor
{
    public required string Key { get; init; }
    public required string DisplayName { get; init; }
    public required string Kind { get; init; }
    public required bool FetchSupported { get; init; }
    public required bool Configured { get; init; }
    public required bool LiveFeed { get; init; }
    public required string Coverage { get; init; }
    public required string AccessNotes { get; init; }
    public required string DocumentationUrl { get; init; }
}

public sealed class MarketProviderFetchRequest
{
    public DateOnly? ObservedFrom { get; init; }
    public DateOnly? ObservedTo { get; init; }
    public IReadOnlyList<string> Localities { get; init; } = ["Whitefield", "HSR Layout", "Sarjapur Road", "Electronic City", "Marathahalli"];
}

public sealed class ProviderFetchResult
{
    public required string Status { get; init; }
    public required string Message { get; init; }
    public IReadOnlyList<NormalizedMarketObservation> Observations { get; init; } = [];
    public int ErrorCount { get; init; }
}

public sealed class NormalizedMarketObservation
{
    public required string Locality { get; init; }
    public required string BuilderProject { get; init; }
    public string PropertyType { get; init; } = "Apartment";
    public required string Configuration { get; init; }
    public required string RecordType { get; init; }
    public required decimal Amount { get; init; }
    public decimal? AreaValue { get; init; }
    public string AreaType { get; init; } = MarketObservation.AreaUnspecified;
    public string AreaUnit { get; init; } = MarketObservation.UnitSqFt;
    public required string SourceName { get; init; }
    public string? SourceUrl { get; init; }
    public string? SourceRecordId { get; init; }
    public required DateOnly ObservedOn { get; init; }
    public string? QualityNotes { get; init; }
    public string? LicenceOrUsageNotes { get; init; }
    public bool IsSyntheticTestData { get; init; }
}

public interface IMarketDataProvider
{
    string Key { get; }
    MarketProviderDescriptor Describe();
    Task<ProviderFetchResult> FetchAsync(MarketProviderFetchRequest request, CancellationToken cancellationToken);
}

public static class NormalizedObservationMapper
{
    public static SaveMarketObservationRequest ToRequest(NormalizedMarketObservation item)
    {
        var hasSource = !string.IsNullOrWhiteSpace(item.SourceName)
            && (!string.IsNullOrWhiteSpace(item.SourceUrl) || !string.IsNullOrWhiteSpace(item.SourceRecordId));

        return new SaveMarketObservationRequest
        {
            Locality = item.Locality,
            BuilderProject = item.BuilderProject,
            PropertyType = item.PropertyType,
            Configuration = item.Configuration,
            RecordType = item.RecordType,
            Amount = item.Amount,
            AreaValue = item.AreaValue,
            AreaType = item.AreaType,
            AreaUnit = item.AreaUnit,
            SourceName = item.SourceName,
            SourceUrl = item.SourceUrl,
            SourceRecordId = item.SourceRecordId,
            ObservedOn = item.ObservedOn,
            QualityNotes = item.QualityNotes,
            LicenceOrUsageNotes = item.LicenceOrUsageNotes,
            IsSyntheticTestData = item.IsSyntheticTestData,
            VerificationStatus = item.IsSyntheticTestData
                ? MarketObservation.VerificationSourceCited
                : hasSource
                    ? MarketObservation.VerificationSourceCited
                    : MarketObservation.VerificationUnverified,
            ProviderKey = null,
        };
    }
}
