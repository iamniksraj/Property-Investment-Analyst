using PropertyInvestmentAnalyst.Api.Contracts;
using PropertyInvestmentAnalyst.Api.Domain;

namespace PropertyInvestmentAnalyst.Api.Services;

public static class MarketInsightsCalculator
{
    public const int MaxPageSize = 200;
    public const int DefaultPageSize = 50;
    public const int HistoryMinMonths = 3;
    public const int HistoryMinPerMonth = 3;

    public static MarketStatisticBucketDto BuildBucket(
        string recordType,
        IReadOnlyList<MarketObservationDto> matched,
        bool usingSynthetic,
        bool includeOutliers = false)
    {
        var exclusions = new List<ExclusionCountDto>();
        var outliers = matched.Where(item => item.IsOutlier).ToArray();
        if (outliers.Length > 0)
        {
            exclusions.Add(new ExclusionCountDto
            {
                Reason = "outlier",
                Count = outliers.Length,
                Explanation = includeOutliers
                    ? "Flagged by 1.5×IQR on amount within locality, configuration, record type and area definition. These rows are included in the statistics because includeOutliers is true."
                    : "Flagged by 1.5×IQR on amount within locality, configuration, record type and area definition. They remain listed in search results but are excluded from these statistics unless you include outliers.",
            });
        }

        var eligible = includeOutliers ? matched.ToArray() : matched.Where(item => !item.IsOutlier).ToArray();
        var mixedArea = eligible.Select(item => $"{item.AreaType}|{item.AreaUnit}").Distinct().Count() > 1;
        var amounts = eligible.Select(item => item.Amount).ToArray();
        var salePsf = eligible.Where(item => item.PricePerSqFt is not null).Select(item => item.PricePerSqFt!.Value).ToArray();
        var rentPsf = eligible.Where(item => item.MonthlyRentPerSqFt is not null).Select(item => item.MonthlyRentPerSqFt!.Value).ToArray();
        var published = eligible.Length >= MarketObservation.MinimumSampleForPublishedMedian;
        var indicative = eligible.Length >= MarketObservation.MinimumSampleForIndicativeMedian;
        var quartiles = MarketObservationMapper.Quartiles(amounts);

        if (mixedArea)
        {
            exclusions.Add(new ExclusionCountDto
            {
                Reason = "incompatible_area_for_psf",
                Count = eligible.Length,
                Explanation = "Amount statistics may still be shown. Price/rent per sq. ft. is omitted because area types or units are not the same.",
            });
        }

        return new MarketStatisticBucketDto
        {
            RecordType = recordType,
            MatchedCount = matched.Count,
            SampleSize = eligible.Length,
            ExcludedOutliers = outliers.Length,
            IsSparse = !published,
            InsufficientData = eligible.Length == 0,
            MixedAreaDefinitions = mixedArea,
            OldestObservedOn = eligible.Length == 0 ? null : eligible.Min(item => item.ObservedOn),
            NewestObservedOn = eligible.Length == 0 ? null : eligible.Max(item => item.ObservedOn),
            MinAmount = amounts.Length == 0 ? null : amounts.Min(),
            MaxAmount = amounts.Length == 0 ? null : amounts.Max(),
            MedianAmount = indicative ? MarketObservationMapper.Median(amounts) : null,
            Percentile25 = quartiles?.Q1,
            Percentile75 = quartiles?.Q3,
            MedianPricePerSqFt = mixedArea ? null : MarketObservationMapper.Median(salePsf),
            MedianMonthlyRentPerSqFt = mixedArea ? null : MarketObservationMapper.Median(rentPsf),
            Exclusions = exclusions,
            Quality = BuildQuality(eligible, outliers.Length, mixedArea, usingSynthetic, includeOutliers),
            Label = BucketLabel(recordType, published, usingSynthetic),
            Methodology = mixedArea
                ? "Amounts may be listed, but price/rent per sq. ft. is omitted because area definitions in this sample are not the same."
                : "Per-sq-ft figures use super built-up area in square feet only. Outliers flagged by 1.5×IQR on amount are excluded from these statistics.",
        };
    }

    public static HistorySeriesDto BuildHistory(IReadOnlyList<MarketObservationDto> eligible, bool usingSynthetic)
    {
        if (usingSynthetic)
        {
            return BuildHistorySeries(
                eligible,
                usingSynthetic: true,
                sufficient: false,
                "Synthetic observation dates may be plotted as a fixture timeline. They are not Bengaluru market history.");
        }

        var months = eligible
            .GroupBy(item => new DateOnly(item.ObservedOn.Year, item.ObservedOn.Month, 1))
            .Select(group => group.Key)
            .OrderBy(item => item)
            .ToArray();
        if (months.Length == 0)
        {
            return BuildHistorySeries([], false, false,
                "No eligible observations remain after filters, so no history interval can be formed.");
        }

        var start = months[0];
        var end = months[^1];
        var points = new List<HistoryPointDto>();
        for (var cursor = start; cursor <= end; cursor = cursor.AddMonths(1))
        {
            var monthRows = eligible.Where(item => item.ObservedOn.Year == cursor.Year && item.ObservedOn.Month == cursor.Month).ToArray();
            var enough = monthRows.Length >= HistoryMinPerMonth;
            points.Add(new HistoryPointDto
            {
                YearMonth = $"{cursor:yyyy-MM}",
                Count = monthRows.Length,
                MedianAmount = enough ? MarketObservationMapper.Median(monthRows.Select(item => item.Amount).ToArray()) : null,
                InsufficientMonth = !enough,
            });
        }

        var adequateMonths = points.Count(item => !item.InsufficientMonth);
        var sufficient = adequateMonths >= HistoryMinMonths;
        var reason = sufficient
            ? $"Monthly counts for one record type after comparability filters. A month needs at least {HistoryMinPerMonth} observations before a median is shown. Empty months are gaps, not interpolated values."
            : $"Insufficient history: need at least {HistoryMinMonths} months with {HistoryMinPerMonth} comparable observations each. Counts are shown; medians are omitted for thin months. No values are interpolated.";

        return new HistorySeriesDto
        {
            Interval = "month",
            Sufficient = sufficient,
            UsingSyntheticTestData = false,
            Reason = reason,
            Points = points,
        };
    }

    public static AssumptionComparisonResponse CompareAssumption(
        AssumptionComparisonRequest request,
        IReadOnlyList<MarketObservationDto> askingEligible,
        IReadOnlyList<MarketObservationDto> rentEligible,
        bool usingSynthetic)
    {
        var asking = CompareMetric(
            MarketObservation.RecordTypeAskingPrice,
            request.PurchasePrice,
            request.AreaValue,
            request.AreaType,
            request.AreaUnit,
            request.AcknowledgeIncompatibleArea,
            askingEligible);
        var rent = CompareMetric(
            MarketObservation.RecordTypeAdvertisedRent,
            request.MonthlyRent,
            request.AreaValue,
            request.AreaType,
            request.AreaUnit,
            request.AcknowledgeIncompatibleArea,
            rentEligible);

        return new AssumptionComparisonResponse
        {
            UsingSyntheticTestData = usingSynthetic,
            Disclaimer = usingSynthetic
                ? "These differences use labelled synthetic test observations. They are not evidence that a property is undervalued or overpriced, and they are not a valuation."
                : "A difference from the median asking price or advertised rent is not proof that a property is undervalued or overpriced. It is not a formal valuation and is separate from scenario net profit and IRR.",
            AskingPrice = asking,
            AdvertisedRent = rent,
        };
    }

    private static AssumptionMetricDto CompareMetric(
        string recordType,
        decimal? userAmount,
        decimal? areaValue,
        string? areaType,
        string? areaUnit,
        bool acknowledgeIncompatible,
        IReadOnlyList<MarketObservationDto> eligible)
    {
        var limitations = new List<string>();
        if (userAmount is null or <= 0)
        {
            return new AssumptionMetricDto
            {
                RecordType = recordType,
                Compared = false,
                Status = "no_user_amount",
                Limitations = ["No matching user amount was supplied for this record type."],
                IncompatibleArea = false,
            };
        }

        if (eligible.Count < MarketObservation.MinimumSampleForIndicativeMedian)
        {
            return new AssumptionMetricDto
            {
                RecordType = recordType,
                Compared = false,
                Status = "insufficient_observations",
                UserAmount = userAmount,
                ComparableCount = eligible.Count,
                Limitations = [$"Need at least {MarketObservation.MinimumSampleForIndicativeMedian} comparable observations after filters."],
                IncompatibleArea = false,
            };
        }

        var userArea = Normalise(areaType);
        var userUnit = Normalise(areaUnit);
        var areaKeys = eligible.Select(item => $"{item.AreaType}|{item.AreaUnit}").Distinct().ToArray();
        var mixed = areaKeys.Length > 1;
        var sampleArea = areaKeys[0].Split('|')[0];
        var sampleUnit = areaKeys[0].Split('|')[1];
        var incompatible = mixed
            || (!string.IsNullOrWhiteSpace(userArea) && userArea != sampleArea)
            || (!string.IsNullOrWhiteSpace(userUnit) && userUnit != sampleUnit);

        if (incompatible && !acknowledgeIncompatible)
        {
            return new AssumptionMetricDto
            {
                RecordType = recordType,
                Compared = false,
                Status = "needs_area_acknowledgement",
                UserAmount = userAmount,
                ComparableCount = eligible.Count,
                IncompatibleArea = true,
                Limitations =
                [
                    "The user area definition does not match the observation sample, or the sample mixes area types. Confirm that you want to compare amounts anyway. Price per sq. ft. will not be compared across incompatible areas.",
                ],
            };
        }

        if (incompatible)
        {
            limitations.Add("Area definitions differ or are mixed. Amounts are compared with that caveat. Per-sq-ft figures are omitted.");
        }

        var amounts = eligible.Select(item => item.Amount).ToArray();
        var median = MarketObservationMapper.Median(amounts);
        var quartiles = MarketObservationMapper.Quartiles(amounts);
        decimal? userPsf = null;
        decimal? medianPsf = null;
        if (!incompatible
            && userArea == MarketObservation.AreaSuperBuiltUp
            && userUnit == MarketObservation.UnitSqFt
            && areaValue is > 0)
        {
            userPsf = decimal.Round(userAmount.Value / areaValue.Value, 2, MidpointRounding.AwayFromZero);
            var psfValues = recordType == MarketObservation.RecordTypeAdvertisedRent
                ? eligible.Where(item => item.MonthlyRentPerSqFt is not null).Select(item => item.MonthlyRentPerSqFt!.Value).ToArray()
                : eligible.Where(item => item.PricePerSqFt is not null).Select(item => item.PricePerSqFt!.Value).ToArray();
            medianPsf = MarketObservationMapper.Median(psfValues);
        }
        else if (!incompatible && (areaValue is null or <= 0 || userArea != MarketObservation.AreaSuperBuiltUp || userUnit != MarketObservation.UnitSqFt))
        {
            limitations.Add("Price per sq. ft. was not calculated because area is missing or is not super built-up square feet.");
        }

        limitations.Add("This comparison is not an investment recommendation and is independent of net profit and IRR.");

        return new AssumptionMetricDto
        {
            RecordType = recordType,
            Compared = true,
            Status = "compared",
            ComparableCount = eligible.Count,
            UserAmount = userAmount,
            UserPerSqFt = userPsf,
            MedianAmount = median,
            Percentile25 = quartiles?.Q1,
            Percentile75 = quartiles?.Q3,
            MedianPerSqFt = medianPsf,
            AmountDifference = median is null ? null : userAmount - median,
            AmountDifferencePercent = PercentDelta(userAmount, median),
            PerSqFtDifference = userPsf is null || medianPsf is null ? null : userPsf - medianPsf,
            PerSqFtDifferencePercent = PercentDelta(userPsf, medianPsf),
            OldestObservedOn = eligible.Min(item => item.ObservedOn),
            NewestObservedOn = eligible.Max(item => item.ObservedOn),
            IncompatibleArea = incompatible,
            Limitations = limitations,
        };
    }

    public static EvidenceQualityDto BuildQuality(
        IReadOnlyList<MarketObservationDto> eligible,
        int excludedOutliers,
        bool mixedArea,
        bool usingSynthetic,
        bool includeOutliers = false)
    {
        var notes = new List<string>();
        var sample = eligible.Count >= MarketObservation.MinimumSampleForPublishedMedian
            ? "adequate"
            : eligible.Count >= MarketObservation.MinimumSampleForIndicativeMedian
                ? "sparse"
                : "insufficient";
        var staleShare = eligible.Count == 0 ? 0 : eligible.Count(item => item.IsStale) / (decimal)eligible.Count;
        var recency = eligible.Count == 0
            ? "unknown"
            : staleShare >= 0.5m
                ? "stale_majority"
                : staleShare > 0
                    ? "mixed"
                    : "recent";
        var unverifiedShare = eligible.Count == 0 ? 0 : eligible.Count(item => item.IsUnverified) / (decimal)eligible.Count;
        var verification = eligible.Count == 0
            ? "unknown"
            : unverifiedShare >= 0.5m
                ? "unverified_majority"
                : unverifiedShare > 0
                    ? "mixed"
                    : "cited_or_reviewed";
        var area = eligible.Count == 0
            ? "unknown"
            : mixedArea
                ? "mixed"
                : eligible[0].AreaType == MarketObservation.AreaSuperBuiltUp && eligible[0].AreaUnit == MarketObservation.UnitSqFt
                    ? "comparable_super_built_up_sq_ft"
                    : "comparable_other_definition";
        var incomplete = eligible.Count(item => !item.HasCompleteSource);
        var source = eligible.Count == 0
            ? "unknown"
            : incomplete * 2 >= eligible.Count
                ? "incomplete_majority"
                : incomplete > 0
                    ? "mixed"
                    : "complete";
        if (usingSynthetic)
        {
            notes.Add("Every statistic in this view is built from labelled synthetic test data. No genuine market conclusion can be drawn.");
        }

        notes.Add($"Stale means observed more than {MarketObservation.StaleAfterDays} days ago. That cut-off is a product setting, not a market standard.");
        notes.Add("`source_cited` only means a source name and reference were stored. It does not prove the row is a genuine listing.");

        return new EvidenceQualityDto
        {
            SampleSize = sample,
            Recency = recency,
            Verification = verification,
            AreaComparability = area,
            SourceCompleteness = source,
            Synthetic = usingSynthetic ? "synthetic_only" : "genuine_only",
            Outliers = excludedOutliers == 0
                ? "none_flagged"
                : includeOutliers
                    ? "flagged_and_included_in_statistics"
                    : "flagged_and_excluded_from_statistics",
            Notes = notes,
        };
    }

    private static HistorySeriesDto BuildHistorySeries(
        IReadOnlyList<MarketObservationDto> eligible,
        bool usingSynthetic,
        bool sufficient,
        string reason)
    {
        HistoryPointDto[] points = [];
        if (eligible.Count > 0)
        {
            var months = eligible.Select(item => new DateOnly(item.ObservedOn.Year, item.ObservedOn.Month, 1)).Distinct().OrderBy(item => item).ToArray();
            var start = months[0];
            var end = months[^1];
            var list = new List<HistoryPointDto>();
            for (var cursor = start; cursor <= end; cursor = cursor.AddMonths(1))
            {
                var count = eligible.Count(item => item.ObservedOn.Year == cursor.Year && item.ObservedOn.Month == cursor.Month);
                list.Add(new HistoryPointDto
                {
                    YearMonth = $"{cursor:yyyy-MM}",
                    Count = count,
                    MedianAmount = null,
                    InsufficientMonth = true,
                });
            }

            points = [.. list];
        }

        return new HistorySeriesDto
        {
            Interval = "month",
            Sufficient = sufficient,
            UsingSyntheticTestData = usingSynthetic,
            Reason = reason,
            Points = points,
        };
    }

    private static string BucketLabel(string recordType, bool published, bool synthetic)
    {
        var suffix = synthetic ? " — synthetic test data, not a market conclusion" : "";
        return recordType switch
        {
            MarketObservation.RecordTypeAskingPrice => (published
                ? "Median asking price (not a completed transaction)"
                : "Asking-price sample is too small to publish as a median") + suffix,
            MarketObservation.RecordTypeAdvertisedRent => (published
                ? "Median advertised monthly rent (not a signed lease)"
                : "Advertised-rent sample is too small to publish as a median") + suffix,
            MarketObservation.RecordTypeVerifiedTransaction => (published
                ? "Median verified transaction price"
                : "Verified-transaction sample is too small to publish as a median") + suffix,
            _ => "Other observations" + suffix,
        };
    }

    private static decimal? PercentDelta(decimal? user, decimal? median)
    {
        if (user is null || median is null or 0)
        {
            return null;
        }

        return decimal.Round((user.Value - median.Value) / median.Value * 100m, 2, MidpointRounding.AwayFromZero);
    }

    private static string Normalise(string? value) => (value ?? string.Empty).Trim().ToLowerInvariant();
}
