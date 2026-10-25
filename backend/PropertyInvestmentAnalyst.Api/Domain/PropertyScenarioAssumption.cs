namespace PropertyInvestmentAnalyst.Api.Domain;

public sealed class PropertyScenarioAssumption
{
    public const string Conservative = "conservative";
    public const string Base = "base";
    public const string Optimistic = "optimistic";

    public static readonly string[] AllowedNames = [Conservative, Base, Optimistic];

    public Guid Id { get; set; }
    public Guid ComparedPropertyId { get; set; }
    public ComparedProperty Property { get; set; } = null!;
    public required string ScenarioName { get; set; }
    public decimal PropertyAppreciationPercent { get; set; }
    public decimal RentGrowthPercent { get; set; }
    public decimal VacancyPercent { get; set; }
    public decimal OperatingExpenseGrowthPercent { get; set; }
    public decimal SellingCostPercent { get; set; }
}
