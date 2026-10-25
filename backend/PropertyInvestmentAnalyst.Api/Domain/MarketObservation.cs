namespace PropertyInvestmentAnalyst.Api.Domain;

public sealed class MarketObservation
{
    public const int StaleAfterDays = 180;
    public const int MinimumSampleForPublishedMedian = 5;
    public const int MinimumSampleForIndicativeMedian = 3;

    public static readonly string[] RecordTypes =
        [RecordTypeAskingPrice, RecordTypeAdvertisedRent, RecordTypeVerifiedTransaction, RecordTypeOther];

    public const string RecordTypeAskingPrice = "asking_price";
    public const string RecordTypeAdvertisedRent = "advertised_rent";
    public const string RecordTypeVerifiedTransaction = "verified_transaction";
    public const string RecordTypeOther = "other";

    public static readonly string[] VerificationStatuses =
        [VerificationUnverified, VerificationSourceCited, VerificationReviewed];

    public const string VerificationUnverified = "unverified";
    public const string VerificationSourceCited = "source_cited";
    public const string VerificationReviewed = "reviewed";

    public static readonly string[] AreaTypes =
        [AreaSuperBuiltUp, AreaBuiltUp, AreaCarpet, AreaPlot, AreaUnspecified];

    public const string AreaSuperBuiltUp = "super_built_up";
    public const string AreaBuiltUp = "built_up";
    public const string AreaCarpet = "carpet";
    public const string AreaPlot = "plot";
    public const string AreaUnspecified = "unspecified";

    public static readonly string[] AreaUnits = [UnitSqFt, UnitSqM];
    public const string UnitSqFt = "sq_ft";
    public const string UnitSqM = "sq_m";

    public Guid Id { get; set; }
    public required string Locality { get; set; }
    public required string BuilderProject { get; set; }
    public required string PropertyType { get; set; }
    public required string Configuration { get; set; }
    public required string RecordType { get; set; }
    public required string VerificationStatus { get; set; }
    public decimal Amount { get; set; }
    public decimal? AreaValue { get; set; }
    public required string AreaType { get; set; }
    public required string AreaUnit { get; set; }
    public decimal? PricePerSqFt { get; set; }
    public decimal? MonthlyRentPerSqFt { get; set; }
    public required string SourceName { get; set; }
    public string? SourceUrl { get; set; }
    public string? SourceRecordId { get; set; }
    public DateOnly ObservedOn { get; set; }
    public DateTimeOffset ImportedUtc { get; set; }
    public string? QualityNotes { get; set; }
    public string? LicenceOrUsageNotes { get; set; }
    public bool IsSyntheticTestData { get; set; }
    public bool IsOutlier { get; set; }
    public string? ProviderKey { get; set; }
    public required string DuplicateKey { get; set; }
}
