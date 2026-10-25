using PropertyInvestmentAnalyst.Api.Contracts;

namespace PropertyInvestmentAnalyst.Api.Tests;

internal static class RequestFactory
{
    public static SaveComparisonRequest ValidRequest(string name = "Bengaluru sample") => new()
    {
        Name = name,
        Description = "Illustrative comparison",
        Properties = [ValidProperty("Whitefield 2 BHK")],
    };

    public static SaveComparedPropertyRequest ValidProperty(string name) => new()
    {
        Name = name,
        Locality = "Whitefield",
        BuilderProject = "Illustrative project",
        PropertyType = "Apartment",
        PurchasePrice = 12_000_000,
        SuperBuiltUpAreaSqFt = 1200,
        MonthlyRent = 40_000,
        MonthlyMaintenance = 5_000,
        DownPaymentPercent = 20,
        AnnualInterestRatePercent = 8.5m,
        LoanTenureYears = 20,
        AcquisitionCostPercent = 7,
        HoldingPeriodYears = 10,
        VacancyPercent = 5,
        AnnualPropertyTax = 0,
        AnnualRepairAndMaintenance = 0,
        Conservative = Scenario(2, 2, 8, 6, 2),
        Base = Scenario(5, 4, 5, 5, 2),
        Optimistic = Scenario(8, 6, 3, 4, 2),
    };

    public static SaveMarketObservationRequest ValidMarketObservation() => new()
    {
        Locality = "Whitefield",
        BuilderProject = "Synthetic fixture tower",
        PropertyType = "Apartment",
        Configuration = "2 BHK",
        RecordType = "asking_price",
        Amount = 12_000_000,
        AreaValue = 1200,
        AreaType = "super_built_up",
        AreaUnit = "sq_ft",
        SourceName = "Synthetic fixture — not a live listing",
        SourceRecordId = "SYN-MANUAL-001",
        ObservedOn = new DateOnly(2026, 10, 1),
        VerificationStatus = "source_cited",
        QualityNotes = "Unit test fixture.",
        LicenceOrUsageNotes = "Test data only.",
        IsSyntheticTestData = true,
    };

    public static ScenarioAssumptionsDto Scenario(
        decimal appreciation,
        decimal rentGrowth,
        decimal vacancy,
        decimal opexGrowth,
        decimal selling) => new()
    {
        PropertyAppreciationPercent = appreciation,
        RentGrowthPercent = rentGrowth,
        VacancyPercent = vacancy,
        OperatingExpenseGrowthPercent = opexGrowth,
        SellingCostPercent = selling,
    };
}
