namespace PropertyInvestmentAnalyst.Api.Contracts;

public sealed class MarketFilter
{
    public string Locality { get; set; } = string.Empty;
    public string? PropertyType { get; set; }
    public string? Configuration { get; set; }
    public string? RecordType { get; set; }
    public string? AreaType { get; set; }
    public string? AreaUnit { get; set; }
    public string? VerificationStatus { get; set; }
    public bool IncludeSynthetic { get; set; }
    public bool IncludeOutliers { get; set; }
    public DateOnly? ObservedFrom { get; set; }
    public DateOnly? ObservedTo { get; set; }
    public int Skip { get; set; }
    public int Take { get; set; } = 50;
}

public sealed class ExclusionCountDto
{
    public required string Reason { get; init; }
    public required int Count { get; init; }
    public required string Explanation { get; init; }
}

public sealed class EvidenceQualityDto
{
    public required string SampleSize { get; init; }
    public required string Recency { get; init; }
    public required string Verification { get; init; }
    public required string AreaComparability { get; init; }
    public required string SourceCompleteness { get; init; }
    public required string Synthetic { get; init; }
    public required string Outliers { get; init; }
    public required IReadOnlyList<string> Notes { get; init; }
}

public sealed class HistoryPointDto
{
    public required string YearMonth { get; init; }
    public required int Count { get; init; }
    public decimal? MedianAmount { get; init; }
    public required bool InsufficientMonth { get; init; }
}

public sealed class HistorySeriesDto
{
    public required string Interval { get; init; }
    public required bool Sufficient { get; init; }
    public required bool UsingSyntheticTestData { get; init; }
    public required string Reason { get; init; }
    public required IReadOnlyList<HistoryPointDto> Points { get; init; }
}

public sealed class AssumptionComparisonRequest
{
    public required string Locality { get; set; }
    public string? PropertyType { get; set; }
    public string? Configuration { get; set; }
    public bool IncludeSynthetic { get; set; }
    public bool AcknowledgeIncompatibleArea { get; set; }
    public decimal? PurchasePrice { get; set; }
    public decimal? MonthlyRent { get; set; }
    public decimal? AreaValue { get; set; }
    public string? AreaType { get; set; }
    public string? AreaUnit { get; set; }
}

public sealed class AssumptionMetricDto
{
    public required string RecordType { get; init; }
    public required bool Compared { get; init; }
    public required string Status { get; init; }
    public int ComparableCount { get; init; }
    public decimal? UserAmount { get; init; }
    public decimal? UserPerSqFt { get; init; }
    public decimal? MedianAmount { get; init; }
    public decimal? Percentile25 { get; init; }
    public decimal? Percentile75 { get; init; }
    public decimal? MedianPerSqFt { get; init; }
    public decimal? AmountDifference { get; init; }
    public decimal? AmountDifferencePercent { get; init; }
    public decimal? PerSqFtDifference { get; init; }
    public decimal? PerSqFtDifferencePercent { get; init; }
    public DateOnly? OldestObservedOn { get; init; }
    public DateOnly? NewestObservedOn { get; init; }
    public required bool IncompatibleArea { get; init; }
    public required IReadOnlyList<string> Limitations { get; init; }
}

public sealed class AssumptionComparisonResponse
{
    public required bool UsingSyntheticTestData { get; init; }
    public required string Disclaimer { get; init; }
    public AssumptionMetricDto? AskingPrice { get; init; }
    public AssumptionMetricDto? AdvertisedRent { get; init; }
}
