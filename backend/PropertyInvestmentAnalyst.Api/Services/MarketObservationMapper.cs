using System.Globalization;
using System.Text;
using PropertyInvestmentAnalyst.Api.Contracts;
using PropertyInvestmentAnalyst.Api.Domain;

namespace PropertyInvestmentAnalyst.Api.Services;

public static class MarketObservationMapper
{
    public static MarketObservationDto ToDto(MarketObservation item, DateOnly today)
    {
        var staleOn = today.AddDays(-MarketObservation.StaleAfterDays);
        return new MarketObservationDto
        {
            Id = item.Id,
            Locality = item.Locality,
            BuilderProject = item.BuilderProject,
            PropertyType = item.PropertyType,
            Configuration = item.Configuration,
            RecordType = item.RecordType,
            VerificationStatus = item.VerificationStatus,
            Amount = item.Amount,
            AreaValue = item.AreaValue,
            AreaType = item.AreaType,
            AreaUnit = item.AreaUnit,
            PricePerSqFt = item.PricePerSqFt,
            MonthlyRentPerSqFt = item.MonthlyRentPerSqFt,
            SourceName = item.SourceName,
            SourceUrl = item.SourceUrl,
            SourceRecordId = item.SourceRecordId,
            ObservedOn = item.ObservedOn,
            ImportedUtc = item.ImportedUtc,
            QualityNotes = item.QualityNotes,
            LicenceOrUsageNotes = item.LicenceOrUsageNotes,
            IsSyntheticTestData = item.IsSyntheticTestData,
            IsOutlier = item.IsOutlier,
            IsStale = item.ObservedOn < staleOn,
            IsUnverified = item.VerificationStatus == MarketObservation.VerificationUnverified,
            HasCompleteSource = HasCompleteSource(item),
            ProviderKey = item.ProviderKey,
        };
    }

    public static MarketObservation ToEntity(SaveMarketObservationRequest request, DateTimeOffset importedUtc)
    {
        var observation = new MarketObservation
        {
            Id = Guid.NewGuid(),
            Locality = request.Locality.Trim(),
            BuilderProject = request.BuilderProject.Trim(),
            PropertyType = string.IsNullOrWhiteSpace(request.PropertyType) ? "Apartment" : request.PropertyType.Trim(),
            Configuration = request.Configuration.Trim(),
            RecordType = request.RecordType.Trim().ToLowerInvariant(),
            VerificationStatus = ResolveVerification(request),
            Amount = request.Amount,
            AreaValue = request.AreaValue,
            AreaType = string.IsNullOrWhiteSpace(request.AreaType)
                ? MarketObservation.AreaUnspecified
                : request.AreaType.Trim().ToLowerInvariant(),
            AreaUnit = string.IsNullOrWhiteSpace(request.AreaUnit)
                ? MarketObservation.UnitSqFt
                : request.AreaUnit.Trim().ToLowerInvariant(),
            SourceName = request.SourceName.Trim(),
            SourceUrl = TrimToNull(request.SourceUrl),
            SourceRecordId = TrimToNull(request.SourceRecordId),
            ObservedOn = request.ObservedOn,
            ImportedUtc = importedUtc,
            QualityNotes = TrimToNull(request.QualityNotes),
            LicenceOrUsageNotes = TrimToNull(request.LicenceOrUsageNotes),
            IsSyntheticTestData = request.IsSyntheticTestData,
            ProviderKey = TrimToNull(request.ProviderKey),
            DuplicateKey = "",
        };

        ApplyDerived(observation);
        return observation;
    }

    public static void ApplyDerived(MarketObservation observation)
    {
        observation.PricePerSqFt = null;
        observation.MonthlyRentPerSqFt = null;
        if (observation.AreaValue is > 0
            && observation.AreaType == MarketObservation.AreaSuperBuiltUp
            && observation.AreaUnit == MarketObservation.UnitSqFt)
        {
            var perSqFt = decimal.Round(observation.Amount / observation.AreaValue.Value, 2, MidpointRounding.AwayFromZero);
            if (observation.RecordType is MarketObservation.RecordTypeAskingPrice or MarketObservation.RecordTypeVerifiedTransaction)
            {
                observation.PricePerSqFt = perSqFt;
            }
            else if (observation.RecordType == MarketObservation.RecordTypeAdvertisedRent)
            {
                observation.MonthlyRentPerSqFt = perSqFt;
            }
        }

        observation.DuplicateKey = DuplicateKey(
            observation.SourceName,
            observation.SourceRecordId,
            observation.Locality,
            observation.BuilderProject,
            observation.Configuration,
            observation.RecordType,
            observation.Amount,
            observation.ObservedOn);
    }

    public static string DuplicateKey(
        string sourceName,
        string? sourceRecordId,
        string locality,
        string builderProject,
        string configuration,
        string recordType,
        decimal amount,
        DateOnly observedOn)
    {
        var raw = string.Join(
            '|',
            Normalize(sourceName),
            Normalize(sourceRecordId),
            Normalize(locality),
            Normalize(builderProject),
            Normalize(configuration),
            recordType.Trim().ToLowerInvariant(),
            amount.ToString("0.##", CultureInfo.InvariantCulture),
            observedOn.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
        return Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(Encoding.UTF8.GetBytes(raw)));
    }

    public static decimal? Median(IReadOnlyList<decimal> values)
    {
        if (values.Count == 0)
        {
            return null;
        }

        var sorted = values.OrderBy(item => item).ToArray();
        var mid = sorted.Length / 2;
        if (sorted.Length % 2 == 1)
        {
            return sorted[mid];
        }

        return decimal.Round((sorted[mid - 1] + sorted[mid]) / 2m, 2, MidpointRounding.AwayFromZero);
    }

    public static (decimal Q1, decimal Q3)? Quartiles(IReadOnlyList<decimal> values)
    {
        if (values.Count < 5)
        {
            return null;
        }

        var sorted = values.OrderBy(item => item).ToArray();
        var q1 = Percentile(sorted, 0.25m);
        var q3 = Percentile(sorted, 0.75m);
        return (q1, q3);
    }

    public static bool IsOutlierAmount(decimal amount, IReadOnlyList<decimal> comparableAmounts)
    {
        var quartiles = Quartiles(comparableAmounts);
        if (quartiles is null)
        {
            return false;
        }

        var iqr = quartiles.Value.Q3 - quartiles.Value.Q1;
        var low = quartiles.Value.Q1 - (1.5m * iqr);
        var high = quartiles.Value.Q3 + (1.5m * iqr);
        return amount < low || amount > high;
    }

    public static bool HasCompleteSource(MarketObservation item) =>
        !string.IsNullOrWhiteSpace(item.SourceName)
        && (!string.IsNullOrWhiteSpace(item.SourceUrl) || !string.IsNullOrWhiteSpace(item.SourceRecordId));

    public static decimal Percentile(IReadOnlyList<decimal> sorted, decimal percentile)
    {
        var index = (sorted.Count - 1) * percentile;
        var low = (int)Math.Floor(index);
        var high = (int)Math.Ceiling(index);
        if (low == high)
        {
            return sorted[low];
        }

        var weight = index - low;
        return sorted[low] + ((sorted[high] - sorted[low]) * weight);
    }

    private static string ResolveVerification(SaveMarketObservationRequest request)
    {
        if (!string.IsNullOrWhiteSpace(request.VerificationStatus))
        {
            return request.VerificationStatus.Trim().ToLowerInvariant();
        }

        return !string.IsNullOrWhiteSpace(request.SourceUrl) || !string.IsNullOrWhiteSpace(request.SourceRecordId)
            ? MarketObservation.VerificationSourceCited
            : MarketObservation.VerificationUnverified;
    }

    private static string Normalize(string? value) => (value ?? string.Empty).Trim().ToLowerInvariant();

    private static string? TrimToNull(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
