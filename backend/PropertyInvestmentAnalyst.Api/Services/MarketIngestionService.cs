using Microsoft.EntityFrameworkCore;
using PropertyInvestmentAnalyst.Api.Contracts;
using PropertyInvestmentAnalyst.Api.Data;
using PropertyInvestmentAnalyst.Api.Domain;

namespace PropertyInvestmentAnalyst.Api.Services;

public sealed class MarketIngestionService(AppDbContext db)
{
    public const string Policy =
        "Insert-only. Duplicate keys are skipped. Older or lower-quality rows for the same source record are skipped. Existing verified or newer observations are never overwritten.";

    public async Task<MarketImportResult> IngestAsync(
        IReadOnlyList<SaveMarketObservationRequest> requests,
        string? providerKey,
        bool commit,
        CancellationToken cancellationToken)
    {
        var importedUtc = DateTimeOffset.UtcNow;
        var existing = await db.MarketObservations.AsNoTracking().ToListAsync(cancellationToken);
        var existingKeys = existing.Select(item => item.DuplicateKey).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var pending = new List<MarketObservation>();
        var seenKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var rows = new List<MarketImportRowResult>();

        for (var index = 0; index < requests.Count; index += 1)
        {
            var lineNumber = index + 1;
            var request = requests[index];
            request.ProviderKey = string.IsNullOrWhiteSpace(request.ProviderKey) ? providerKey : request.ProviderKey;
            if (!string.IsNullOrWhiteSpace(providerKey) && providerKey != "csv_file" && providerKey != "manual")
            {
                request.VerificationStatus = CapProviderVerification(request.VerificationStatus);
            }

            var errors = MarketObservationValidator.Validate(request, $"row[{lineNumber}]");
            if (errors.Count > 0)
            {
                rows.Add(new MarketImportRowResult
                {
                    LineNumber = lineNumber,
                    Status = "invalid",
                    Errors = errors,
                });
                continue;
            }

            var entity = MarketObservationMapper.ToEntity(request, importedUtc);
            if (existingKeys.Contains(entity.DuplicateKey) || !seenKeys.Add(entity.DuplicateKey))
            {
                rows.Add(Skip(lineNumber, "duplicate", entity.DuplicateKey,
                    "Matching source, listing identity, amount and observation date already exist. The existing record was not overwritten."));
                continue;
            }

            var peers = existing.Concat(pending)
                .Where(item => SameSourceRecord(item, entity))
                .ToArray();
            if (peers.Length > 0)
            {
                var newest = peers.Max(item => item.ObservedOn);
                if (entity.ObservedOn < newest)
                {
                    rows.Add(Skip(lineNumber, "skipped_older", entity.DuplicateKey,
                        "An existing observation for this source record has a later observation date. Older rows are not applied."));
                    continue;
                }

                var sameDay = peers.Where(item => item.ObservedOn == entity.ObservedOn).ToArray();
                if (sameDay.Any(item => Rank(item.VerificationStatus) > Rank(entity.VerificationStatus)
                    || item.VerificationStatus == MarketObservation.VerificationReviewed))
                {
                    rows.Add(Skip(lineNumber, "skipped_lower_quality", entity.DuplicateKey,
                        "An existing same-day observation is verified or higher quality. It was not overwritten."));
                    continue;
                }
            }

            var comparable = existing.Concat(pending)
                .Where(item => SameComparableGroup(item, entity))
                .Select(item => item.Amount)
                .ToList();
            entity.IsOutlier = MarketObservationMapper.IsOutlierAmount(entity.Amount, comparable);
            pending.Add(entity);
            rows.Add(new MarketImportRowResult
            {
                LineNumber = lineNumber,
                Status = entity.IsOutlier ? "valid_outlier" : "valid",
                Errors = [],
                DuplicateKey = entity.DuplicateKey,
                Observation = MarketObservationMapper.ToDto(entity, DateOnly.FromDateTime(DateTime.UtcNow)),
            });
        }

        var inserted = 0;
        if (commit && pending.Count > 0)
        {
            await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
            db.MarketObservations.AddRange(pending);
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            inserted = pending.Count;
        }

        return new MarketImportResult
        {
            TotalRows = requests.Count,
            Valid = rows.Count(item => item.Status is "valid" or "valid_outlier"),
            Invalid = rows.Count(item => item.Status == "invalid"),
            Duplicates = rows.Count(item => item.Status == "duplicate"),
            Outliers = rows.Count(item => item.Status == "valid_outlier"),
            Inserted = inserted,
            Updated = 0,
            Skipped = rows.Count(item => item.Status is "skipped_older" or "skipped_lower_quality"),
            Rows = rows,
        };
    }

    private static MarketImportRowResult Skip(int line, string status, string key, string message) =>
        new()
        {
            LineNumber = line,
            Status = status,
            Errors = [message],
            DuplicateKey = key,
        };

    private static string CapProviderVerification(string? status)
    {
        var value = (status ?? "").Trim().ToLowerInvariant();
        if (value == MarketObservation.VerificationReviewed)
        {
            return MarketObservation.VerificationSourceCited;
        }

        return string.IsNullOrWhiteSpace(value) ? MarketObservation.VerificationSourceCited : value;
    }

    private static int Rank(string status) => status switch
    {
        MarketObservation.VerificationReviewed => 3,
        MarketObservation.VerificationSourceCited => 2,
        _ => 1,
    };

    private static bool SameSourceRecord(MarketObservation left, MarketObservation right) =>
        !string.IsNullOrWhiteSpace(left.SourceRecordId)
        && left.SourceName.Equals(right.SourceName, StringComparison.OrdinalIgnoreCase)
        && left.SourceRecordId.Equals(right.SourceRecordId, StringComparison.OrdinalIgnoreCase);

    private static bool SameComparableGroup(MarketObservation left, MarketObservation right) =>
        left.Locality.Equals(right.Locality, StringComparison.OrdinalIgnoreCase)
        && left.Configuration.Equals(right.Configuration, StringComparison.OrdinalIgnoreCase)
        && left.RecordType == right.RecordType
        && left.AreaType == right.AreaType
        && left.AreaUnit == right.AreaUnit;
}
