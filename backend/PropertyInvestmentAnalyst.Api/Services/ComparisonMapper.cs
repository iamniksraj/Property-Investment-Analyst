using PropertyInvestmentAnalyst.Api.Contracts;
using PropertyInvestmentAnalyst.Api.Domain;

namespace PropertyInvestmentAnalyst.Api.Services;

public static class ComparisonMapper
{
    public static ComparisonSummaryDto ToSummary(ComparisonAnalysis analysis) => new()
    {
        Id = analysis.Id,
        Name = analysis.Name,
        Description = analysis.Description,
        CreatedUtc = analysis.CreatedUtc,
        UpdatedUtc = analysis.UpdatedUtc,
        PropertyCount = analysis.Properties.Count,
        SchemaVersion = analysis.SchemaVersion,
    };

    public static ComparisonDetailDto ToDetail(ComparisonAnalysis analysis) => new()
    {
        Id = analysis.Id,
        Name = analysis.Name,
        Description = analysis.Description,
        CreatedUtc = analysis.CreatedUtc,
        UpdatedUtc = analysis.UpdatedUtc,
        SchemaVersion = analysis.SchemaVersion,
        Properties = analysis.Properties
            .OrderBy(item => item.SortOrder)
            .Select(ToProperty)
            .ToArray(),
    };

    public static ComparedPropertyDto ToProperty(ComparedProperty property)
    {
        var scenarios = property.Scenarios.ToDictionary(item => item.ScenarioName, StringComparer.OrdinalIgnoreCase);
        return new ComparedPropertyDto
        {
            Id = property.Id,
            Name = property.Name,
            Locality = property.Locality,
            BuilderProject = property.BuilderProject,
            PropertyType = property.PropertyType,
            PurchasePrice = property.PurchasePrice,
            SuperBuiltUpAreaSqFt = property.SuperBuiltUpAreaSqFt,
            MonthlyRent = property.MonthlyRent,
            MonthlyMaintenance = property.MonthlyMaintenance,
            DownPaymentPercent = property.DownPaymentPercent,
            AnnualInterestRatePercent = property.AnnualInterestRatePercent,
            LoanTenureYears = property.LoanTenureYears,
            AcquisitionCostPercent = property.AcquisitionCostPercent,
            HoldingPeriodYears = property.HoldingPeriodYears,
            VacancyPercent = property.VacancyPercent,
            AnnualPropertyTax = property.AnnualPropertyTax,
            AnnualRepairAndMaintenance = property.AnnualRepairs,
            Conservative = ToScenario(scenarios[PropertyScenarioAssumption.Conservative]),
            Base = ToScenario(scenarios[PropertyScenarioAssumption.Base]),
            Optimistic = ToScenario(scenarios[PropertyScenarioAssumption.Optimistic]),
        };
    }

    public static ScenarioAssumptionsDto ToScenario(PropertyScenarioAssumption scenario) => new()
    {
        PropertyAppreciationPercent = scenario.PropertyAppreciationPercent,
        RentGrowthPercent = scenario.RentGrowthPercent,
        VacancyPercent = scenario.VacancyPercent,
        OperatingExpenseGrowthPercent = scenario.OperatingExpenseGrowthPercent,
        SellingCostPercent = scenario.SellingCostPercent,
    };

    public static ComparedProperty ToEntity(SaveComparedPropertyRequest request, Guid comparisonId, int sortOrder)
    {
        var property = new ComparedProperty
        {
            Id = Guid.NewGuid(),
            ComparisonAnalysisId = comparisonId,
            SortOrder = sortOrder,
            Name = request.Name.Trim(),
            Locality = request.Locality?.Trim() ?? string.Empty,
            BuilderProject = request.BuilderProject?.Trim() ?? string.Empty,
            PropertyType = request.PropertyType.Trim(),
            PurchasePrice = request.PurchasePrice,
            SuperBuiltUpAreaSqFt = request.SuperBuiltUpAreaSqFt,
            MonthlyRent = request.MonthlyRent,
            MonthlyMaintenance = request.MonthlyMaintenance,
            DownPaymentPercent = request.DownPaymentPercent,
            AnnualInterestRatePercent = request.AnnualInterestRatePercent,
            LoanTenureYears = request.LoanTenureYears,
            AcquisitionCostPercent = request.AcquisitionCostPercent,
            HoldingPeriodYears = request.HoldingPeriodYears,
            VacancyPercent = request.VacancyPercent,
            AnnualPropertyTax = request.AnnualPropertyTax,
            AnnualRepairs = request.AnnualRepairAndMaintenance,
        };

        ApplyScenarios(property, request);
        return property;
    }

    public static void Apply(ComparedProperty property, SaveComparedPropertyRequest request, int sortOrder)
    {
        property.SortOrder = sortOrder;
        property.Name = request.Name.Trim();
        property.Locality = request.Locality?.Trim() ?? string.Empty;
        property.BuilderProject = request.BuilderProject?.Trim() ?? string.Empty;
        property.PropertyType = request.PropertyType.Trim();
        property.PurchasePrice = request.PurchasePrice;
        property.SuperBuiltUpAreaSqFt = request.SuperBuiltUpAreaSqFt;
        property.MonthlyRent = request.MonthlyRent;
        property.MonthlyMaintenance = request.MonthlyMaintenance;
        property.DownPaymentPercent = request.DownPaymentPercent;
        property.AnnualInterestRatePercent = request.AnnualInterestRatePercent;
        property.LoanTenureYears = request.LoanTenureYears;
        property.AcquisitionCostPercent = request.AcquisitionCostPercent;
        property.HoldingPeriodYears = request.HoldingPeriodYears;
        property.VacancyPercent = request.VacancyPercent;
        property.AnnualPropertyTax = request.AnnualPropertyTax;
        property.AnnualRepairs = request.AnnualRepairAndMaintenance;
        ApplyScenarios(property, request);
    }

    private static void ApplyScenarios(ComparedProperty property, SaveComparedPropertyRequest request)
    {
        UpsertScenario(property, PropertyScenarioAssumption.Conservative, request.Conservative!);
        UpsertScenario(property, PropertyScenarioAssumption.Base, request.Base!);
        UpsertScenario(property, PropertyScenarioAssumption.Optimistic, request.Optimistic!);
    }

    private static void UpsertScenario(ComparedProperty property, string name, ScenarioAssumptionsDto dto)
    {
        var existing = property.Scenarios.FirstOrDefault(item =>
            string.Equals(item.ScenarioName, name, StringComparison.OrdinalIgnoreCase));
        if (existing is null)
        {
            var created = ComparisonValidator.CreateScenario(name, dto);
            created.ComparedPropertyId = property.Id;
            property.Scenarios.Add(created);
            return;
        }

        existing.PropertyAppreciationPercent = dto.PropertyAppreciationPercent;
        existing.RentGrowthPercent = dto.RentGrowthPercent;
        existing.VacancyPercent = dto.VacancyPercent;
        existing.OperatingExpenseGrowthPercent = dto.OperatingExpenseGrowthPercent;
        existing.SellingCostPercent = dto.SellingCostPercent;
    }
}
