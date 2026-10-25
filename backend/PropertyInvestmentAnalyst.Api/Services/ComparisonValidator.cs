using PropertyInvestmentAnalyst.Api.Contracts;
using PropertyInvestmentAnalyst.Api.Domain;

namespace PropertyInvestmentAnalyst.Api.Services;

public static class ComparisonValidator
{
    public const int MaxProperties = 3;

    public static void Validate(SaveComparisonRequest request)
    {
        var errors = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

        if (string.IsNullOrWhiteSpace(request.Name))
        {
            Add(errors, nameof(request.Name), "A comparison name is required.");
        }

        if (request.Properties.Count > MaxProperties)
        {
            Add(errors, nameof(request.Properties), $"A comparison can include at most {MaxProperties} properties.");
        }

        for (var index = 0; index < request.Properties.Count; index += 1)
        {
            ValidateProperty(request.Properties[index], $"properties[{index}]", errors);
        }

        if (errors.Count > 0)
        {
            throw new ComparisonValidationException(
                errors.ToDictionary(pair => pair.Key, pair => pair.Value.ToArray(), StringComparer.OrdinalIgnoreCase));
        }
    }

    private static void ValidateProperty(
        SaveComparedPropertyRequest property,
        string prefix,
        IDictionary<string, List<string>> errors)
    {
        if (string.IsNullOrWhiteSpace(property.Name))
        {
            Add(errors, $"{prefix}.name", "A property name is required.");
        }

        if (string.IsNullOrWhiteSpace(property.PropertyType))
        {
            Add(errors, $"{prefix}.propertyType", "A property type is required.");
        }

        RequireNonNegative(property.PurchasePrice, $"{prefix}.purchasePrice", errors, allowNull: true, mustBePositive: true);
        RequireNonNegative(property.SuperBuiltUpAreaSqFt, $"{prefix}.superBuiltUpAreaSqFt", errors, allowNull: true, mustBePositive: true);
        RequireNonNegative(property.MonthlyRent, $"{prefix}.monthlyRent", errors, allowNull: true);
        RequireNonNegative(property.MonthlyMaintenance, $"{prefix}.monthlyMaintenance", errors, allowNull: true);
        RequireRange(property.DownPaymentPercent, 0, 100, $"{prefix}.downPaymentPercent", errors);
        RequireNonNegative(property.AnnualInterestRatePercent, $"{prefix}.annualInterestRatePercent", errors);
        if (property.LoanTenureYears <= 0)
        {
            Add(errors, $"{prefix}.loanTenureYears", "Loan tenure must be greater than zero.");
        }

        RequireNonNegative(property.AcquisitionCostPercent, $"{prefix}.acquisitionCostPercent", errors);
        if (property.HoldingPeriodYears < 0)
        {
            Add(errors, $"{prefix}.holdingPeriodYears", "Holding period cannot be negative.");
        }

        RequireRange(property.VacancyPercent, 0, 100, $"{prefix}.vacancyPercent", errors);
        RequireNonNegative(property.AnnualPropertyTax, $"{prefix}.annualPropertyTax", errors);
        RequireNonNegative(property.AnnualRepairAndMaintenance, $"{prefix}.annualRepairAndMaintenance", errors);
        ValidateScenario(property.Conservative, $"{prefix}.conservative", errors);
        ValidateScenario(property.Base, $"{prefix}.base", errors);
        ValidateScenario(property.Optimistic, $"{prefix}.optimistic", errors);
    }

    private static void ValidateScenario(
        ScenarioAssumptionsDto? scenario,
        string prefix,
        IDictionary<string, List<string>> errors)
    {
        if (scenario is null)
        {
            Add(errors, prefix, "Conservative, Base and Optimistic assumptions are required for each property.");
            return;
        }

        RequireNonNegative(scenario.PropertyAppreciationPercent, $"{prefix}.propertyAppreciationPercent", errors);
        RequireNonNegative(scenario.RentGrowthPercent, $"{prefix}.rentGrowthPercent", errors);
        RequireRange(scenario.VacancyPercent, 0, 100, $"{prefix}.vacancyPercent", errors);
        RequireNonNegative(scenario.OperatingExpenseGrowthPercent, $"{prefix}.operatingExpenseGrowthPercent", errors);
        RequireRange(scenario.SellingCostPercent, 0, 100, $"{prefix}.sellingCostPercent", errors);
    }

    private static void RequireNonNegative(
        decimal? value,
        string key,
        IDictionary<string, List<string>> errors,
        bool allowNull = false,
        bool mustBePositive = false)
    {
        if (value is null)
        {
            if (!allowNull)
            {
                Add(errors, key, "A numeric value is required.");
            }

            return;
        }

        if (mustBePositive && value <= 0)
        {
            Add(errors, key, "Enter a value greater than zero.");
            return;
        }

        if (value < 0)
        {
            Add(errors, key, "Enter zero or a positive value.");
        }
    }

    private static void RequireRange(
        decimal value,
        decimal min,
        decimal max,
        string key,
        IDictionary<string, List<string>> errors)
    {
        if (value < min || value > max)
        {
            Add(errors, key, $"Enter a value between {min} and {max}.");
        }
    }

    private static void Add(IDictionary<string, List<string>> errors, string key, string message)
    {
        if (!errors.TryGetValue(key, out var list))
        {
            list = [];
            errors[key] = list;
        }

        list.Add(message);
    }

    public static PropertyScenarioAssumption CreateScenario(string name, ScenarioAssumptionsDto dto) => new()
    {
        Id = Guid.NewGuid(),
        ScenarioName = name,
        PropertyAppreciationPercent = dto.PropertyAppreciationPercent,
        RentGrowthPercent = dto.RentGrowthPercent,
        VacancyPercent = dto.VacancyPercent,
        OperatingExpenseGrowthPercent = dto.OperatingExpenseGrowthPercent,
        SellingCostPercent = dto.SellingCostPercent,
    };
}
