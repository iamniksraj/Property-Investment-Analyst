namespace PropertyInvestmentAnalyst.Api.Domain;

public sealed class ComparedProperty
{
    public Guid Id { get; set; }
    public Guid ComparisonAnalysisId { get; set; }
    public ComparisonAnalysis Comparison { get; set; } = null!;
    public int SortOrder { get; set; }
    public required string Name { get; set; }
    public string Locality { get; set; } = string.Empty;
    public string BuilderProject { get; set; } = string.Empty;
    public required string PropertyType { get; set; }
    public decimal? PurchasePrice { get; set; }
    public decimal? SuperBuiltUpAreaSqFt { get; set; }
    public decimal? MonthlyRent { get; set; }
    public decimal? MonthlyMaintenance { get; set; }
    public decimal DownPaymentPercent { get; set; }
    public decimal AnnualInterestRatePercent { get; set; }
    public decimal LoanTenureYears { get; set; }
    public decimal AcquisitionCostPercent { get; set; }
    public int HoldingPeriodYears { get; set; }
    public decimal VacancyPercent { get; set; }
    public decimal AnnualPropertyTax { get; set; }
    public decimal AnnualRepairs { get; set; }
    public ICollection<PropertyScenarioAssumption> Scenarios { get; set; } = new List<PropertyScenarioAssumption>();
}
