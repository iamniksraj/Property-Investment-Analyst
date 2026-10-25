using System.ComponentModel.DataAnnotations;

namespace PropertyInvestmentAnalyst.Api.Contracts;

public sealed class ComparisonSummaryDto
{
    public required Guid Id { get; init; }
    public required string Name { get; init; }
    public string? Description { get; init; }
    public required DateTimeOffset CreatedUtc { get; init; }
    public required DateTimeOffset UpdatedUtc { get; init; }
    public required int PropertyCount { get; init; }
    public required int SchemaVersion { get; init; }
}

public sealed class ComparisonDetailDto
{
    public required Guid Id { get; init; }
    public required string Name { get; init; }
    public string? Description { get; init; }
    public required DateTimeOffset CreatedUtc { get; init; }
    public required DateTimeOffset UpdatedUtc { get; init; }
    public required int SchemaVersion { get; init; }
    public required IReadOnlyList<ComparedPropertyDto> Properties { get; init; }
}

public sealed class ComparedPropertyDto
{
    public required Guid Id { get; init; }
    public required string Name { get; init; }
    public required string Locality { get; init; }
    public required string BuilderProject { get; init; }
    public required string PropertyType { get; init; }
    public decimal? PurchasePrice { get; init; }
    public decimal? SuperBuiltUpAreaSqFt { get; init; }
    public decimal? MonthlyRent { get; init; }
    public decimal? MonthlyMaintenance { get; init; }
    public required decimal DownPaymentPercent { get; init; }
    public required decimal AnnualInterestRatePercent { get; init; }
    public required decimal LoanTenureYears { get; init; }
    public required decimal AcquisitionCostPercent { get; init; }
    public required int HoldingPeriodYears { get; init; }
    public required decimal VacancyPercent { get; init; }
    public required decimal AnnualPropertyTax { get; init; }
    public required decimal AnnualRepairAndMaintenance { get; init; }
    public required ScenarioAssumptionsDto Conservative { get; init; }
    public required ScenarioAssumptionsDto Base { get; init; }
    public required ScenarioAssumptionsDto Optimistic { get; init; }
}

public sealed class ScenarioAssumptionsDto
{
    public required decimal PropertyAppreciationPercent { get; init; }
    public required decimal RentGrowthPercent { get; init; }
    public required decimal VacancyPercent { get; init; }
    public required decimal OperatingExpenseGrowthPercent { get; init; }
    public required decimal SellingCostPercent { get; init; }
}

public sealed class SaveComparisonRequest
{
    [Required]
    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string? Description { get; set; }

    public DateTimeOffset? ExpectedUpdatedUtc { get; set; }

    [Required]
    public List<SaveComparedPropertyRequest> Properties { get; set; } = [];
}

public sealed class SaveComparedPropertyRequest
{
    public Guid? Id { get; set; }

    [Required]
    [MaxLength(120)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(80)]
    public string Locality { get; set; } = string.Empty;

    [MaxLength(120)]
    public string BuilderProject { get; set; } = string.Empty;

    [Required]
    [MaxLength(40)]
    public string PropertyType { get; set; } = "Apartment";

    public decimal? PurchasePrice { get; set; }
    public decimal? SuperBuiltUpAreaSqFt { get; set; }
    public decimal? MonthlyRent { get; set; }
    public decimal? MonthlyMaintenance { get; set; }
    public decimal DownPaymentPercent { get; set; }
    public decimal AnnualInterestRatePercent { get; set; }
    public decimal LoanTenureYears { get; set; } = 20;
    public decimal AcquisitionCostPercent { get; set; }
    public int HoldingPeriodYears { get; set; }
    public decimal VacancyPercent { get; set; }
    public decimal AnnualPropertyTax { get; set; }
    public decimal AnnualRepairAndMaintenance { get; set; }
    public ScenarioAssumptionsDto? Conservative { get; set; }
    public ScenarioAssumptionsDto? Base { get; set; }
    public ScenarioAssumptionsDto? Optimistic { get; set; }
}

public sealed class HealthResponse
{
    public required string Status { get; init; }
    public required DateTimeOffset TimestampUtc { get; init; }
    public required string Environment { get; init; }
}
