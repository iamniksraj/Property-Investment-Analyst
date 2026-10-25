using System.ComponentModel.DataAnnotations;

namespace PropertyInvestmentAnalyst.Api.Contracts;

public sealed class MarketObservationDto
{
    public required Guid Id { get; init; }
    public required string Locality { get; init; }
    public required string BuilderProject { get; init; }
    public required string PropertyType { get; init; }
    public required string Configuration { get; init; }
    public required string RecordType { get; init; }
    public required string VerificationStatus { get; init; }
    public required decimal Amount { get; init; }
    public decimal? AreaValue { get; init; }
    public required string AreaType { get; init; }
    public required string AreaUnit { get; init; }
    public decimal? PricePerSqFt { get; init; }
    public decimal? MonthlyRentPerSqFt { get; init; }
    public required string SourceName { get; init; }
    public string? SourceUrl { get; init; }
    public string? SourceRecordId { get; init; }
    public required DateOnly ObservedOn { get; init; }
    public required DateTimeOffset ImportedUtc { get; init; }
    public string? QualityNotes { get; init; }
    public string? LicenceOrUsageNotes { get; init; }
    public required bool IsSyntheticTestData { get; init; }
    public required bool IsOutlier { get; init; }
    public required bool IsStale { get; init; }
    public required bool IsUnverified { get; init; }
    public required bool HasCompleteSource { get; init; }
    public string? ProviderKey { get; init; }
}

public sealed class SaveMarketObservationRequest
{
    [Required, MaxLength(80)]
    public string Locality { get; set; } = string.Empty;

    [Required, MaxLength(120)]
    public string BuilderProject { get; set; } = string.Empty;

    [MaxLength(40)]
    public string PropertyType { get; set; } = "Apartment";

    [Required, MaxLength(40)]
    public string Configuration { get; set; } = string.Empty;

    [Required, MaxLength(32)]
    public string RecordType { get; set; } = string.Empty;

    [MaxLength(32)]
    public string? VerificationStatus { get; set; }

    [Required]
    public decimal Amount { get; set; }

    public decimal? AreaValue { get; set; }

    [MaxLength(32)]
    public string AreaType { get; set; } = "unspecified";

    [MaxLength(16)]
    public string AreaUnit { get; set; } = "sq_ft";

    [Required, MaxLength(160)]
    public string SourceName { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? SourceUrl { get; set; }

    [MaxLength(120)]
    public string? SourceRecordId { get; set; }

    [Required]
    public DateOnly ObservedOn { get; set; }

    [MaxLength(1000)]
    public string? QualityNotes { get; set; }

    [MaxLength(500)]
    public string? LicenceOrUsageNotes { get; set; }

    public bool IsSyntheticTestData { get; set; }

    [MaxLength(64)]
    public string? ProviderKey { get; set; }
}

public sealed class MarketImportRowResult
{
    public required int LineNumber { get; init; }
    public required string Status { get; init; }
    public required IReadOnlyList<string> Errors { get; init; }
    public string? DuplicateKey { get; init; }
    public MarketObservationDto? Observation { get; init; }
}

public sealed class MarketImportResult
{
    public required int TotalRows { get; init; }
    public required int Valid { get; init; }
    public required int Invalid { get; init; }
    public required int Duplicates { get; init; }
    public required int Outliers { get; init; }
    public required int Inserted { get; init; }
    public int Updated { get; init; }
    public int Skipped { get; init; }
    public required IReadOnlyList<MarketImportRowResult> Rows { get; init; }
}

public sealed class MarketStatisticBucketDto
{
    public required string RecordType { get; init; }
    public required int MatchedCount { get; init; }
    public required int SampleSize { get; init; }
    public required int ExcludedOutliers { get; init; }
    public required bool IsSparse { get; init; }
    public required bool InsufficientData { get; init; }
    public required bool MixedAreaDefinitions { get; init; }
    public DateOnly? OldestObservedOn { get; init; }
    public DateOnly? NewestObservedOn { get; init; }
    public decimal? MinAmount { get; init; }
    public decimal? MaxAmount { get; init; }
    public decimal? MedianAmount { get; init; }
    public decimal? Percentile25 { get; init; }
    public decimal? Percentile75 { get; init; }
    public decimal? MedianPricePerSqFt { get; init; }
    public decimal? MedianMonthlyRentPerSqFt { get; init; }
    public required IReadOnlyList<ExclusionCountDto> Exclusions { get; init; }
    public required EvidenceQualityDto Quality { get; init; }
    public required string Label { get; init; }
    public required string Methodology { get; init; }
}

public sealed class MarketStatsResponse
{
    public required string Locality { get; init; }
    public string? PropertyType { get; init; }
    public string? Configuration { get; init; }
    public required bool IncludeSynthetic { get; init; }
    public required bool OnlySyntheticAvailable { get; init; }
    public required bool UsingSyntheticTestData { get; init; }
    public required int GenuineCount { get; init; }
    public required int SyntheticCount { get; init; }
    public required IReadOnlyList<MarketStatisticBucketDto> Buckets { get; init; }
    public required HistorySeriesDto History { get; init; }
    public required string Notes { get; init; }
}

public sealed class MarketSearchResponse
{
    public required IReadOnlyList<MarketObservationDto> Items { get; init; }
    public required int Count { get; init; }
    public required int TotalCount { get; init; }
    public required int Skip { get; init; }
    public required int Take { get; init; }
    public DateOnly? OldestObservedOn { get; init; }
    public DateOnly? NewestObservedOn { get; init; }
    public required bool IsSparse { get; init; }
    public required bool ContainsStale { get; init; }
    public required bool ContainsUnverified { get; init; }
    public required bool ContainsSynthetic { get; init; }
    public required bool OnlySyntheticAvailable { get; init; }
}
