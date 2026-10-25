using System.Text.Json;
using System.Text.Json.Serialization;
using PropertyInvestmentAnalyst.Api.Contracts;

namespace PropertyInvestmentAnalyst.Api.Services.Analyst;

public static class InvestmentAnalystContextBuilder
{
    public const int MaxQuestionChars = 4000;
    public const int MaxProperties = 3;
    public const int MaxObservationIds = 8;
    public const int MaxMarketRows = 8;
    public const int MaxContextChars = 24_000;
    public const decimal MaxMoney = 1_000_000_000_000m;
    public const decimal MaxPercent = 1_000m;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = false,
    };

    public static void Validate(InvestmentAnalystAskRequest request)
    {
        var errors = new Dictionary<string, List<string>>();
        var question = request.Question?.Trim() ?? string.Empty;
        if (question.Length == 0)
        {
            Add(errors, "question", "Enter a question.");
        }
        else if (question.Length > MaxQuestionChars)
        {
            Add(errors, "question", $"Questions are limited to {MaxQuestionChars} characters.");
        }

        if (request.Properties.Count > MaxProperties)
        {
            Add(errors, "properties", $"At most {MaxProperties} properties may be sent.");
        }

        if (request.ObservationIds.Count > MaxObservationIds)
        {
            Add(errors, "observationIds", $"At most {MaxObservationIds} observation identifiers may be sent.");
        }

        if (request.ObservationIds.Distinct().Count() != request.ObservationIds.Count)
        {
            Add(errors, "observationIds", "Observation identifiers must be unique.");
        }

        var index = 0;
        foreach (var property in request.Properties)
        {
            ValidateProperty(property, $"properties[{index}]", errors);
            index += 1;
        }

        if (errors.Count > 0)
        {
            throw new AnalystValidationException(errors.ToDictionary(item => item.Key, item => item.Value.ToArray()));
        }
    }

    public static string Serialize(
        string question,
        ComparisonDetailDto? saved,
        IReadOnlyList<AnalystPropertyContextDto> calculated,
        IReadOnlyList<MarketObservationDto> observations,
        IReadOnlyList<string> warnings)
    {
        var payload = new Dictionary<string, object?>
        {
            ["calculationSource"] =
                "TypeScript engine in the browser. The API stores inputs only and does not recalculate IRR, EMI or scenarios.",
            ["marketFeedStatus"] = "No genuine live Bengaluru market feed is currently connected.",
            ["question"] = question.Trim(),
            ["savedComparison"] = saved is null
                ? null
                : new
                {
                    saved.Id,
                    saved.Name,
                    saved.Description,
                    saved.UpdatedUtc,
                    properties = saved.Properties.Select(item => new
                    {
                        item.Id,
                        item.Name,
                        item.Locality,
                        item.BuilderProject,
                        item.PropertyType,
                        item.PurchasePrice,
                        item.SuperBuiltUpAreaSqFt,
                        item.MonthlyRent,
                        item.MonthlyMaintenance,
                        item.DownPaymentPercent,
                        item.AnnualInterestRatePercent,
                        item.LoanTenureYears,
                        item.AcquisitionCostPercent,
                        item.HoldingPeriodYears,
                        item.VacancyPercent,
                        item.AnnualPropertyTax,
                        item.AnnualRepairAndMaintenance,
                        conservative = item.Conservative,
                        @base = item.Base,
                        optimistic = item.Optimistic,
                        calculatedMetricsIncluded = false,
                    }),
                    note = calculated.Count == 0
                        ? "Saved records contain inputs and assumptions only. Do not invent yield, cash flow, net profit or IRR."
                        : "Browser-calculated metrics are supplied separately and remain the source of truth.",
                },
            ["browserCalculatedProperties"] = calculated.Select(ToContextProperty).ToArray(),
            ["marketObservations"] = observations.Select(item => new
            {
                item.Id,
                item.Locality,
                item.BuilderProject,
                item.PropertyType,
                item.Configuration,
                item.RecordType,
                item.VerificationStatus,
                item.Amount,
                item.AreaType,
                item.AreaUnit,
                item.AreaValue,
                item.PricePerSqFt,
                item.SourceName,
                item.ObservedOn,
                item.IsSyntheticTestData,
                item.IsStale,
                item.IsUnverified,
                item.IsOutlier,
                item.HasCompleteSource,
                genuineMarketEvidence = !item.IsSyntheticTestData,
            }).ToArray(),
            ["warnings"] = warnings,
        };

        var json = JsonSerializer.Serialize(payload, JsonOptions);
        if (json.Length <= MaxContextChars)
        {
            return json;
        }

        payload["marketObservations"] = observations.Take(3).Select(item => new { item.Id, item.Locality, item.RecordType, item.IsSyntheticTestData }).ToArray();
        json = JsonSerializer.Serialize(payload, JsonOptions);
        return json.Length <= MaxContextChars ? json : json[..MaxContextChars];
    }

    public static IReadOnlyList<string> BuildWarnings(
        ComparisonDetailDto? saved,
        IReadOnlyList<AnalystPropertyContextDto> calculated,
        IReadOnlyList<MarketObservationDto> observations)
    {
        var warnings = new List<string>();
        if (calculated.Count == 0)
        {
            warnings.Add(
                "No browser-calculated metrics were supplied. Explain inputs and assumptions only; do not invent IRR, EMI, cash flow or net profit.");
        }
        else
        {
            warnings.Add(
                "Calculated metrics were supplied by the Angular TypeScript engine as contextual values. They were not recalculated in C#.");
        }

        if (observations.Count == 0)
        {
            warnings.Add("No market observations were attached. Do not invent listings, rents or locality trends.");
        }
        else if (observations.All(item => item.IsSyntheticTestData))
        {
            warnings.Add(
                "Only synthetic test observations are available. There is no verified market conclusion. Do not present them as genuine Bengaluru evidence.");
        }
        else if (observations.Any(item => item.IsSyntheticTestData))
        {
            warnings.Add("Some attached observations are labelled synthetic and are not genuine market evidence.");
        }

        if (observations.Any(item => item.IsStale))
        {
            warnings.Add("Some attached observations are stale (observed more than 180 days ago).");
        }

        if (observations.Any(item => item.IsUnverified))
        {
            warnings.Add("Some attached observations are unverified.");
        }

        if (saved is null && calculated.Count == 0)
        {
            warnings.Add("No saved comparison or property context was supplied. Answer only with general methodology and the limitations above.");
        }

        return warnings;
    }

    private static object ToContextProperty(AnalystPropertyContextDto item) => new
    {
        item.Name,
        item.Locality,
        item.BuilderProject,
        item.PropertyType,
        item.PurchasePrice,
        item.SuperBuiltUpAreaSqFt,
        item.MonthlyRent,
        item.MonthlyMaintenance,
        item.DownPaymentPercent,
        item.AnnualInterestRatePercent,
        item.LoanTenureYears,
        item.AcquisitionCostPercent,
        item.HoldingPeriodYears,
        item.VacancyPercent,
        sourceOfTruth = "browser_typescript_engine",
        trust = "contextual_not_recalculated",
        snapshot = item.Snapshot,
        mortgage = item.Mortgage,
        conservative = item.Conservative,
        @base = item.Base,
        optimistic = item.Optimistic,
    };

    private static void ValidateProperty(
        AnalystPropertyContextDto property,
        string prefix,
        Dictionary<string, List<string>> errors)
    {
        Bound(property.PurchasePrice, prefix + ".purchasePrice", 0, MaxMoney, errors);
        Bound(property.SuperBuiltUpAreaSqFt, prefix + ".superBuiltUpAreaSqFt", 0, 1_000_000, errors);
        Bound(property.MonthlyRent, prefix + ".monthlyRent", 0, MaxMoney, errors);
        Bound(property.MonthlyMaintenance, prefix + ".monthlyMaintenance", 0, MaxMoney, errors);
        Bound(property.DownPaymentPercent, prefix + ".downPaymentPercent", 0, 100, errors);
        Bound(property.AnnualInterestRatePercent, prefix + ".annualInterestRatePercent", 0, MaxPercent, errors);
        Bound(property.LoanTenureYears, prefix + ".loanTenureYears", 0, 50, errors);
        Bound(property.AcquisitionCostPercent, prefix + ".acquisitionCostPercent", 0, MaxPercent, errors);
        if (property.HoldingPeriodYears is { } holding && (holding < 0 || holding > 80))
        {
            Add(errors, prefix + ".holdingPeriodYears", "Holding period is outside the allowed range.");
        }

        Bound(property.VacancyPercent, prefix + ".vacancyPercent", 0, 100, errors);
        ValidateSnapshot(property.Snapshot, prefix + ".snapshot", errors);
        ValidateMortgage(property.Mortgage, prefix + ".mortgage", errors);
        ValidateScenario(property.Conservative, prefix + ".conservative", errors);
        ValidateScenario(property.Base, prefix + ".base", errors);
        ValidateScenario(property.Optimistic, prefix + ".optimistic", errors);
    }

    private static void ValidateSnapshot(AnalystSnapshotMetricsDto? snapshot, string prefix, Dictionary<string, List<string>> errors)
    {
        if (snapshot is null)
        {
            return;
        }

        Bound(snapshot.PricePerSqFt, prefix + ".pricePerSqFt", 0, MaxMoney, errors);
        Bound(snapshot.GrossAnnualRentalYieldPercent, prefix + ".grossAnnualRentalYieldPercent", -100, MaxPercent, errors);
        Bound(snapshot.AnnualRentalIncome, prefix + ".annualRentalIncome", 0, MaxMoney, errors);
        Bound(snapshot.EstimatedAnnualMaintenance, prefix + ".estimatedAnnualMaintenance", 0, MaxMoney, errors);
        Bound(snapshot.NetRentalIncomeBeforeOtherExpenses, prefix + ".netRentalIncomeBeforeOtherExpenses", -MaxMoney, MaxMoney, errors);
    }

    private static void ValidateMortgage(AnalystMortgageMetricsDto? mortgage, string prefix, Dictionary<string, List<string>> errors)
    {
        if (mortgage is null)
        {
            return;
        }

        Bound(mortgage.DownPaymentAmount, prefix + ".downPaymentAmount", 0, MaxMoney, errors);
        Bound(mortgage.LoanPrincipal, prefix + ".loanPrincipal", 0, MaxMoney, errors);
        Bound(mortgage.MonthlyEmi, prefix + ".monthlyEmi", 0, MaxMoney, errors);
        Bound(mortgage.AnnualLoanPayments, prefix + ".annualLoanPayments", 0, MaxMoney, errors);
        Bound(mortgage.AcquisitionCostAmount, prefix + ".acquisitionCostAmount", 0, MaxMoney, errors);
        Bound(mortgage.TotalInitialAcquisitionCash, prefix + ".totalInitialAcquisitionCash", 0, MaxMoney, errors);
        Bound(mortgage.AnnualPotentialRent, prefix + ".annualPotentialRent", 0, MaxMoney, errors);
        Bound(mortgage.AnnualRentAfterVacancy, prefix + ".annualRentAfterVacancy", 0, MaxMoney, errors);
        Bound(mortgage.AnnualOperatingExpenses, prefix + ".annualOperatingExpenses", 0, MaxMoney, errors);
        Bound(mortgage.AnnualCashFlowAfterOperatingExpensesAndLoanPayments, prefix + ".annualCashFlowAfterOperatingExpensesAndLoanPayments", -MaxMoney, MaxMoney, errors);
        Bound(mortgage.OutstandingLoanBalanceAfterHoldingPeriod, prefix + ".outstandingLoanBalanceAfterHoldingPeriod", 0, MaxMoney, errors);
    }

    private static void ValidateScenario(AnalystScenarioMetricsDto? scenario, string prefix, Dictionary<string, List<string>> errors)
    {
        if (scenario is null)
        {
            return;
        }

        Bound(scenario.PropertyAppreciationPercent, prefix + ".propertyAppreciationPercent", -100, MaxPercent, errors);
        Bound(scenario.RentGrowthPercent, prefix + ".rentGrowthPercent", -100, MaxPercent, errors);
        Bound(scenario.VacancyPercent, prefix + ".vacancyPercent", 0, 100, errors);
        Bound(scenario.OperatingExpenseGrowthPercent, prefix + ".operatingExpenseGrowthPercent", -100, MaxPercent, errors);
        Bound(scenario.SellingCostPercent, prefix + ".sellingCostPercent", 0, 100, errors);
        Bound(scenario.InitialCashInvested, prefix + ".initialCashInvested", 0, MaxMoney, errors);
        Bound(scenario.NetProfit, prefix + ".netProfit", -MaxMoney, MaxMoney, errors);
        Bound(scenario.GrossPropertyAppreciationPercent, prefix + ".grossPropertyAppreciationPercent", -100, MaxPercent, errors);
        Bound(scenario.AnnualisedEquityIrr, prefix + ".annualisedEquityIrr", -100, MaxPercent, errors);
        Bound(scenario.NetSaleProceeds, prefix + ".netSaleProceeds", -MaxMoney, MaxMoney, errors);
    }

    private static void Bound(
        decimal? value,
        string key,
        decimal min,
        decimal max,
        Dictionary<string, List<string>> errors)
    {
        if (value is null)
        {
            return;
        }

        if (value < min || value > max)
        {
            Add(errors, key, "Value is outside the allowed range.");
        }
    }

    private static void Add(Dictionary<string, List<string>> errors, string key, string message)
    {
        if (!errors.TryGetValue(key, out var list))
        {
            list = [];
            errors[key] = list;
        }

        list.Add(message);
    }
}
