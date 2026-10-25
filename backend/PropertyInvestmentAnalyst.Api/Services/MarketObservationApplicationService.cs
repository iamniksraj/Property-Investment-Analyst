using Microsoft.EntityFrameworkCore;
using PropertyInvestmentAnalyst.Api.Contracts;
using PropertyInvestmentAnalyst.Api.Data;
using PropertyInvestmentAnalyst.Api.Domain;

namespace PropertyInvestmentAnalyst.Api.Services;

public sealed class MarketObservationApplicationService(AppDbContext db, MarketIngestionService ingestion)
{
    public async Task<MarketSearchResponse> SearchAsync(
        string locality,
        string? propertyType,
        string? configuration,
        string? recordType,
        CancellationToken cancellationToken,
        bool includeSynthetic = true,
        bool includeOutliers = true,
        string? areaType = null,
        string? areaUnit = null,
        string? verificationStatus = null,
        DateOnly? observedFrom = null,
        DateOnly? observedTo = null,
        int skip = 0,
        int take = MarketInsightsCalculator.DefaultPageSize) =>
        await SearchAsync(new MarketFilter
        {
            Locality = locality,
            PropertyType = propertyType,
            Configuration = configuration,
            RecordType = recordType,
            AreaType = areaType,
            AreaUnit = areaUnit,
            VerificationStatus = verificationStatus,
            IncludeSynthetic = includeSynthetic,
            IncludeOutliers = includeOutliers,
            ObservedFrom = observedFrom,
            ObservedTo = observedTo,
            Skip = skip,
            Take = take,
        }, cancellationToken);

    public async Task<MarketSearchResponse> SearchAsync(MarketFilter filter, CancellationToken cancellationToken)
    {
        ValidateFilter(filter, requireLocality: true);
        var take = filter.Take <= 0 ? MarketInsightsCalculator.DefaultPageSize : Math.Min(filter.Take, MarketInsightsCalculator.MaxPageSize);
        var skip = Math.Max(0, filter.Skip);

        var query = Filtered(filter);
        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .AsNoTracking()
            .OrderByDescending(item => item.ObservedOn)
            .ThenBy(item => item.BuilderProject)
            .Skip(skip)
            .Take(take)
            .ToListAsync(cancellationToken);

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var dtos = items.Select(item => MarketObservationMapper.ToDto(item, today)).ToArray();
        var syntheticCount = await Filtered(Clone(filter, includeSynthetic: true, includeOutliers: true))
            .CountAsync(item => item.IsSyntheticTestData, cancellationToken);
        var genuineCount = await Filtered(Clone(filter, includeSynthetic: true, includeOutliers: true))
            .CountAsync(item => !item.IsSyntheticTestData, cancellationToken);

        return new MarketSearchResponse
        {
            Items = dtos,
            Count = dtos.Length,
            TotalCount = total,
            Skip = skip,
            Take = take,
            OldestObservedOn = dtos.Length == 0 ? null : dtos.Min(item => item.ObservedOn),
            NewestObservedOn = dtos.Length == 0 ? null : dtos.Max(item => item.ObservedOn),
            IsSparse = total < MarketObservation.MinimumSampleForPublishedMedian,
            ContainsStale = dtos.Any(item => item.IsStale),
            ContainsUnverified = dtos.Any(item => item.IsUnverified),
            ContainsSynthetic = dtos.Any(item => item.IsSyntheticTestData),
            OnlySyntheticAvailable = genuineCount == 0 && syntheticCount > 0,
        };
    }

    public async Task<MarketObservationDto> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        var item = await db.MarketObservations.AsNoTracking()
            .FirstOrDefaultAsync(row => row.Id == id, cancellationToken)
            ?? throw new MarketNotFoundException(id);
        return MarketObservationMapper.ToDto(item, DateOnly.FromDateTime(DateTime.UtcNow));
    }

    public async Task<MarketObservationDto> CreateAsync(
        SaveMarketObservationRequest request,
        CancellationToken cancellationToken)
    {
        MarketObservationValidator.Validate(request);
        request.ProviderKey ??= "manual";
        var entity = MarketObservationMapper.ToEntity(request, DateTimeOffset.UtcNow);
        var existingKeys = await db.MarketObservations
            .AsNoTracking()
            .Select(item => item.DuplicateKey)
            .ToListAsync(cancellationToken);
        if (existingKeys.Contains(entity.DuplicateKey, StringComparer.OrdinalIgnoreCase))
        {
            throw new MarketValidationException(new Dictionary<string, string[]>
            {
                ["duplicate"] = ["An observation with the same source, listing identity, amount and observation date already exists. Existing records were not overwritten."],
            });
        }

        var comparable = await ComparableAmountsAsync(entity, cancellationToken);
        entity.IsOutlier = MarketObservationMapper.IsOutlierAmount(entity.Amount, comparable);
        db.MarketObservations.Add(entity);
        await db.SaveChangesAsync(cancellationToken);
        return MarketObservationMapper.ToDto(entity, DateOnly.FromDateTime(DateTime.UtcNow));
    }

    public async Task<MarketStatsResponse> StatsAsync(
        string locality,
        string? propertyType,
        string? configuration,
        CancellationToken cancellationToken,
        bool includeSynthetic = false,
        bool includeOutliers = false,
        string? areaType = null,
        string? areaUnit = null,
        string? verificationStatus = null,
        DateOnly? observedFrom = null,
        DateOnly? observedTo = null) =>
        await StatsAsync(new MarketFilter
        {
            Locality = locality,
            PropertyType = propertyType,
            Configuration = configuration,
            AreaType = areaType,
            AreaUnit = areaUnit,
            VerificationStatus = verificationStatus,
            IncludeSynthetic = includeSynthetic,
            IncludeOutliers = includeOutliers,
            ObservedFrom = observedFrom,
            ObservedTo = observedTo,
            Take = MarketInsightsCalculator.MaxPageSize,
        }, cancellationToken);

    public async Task<MarketStatsResponse> StatsAsync(MarketFilter filter, CancellationToken cancellationToken)
    {
        ValidateFilter(filter, requireLocality: true);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var (cohort, usingSynthetic, genuineCount, syntheticCount) = await LoadCohortAsync(filter, cancellationToken);
        var dtos = cohort.Select(item => MarketObservationMapper.ToDto(item, today)).ToArray();

        var buckets = MarketObservation.RecordTypes
            .Where(type => type != MarketObservation.RecordTypeOther)
            .Select(type => MarketInsightsCalculator.BuildBucket(
                type,
                dtos.Where(item => item.RecordType == type).ToArray(),
                usingSynthetic,
                filter.IncludeOutliers))
            .Where(bucket => bucket.MatchedCount > 0)
            .ToArray();

        var historySource = dtos
            .Where(item => item.RecordType == MarketObservation.RecordTypeAskingPrice)
            .ToArray();
        if (historySource.Length == 0)
        {
            historySource = dtos.Where(item => item.RecordType == MarketObservation.RecordTypeAdvertisedRent).ToArray();
        }

        var historyEligible = filter.IncludeOutliers
            ? historySource
            : historySource.Where(item => !item.IsOutlier).ToArray();

        return new MarketStatsResponse
        {
            Locality = filter.Locality.Trim(),
            PropertyType = BlankToNull(filter.PropertyType),
            Configuration = BlankToNull(filter.Configuration),
            IncludeSynthetic = filter.IncludeSynthetic,
            OnlySyntheticAvailable = genuineCount == 0 && syntheticCount > 0,
            UsingSyntheticTestData = usingSynthetic,
            GenuineCount = genuineCount,
            SyntheticCount = syntheticCount,
            Buckets = buckets,
            History = MarketInsightsCalculator.BuildHistory(historyEligible, usingSynthetic),
            Notes = usingSynthetic
                ? "Every figure below is from labelled synthetic test observations. No genuine market conclusion can be drawn. Asking prices, advertised rents and verified transactions remain in separate buckets."
                : "Medians are computed separately for asking prices, advertised rents and verified transactions. "
                  + "Price per sq. ft. uses super built-up area in sq. ft. only. Carpet, built-up and plot areas are never converted. "
                  + $"A published median requires at least {MarketObservation.MinimumSampleForPublishedMedian} comparable non-outlier records. "
                  + "Synthetic rows are never mixed into a genuine statistic. This is not a live Bengaluru market feed.",
        };
    }

    public async Task<AssumptionComparisonResponse> CompareAssumptionAsync(
        AssumptionComparisonRequest request,
        CancellationToken cancellationToken)
    {
        var filter = new MarketFilter
        {
            Locality = request.Locality,
            PropertyType = request.PropertyType,
            Configuration = request.Configuration,
            IncludeSynthetic = request.IncludeSynthetic,
            IncludeOutliers = false,
            AreaType = request.AcknowledgeIncompatibleArea ? null : request.AreaType,
            AreaUnit = request.AcknowledgeIncompatibleArea ? null : request.AreaUnit,
            Take = MarketInsightsCalculator.MaxPageSize,
        };
        ValidateFilter(filter, requireLocality: true);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var (cohort, usingSynthetic, _, _) = await LoadCohortAsync(filter, cancellationToken);
        var dtos = cohort.Select(item => MarketObservationMapper.ToDto(item, today)).Where(item => !item.IsOutlier).ToArray();
        return MarketInsightsCalculator.CompareAssumption(
            request,
            dtos.Where(item => item.RecordType == MarketObservation.RecordTypeAskingPrice).ToArray(),
            dtos.Where(item => item.RecordType == MarketObservation.RecordTypeAdvertisedRent).ToArray(),
            usingSynthetic);
    }

    public async Task<MarketImportResult> PreviewAsync(string csv, CancellationToken cancellationToken) =>
        await ClassifyAsync(csv, commit: false, cancellationToken);

    public async Task<MarketImportResult> CommitAsync(string csv, CancellationToken cancellationToken) =>
        await ClassifyAsync(csv, commit: true, cancellationToken);

    private async Task<MarketImportResult> ClassifyAsync(string csv, bool commit, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(csv))
        {
            throw new MarketValidationException(new Dictionary<string, string[]>
            {
                ["file"] = ["A CSV body is required."],
            });
        }

        var parsed = MarketCsv.Parse(csv);
        if (parsed.Count == 0)
        {
            throw new MarketValidationException(new Dictionary<string, string[]>
            {
                ["file"] = ["The CSV is empty."],
            });
        }

        var headers = MarketCsv.HeaderMap(parsed[0]);
        var missing = MarketCsv.RequiredHeaders.Where(name => !headers.ContainsKey(name)).ToArray();
        if (missing.Length > 0)
        {
            throw new MarketValidationException(new Dictionary<string, string[]>
            {
                ["headers"] = [$"Missing CSV columns: {string.Join(", ", missing)}."],
            });
        }

        var dataRows = parsed.Skip(1).ToList();
        if (dataRows.Count > MarketCsv.MaxRows)
        {
            throw new MarketValidationException(new Dictionary<string, string[]>
            {
                ["file"] = [$"CSV imports are limited to {MarketCsv.MaxRows} data rows."],
            });
        }

        var requests = dataRows.Select(row => ToRequest(headers, row)).ToArray();
        foreach (var request in requests)
        {
            request.ProviderKey = "csv_file";
        }

        var result = await ingestion.IngestAsync(requests, "csv_file", commit, cancellationToken);
        if (commit)
        {
            db.MarketImportRuns.Add(new MarketImportRun
            {
                Id = Guid.NewGuid(),
                ProviderKey = "csv_file",
                Status = result.Invalid > 0 && result.Inserted == 0 ? "completed_with_errors" : "success",
                StartedUtc = DateTimeOffset.UtcNow,
                CompletedUtc = DateTimeOffset.UtcNow,
                Inserted = result.Inserted,
                Updated = result.Updated,
                Skipped = result.Skipped,
                Duplicates = result.Duplicates,
                Invalid = result.Invalid,
                ErrorCount = result.Invalid,
                Summary = $"CSV commit: {result.Inserted} inserted, {result.Duplicates} duplicates, {result.Skipped} skipped, {result.Invalid} invalid. {MarketIngestionService.Policy}",
            });
            await db.SaveChangesAsync(cancellationToken);
        }

        return result;
    }

    public static SaveMarketObservationRequest ToRequest(IReadOnlyDictionary<string, int> headers, string[] row)
    {
        var areaValueText = MarketCsv.Cell(headers, row, "areaValue");
        decimal? areaValue = null;
        if (areaValueText is not null && MarketCsv.TryDecimal(areaValueText, out var parsedArea))
        {
            areaValue = parsedArea;
        }

        MarketCsv.TryDecimal(MarketCsv.Cell(headers, row, "amount"), out var amount);
        MarketCsv.TryDate(MarketCsv.Cell(headers, row, "observedOn"), out var observedOn);
        MarketCsv.TryBool(MarketCsv.Cell(headers, row, "isSyntheticTestData"), out var synthetic);

        return new SaveMarketObservationRequest
        {
            Locality = MarketCsv.Cell(headers, row, "locality") ?? "",
            BuilderProject = MarketCsv.Cell(headers, row, "builderProject") ?? "",
            PropertyType = MarketCsv.Cell(headers, row, "propertyType") ?? "Apartment",
            Configuration = MarketCsv.Cell(headers, row, "configuration") ?? "",
            RecordType = MarketCsv.Cell(headers, row, "recordType") ?? "",
            Amount = amount,
            AreaValue = areaValue,
            AreaType = MarketCsv.Cell(headers, row, "areaType") ?? MarketObservation.AreaUnspecified,
            AreaUnit = MarketCsv.Cell(headers, row, "areaUnit") ?? MarketObservation.UnitSqFt,
            SourceName = MarketCsv.Cell(headers, row, "sourceName") ?? "",
            SourceUrl = MarketCsv.Cell(headers, row, "sourceUrl"),
            SourceRecordId = MarketCsv.Cell(headers, row, "sourceRecordId"),
            ObservedOn = observedOn,
            VerificationStatus = MarketCsv.Cell(headers, row, "verificationStatus"),
            QualityNotes = MarketCsv.Cell(headers, row, "qualityNotes"),
            LicenceOrUsageNotes = MarketCsv.Cell(headers, row, "licenceOrUsageNotes"),
            IsSyntheticTestData = synthetic,
            ProviderKey = "csv_file",
        };
    }

    private IQueryable<MarketObservation> Filtered(MarketFilter filter)
    {
        var locality = filter.Locality.Trim().ToLower();
        var query = db.MarketObservations.Where(item => item.Locality.ToLower() == locality);
        if (!string.IsNullOrWhiteSpace(filter.PropertyType))
        {
            var type = filter.PropertyType.Trim().ToLower();
            query = query.Where(item => item.PropertyType.ToLower() == type);
        }

        if (!string.IsNullOrWhiteSpace(filter.Configuration))
        {
            var config = filter.Configuration.Trim().ToLower();
            query = query.Where(item => item.Configuration.ToLower() == config);
        }

        if (!string.IsNullOrWhiteSpace(filter.RecordType))
        {
            var kind = filter.RecordType.Trim().ToLower();
            query = query.Where(item => item.RecordType == kind);
        }

        if (!string.IsNullOrWhiteSpace(filter.AreaType))
        {
            var area = filter.AreaType.Trim().ToLower();
            query = query.Where(item => item.AreaType == area);
        }

        if (!string.IsNullOrWhiteSpace(filter.AreaUnit))
        {
            var unit = filter.AreaUnit.Trim().ToLower();
            query = query.Where(item => item.AreaUnit == unit);
        }

        if (!string.IsNullOrWhiteSpace(filter.VerificationStatus))
        {
            var status = filter.VerificationStatus.Trim().ToLower();
            query = query.Where(item => item.VerificationStatus == status);
        }

        if (filter.ObservedFrom is not null)
        {
            var from = filter.ObservedFrom.Value;
            query = query.Where(item => item.ObservedOn >= from);
        }

        if (filter.ObservedTo is not null)
        {
            var to = filter.ObservedTo.Value;
            query = query.Where(item => item.ObservedOn <= to);
        }

        if (!filter.IncludeSynthetic)
        {
            query = query.Where(item => !item.IsSyntheticTestData);
        }

        if (!filter.IncludeOutliers)
        {
            query = query.Where(item => !item.IsOutlier);
        }

        return query;
    }

    private async Task<(List<MarketObservation> Cohort, bool UsingSynthetic, int GenuineCount, int SyntheticCount)> LoadCohortAsync(
        MarketFilter filter,
        CancellationToken cancellationToken)
    {
        var all = await Filtered(Clone(filter, includeSynthetic: true, includeOutliers: true))
            .AsNoTracking()
            .ToListAsync(cancellationToken);
        var genuine = all.Where(item => !item.IsSyntheticTestData).ToList();
        var synthetic = all.Where(item => item.IsSyntheticTestData).ToList();
        if (genuine.Count > 0)
        {
            return (genuine, false, genuine.Count, synthetic.Count);
        }

        if (filter.IncludeSynthetic && synthetic.Count > 0)
        {
            return (synthetic, true, 0, synthetic.Count);
        }

        return ([], false, 0, synthetic.Count);
    }

    private static void ValidateFilter(MarketFilter filter, bool requireLocality)
    {
        var errors = new Dictionary<string, string[]>();
        if (requireLocality && (string.IsNullOrWhiteSpace(filter.Locality) || filter.Locality.Trim().Length < 2))
        {
            errors["locality"] = ["Enter at least two characters of a Bengaluru locality."];
        }

        RequireAllowedOptional(filter.RecordType, MarketObservation.RecordTypes, "recordType", errors);
        RequireAllowedOptional(filter.AreaType, MarketObservation.AreaTypes, "areaType", errors);
        RequireAllowedOptional(filter.AreaUnit, MarketObservation.AreaUnits, "areaUnit", errors);
        RequireAllowedOptional(filter.VerificationStatus, MarketObservation.VerificationStatuses, "verificationStatus", errors);
        if (filter.ObservedFrom is not null && filter.ObservedTo is not null && filter.ObservedFrom > filter.ObservedTo)
        {
            errors["observedFrom"] = ["observedFrom must be on or before observedTo."];
        }

        if (filter.Skip < 0)
        {
            errors["skip"] = ["skip cannot be negative."];
        }

        if (filter.Take < 0 || filter.Take > MarketInsightsCalculator.MaxPageSize)
        {
            errors["take"] = [$"take must be between 0 and {MarketInsightsCalculator.MaxPageSize}."];
        }

        if (errors.Count > 0)
        {
            throw new MarketValidationException(errors);
        }
    }

    private static void RequireAllowedOptional(
        string? value,
        IReadOnlyCollection<string> allowed,
        string key,
        Dictionary<string, string[]> errors)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        if (!allowed.Contains(value.Trim().ToLowerInvariant()))
        {
            errors[key] = [$"{key} must be one of: {string.Join(", ", allowed)}."];
        }
    }

    private static MarketFilter Clone(MarketFilter filter, bool includeSynthetic, bool includeOutliers) =>
        new()
        {
            Locality = filter.Locality,
            PropertyType = filter.PropertyType,
            Configuration = filter.Configuration,
            RecordType = filter.RecordType,
            AreaType = filter.AreaType,
            AreaUnit = filter.AreaUnit,
            VerificationStatus = filter.VerificationStatus,
            IncludeSynthetic = includeSynthetic,
            IncludeOutliers = includeOutliers,
            ObservedFrom = filter.ObservedFrom,
            ObservedTo = filter.ObservedTo,
            Skip = filter.Skip,
            Take = filter.Take,
        };

    private static string? BlankToNull(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private async Task<List<decimal>> ComparableAmountsAsync(
        MarketObservation entity,
        CancellationToken cancellationToken)
    {
        return await db.MarketObservations.AsNoTracking()
            .Where(item =>
                item.Locality.ToLower() == entity.Locality.ToLower()
                && item.Configuration.ToLower() == entity.Configuration.ToLower()
                && item.RecordType == entity.RecordType
                && item.AreaType == entity.AreaType
                && item.AreaUnit == entity.AreaUnit)
            .Select(item => item.Amount)
            .ToListAsync(cancellationToken);
    }

}
