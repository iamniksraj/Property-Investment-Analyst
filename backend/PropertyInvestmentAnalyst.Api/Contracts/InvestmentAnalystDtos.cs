using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace PropertyInvestmentAnalyst.Api.Contracts;

public sealed class InvestmentAnalystStatusResponse
{
    public required bool Configured { get; init; }
    public required string Provider { get; init; }
    public required string Model { get; init; }
    public required string SetupGuidance { get; init; }
    public required string Disclaimer { get; init; }
    public required bool HasGenuineLiveFeed { get; init; }
}

public sealed class InvestmentAnalystAskRequest
{
    [Required]
    public string Question { get; set; } = string.Empty;

    public Guid? ComparisonId { get; set; }

    public List<Guid> ObservationIds { get; set; } = [];

    public bool IncludeMarketEvidence { get; set; } = true;

    public List<AnalystPropertyContextDto> Properties { get; set; } = [];
}

public sealed class AnalystPropertyContextDto
{
    [MaxLength(120)]
    public string? Name { get; set; }

    [MaxLength(80)]
    public string? Locality { get; set; }

    [MaxLength(120)]
    public string? BuilderProject { get; set; }

    [MaxLength(40)]
    public string? PropertyType { get; set; }

    public decimal? PurchasePrice { get; set; }
    public decimal? SuperBuiltUpAreaSqFt { get; set; }
    public decimal? MonthlyRent { get; set; }
    public decimal? MonthlyMaintenance { get; set; }
    public decimal? DownPaymentPercent { get; set; }
    public decimal? AnnualInterestRatePercent { get; set; }
    public decimal? LoanTenureYears { get; set; }
    public decimal? AcquisitionCostPercent { get; set; }
    public int? HoldingPeriodYears { get; set; }
    public decimal? VacancyPercent { get; set; }
    public decimal? AnnualPropertyTax { get; set; }
    public decimal? AnnualRepairAndMaintenance { get; set; }

    public AnalystSnapshotMetricsDto? Snapshot { get; set; }
    public AnalystMortgageMetricsDto? Mortgage { get; set; }
    public AnalystScenarioMetricsDto? Conservative { get; set; }
    public AnalystScenarioMetricsDto? Base { get; set; }
    public AnalystScenarioMetricsDto? Optimistic { get; set; }
}

public sealed class AnalystSnapshotMetricsDto
{
    public decimal? PricePerSqFt { get; set; }
    public decimal? GrossAnnualRentalYieldPercent { get; set; }
    public decimal? AnnualRentalIncome { get; set; }
    public decimal? EstimatedAnnualMaintenance { get; set; }
    public decimal? NetRentalIncomeBeforeOtherExpenses { get; set; }
}

public sealed class AnalystMortgageMetricsDto
{
    public decimal? DownPaymentAmount { get; set; }
    public decimal? LoanPrincipal { get; set; }
    public decimal? MonthlyEmi { get; set; }
    public decimal? AnnualLoanPayments { get; set; }
    public decimal? AcquisitionCostAmount { get; set; }
    public decimal? TotalInitialAcquisitionCash { get; set; }
    public decimal? AnnualPotentialRent { get; set; }
    public decimal? AnnualRentAfterVacancy { get; set; }
    public decimal? AnnualOperatingExpenses { get; set; }
    public decimal? AnnualCashFlowAfterOperatingExpensesAndLoanPayments { get; set; }
    public decimal? OutstandingLoanBalanceAfterHoldingPeriod { get; set; }
}

public sealed class AnalystScenarioMetricsDto
{
    public decimal? PropertyAppreciationPercent { get; set; }
    public decimal? RentGrowthPercent { get; set; }
    public decimal? VacancyPercent { get; set; }
    public decimal? OperatingExpenseGrowthPercent { get; set; }
    public decimal? SellingCostPercent { get; set; }
    public decimal? InitialCashInvested { get; set; }
    public decimal? CumulativeRentalCashFlowAfterDebtService { get; set; }
    public decimal? ExitPropertyValue { get; set; }
    public decimal? OutstandingLoanAtExit { get; set; }
    public decimal? SellingCosts { get; set; }
    public decimal? NetSaleProceeds { get; set; }
    public decimal? TotalCashReceived { get; set; }
    public decimal? NetProfit { get; set; }
    public decimal? GrossPropertyAppreciationPercent { get; set; }
    public decimal? AnnualisedEquityIrr { get; set; }
    public string? IrrUnavailableReason { get; set; }
}

public sealed class InvestmentAnalystAskResponse
{
    public required string Status { get; init; }
    public required string Disclaimer { get; init; }
    public string? SetupGuidance { get; init; }
    public required IReadOnlyList<AnalystSectionDto> Sections { get; init; }
    public required IReadOnlyList<AnalystEvidenceReferenceDto> Evidence { get; init; }
    public required IReadOnlyList<string> Warnings { get; init; }
    public bool WroteToDatabase { get; init; }
}

public sealed class AnalystSectionDto
{
    public required string Key { get; init; }
    public required string Title { get; init; }
    public required string Body { get; init; }
}

public sealed class AnalystEvidenceReferenceDto
{
    public required string Kind { get; init; }
    public string? Id { get; init; }
    public required string Label { get; init; }
    public string? Href { get; init; }
    public bool IsSyntheticTestData { get; init; }
    public bool IsStale { get; init; }
    public string? RecordType { get; init; }
    public string? VerificationStatus { get; init; }
}

public sealed class AnalystProviderRequest
{
    public required string Question { get; init; }
    public required string ContextJson { get; init; }
}

public sealed class AnalystProviderResult
{
    public required string Status { get; init; }
    public string? Message { get; init; }
    public IReadOnlyList<AnalystSectionDto> Sections { get; init; } = [];
    public string? Model { get; init; }

    [JsonIgnore]
    public bool Succeeded => Status == "completed";
}
