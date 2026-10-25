using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using PropertyInvestmentAnalyst.Api.Contracts;
using PropertyInvestmentAnalyst.Api.Data;
using PropertyInvestmentAnalyst.Api.Domain;

namespace PropertyInvestmentAnalyst.Api.Services.MarketData;

public sealed class MarketProviderCatalogService(
    IEnumerable<IMarketDataProvider> providers,
    AppDbContext db,
    MarketIngestionService ingestion,
    IOptions<MarketDataOptions> options,
    IHostEnvironment environment,
    ILogger<MarketProviderCatalogService> logger)
{
    public async Task<MarketProviderStatusResponse> StatusAsync(CancellationToken cancellationToken)
    {
        var genuine = await db.MarketObservations.CountAsync(item => !item.IsSyntheticTestData, cancellationToken);
        var synthetic = await db.MarketObservations.CountAsync(item => item.IsSyntheticTestData, cancellationToken);
        DateOnly? newestObservation = await db.MarketObservations.AnyAsync(cancellationToken)
            ? await db.MarketObservations.MaxAsync(item => item.ObservedOn, cancellationToken)
            : null;
        var lastRun = await db.MarketImportRuns
            .AsNoTracking()
            .OrderByDescending(item => item.CompletedUtc)
            .FirstOrDefaultAsync(cancellationToken);

        return new MarketProviderStatusResponse
        {
            HasGenuineLiveFeed = false,
            AllowManualRefresh = MarketDataEnvironmentGuard.ManualRefreshAllowed(environment, options.Value),
            IngestionPolicy = MarketIngestionService.Policy,
            GenuineObservationCount = genuine,
            SyntheticObservationCount = synthetic,
            NewestObservationDate = newestObservation,
            LastSuccessfulImportUtc = lastRun is { Status: "success" or "completed_with_errors" }
                ? lastRun.CompletedUtc
                : null,
            LastImport = lastRun is null ? null : ToDto(lastRun),
            Providers = new[] { CsvChannel() }.Concat(providers.Select(item => item.Describe())).ToArray(),
            Notes =
                "This application is ready for a licensed provider adapter but currently has no genuine live Bengaluru market feed. "
                + "CSV import and manual create remain the supported ingestion paths. Synthetic fixtures are never genuine-market evidence. "
                + "Observation date is when the listing or transaction was observed; import time is when this application stored the row.",
        };
    }

    public async Task<MarketProviderRefreshResponse> RefreshAsync(
        string providerKey,
        MarketProviderFetchRequest request,
        CancellationToken cancellationToken)
    {
        if (!MarketDataEnvironmentGuard.ManualRefreshAllowed(environment, options.Value))
        {
            throw new MarketValidationException(new Dictionary<string, string[]>
            {
                ["refresh"] =
                [
                    environment.IsProduction()
                        ? "Manual provider refresh is disabled in Production and cannot be enabled through configuration. The API is unauthenticated and is not a public ingestion endpoint."
                        : "Manual provider refresh is disabled. Enable MarketData:AllowManualRefresh only on a local development host.",
                ],
            });
        }

        var provider = providers.FirstOrDefault(item => item.Key.Equals(providerKey, StringComparison.OrdinalIgnoreCase))
            ?? throw new MarketValidationException(new Dictionary<string, string[]>
            {
                ["provider"] = [$"Unknown provider '{providerKey}'."],
            });

        var started = DateTimeOffset.UtcNow;
        var fetch = await provider.FetchAsync(request, cancellationToken);
        if (fetch.Status != "fetched")
        {
            var skippedRun = await RecordAsync(new MarketImportRun
            {
                Id = Guid.NewGuid(),
                ProviderKey = provider.Key,
                Status = fetch.Status,
                StartedUtc = started,
                CompletedUtc = DateTimeOffset.UtcNow,
                ObservedFrom = request.ObservedFrom,
                ObservedTo = request.ObservedTo,
                Summary = Redact(fetch.Message),
                Inserted = 0,
                Updated = 0,
                ErrorCount = fetch.ErrorCount,
            }, cancellationToken);
            return new MarketProviderRefreshResponse
            {
                FetchStatus = fetch.Status,
                Message = fetch.Message,
                Import = new MarketImportResult
                {
                    TotalRows = 0,
                    Valid = 0,
                    Invalid = 0,
                    Duplicates = 0,
                    Outliers = 0,
                    Inserted = 0,
                    Rows = [],
                },
                Run = skippedRun,
            };
        }

        var mapped = fetch.Observations.Select(item =>
        {
            var save = NormalizedObservationMapper.ToRequest(item);
            save.IsSyntheticTestData = false;
            save.ProviderKey = provider.Key;
            if (save.RecordType == MarketObservation.RecordTypeAskingPrice)
            {
                save.QualityNotes = string.IsNullOrWhiteSpace(save.QualityNotes)
                    ? "Asking price from a licensed feed. Not a completed transaction."
                    : save.QualityNotes;
            }

            return save;
        }).ToArray();

        var import = await ingestion.IngestAsync(mapped, provider.Key, commit: true, cancellationToken);
        var run = await RecordAsync(new MarketImportRun
        {
            Id = Guid.NewGuid(),
            ProviderKey = provider.Key,
            Status = import.Invalid > 0 && import.Inserted == 0 ? "completed_with_errors" : "success",
            StartedUtc = started,
            CompletedUtc = DateTimeOffset.UtcNow,
            ObservedFrom = request.ObservedFrom,
            ObservedTo = request.ObservedTo,
            Inserted = import.Inserted,
            Updated = import.Updated,
            Skipped = import.Skipped,
            Duplicates = import.Duplicates,
            Invalid = import.Invalid,
            ErrorCount = import.Invalid + fetch.ErrorCount,
            Summary = Redact($"{fetch.Message} Inserted {import.Inserted}, duplicates {import.Duplicates}, skipped {import.Skipped}, invalid {import.Invalid}. {MarketIngestionService.Policy}"),
        }, cancellationToken);

        logger.LogInformation(
            "Provider {Provider} refresh finished with status {Status}: inserted {Inserted}, duplicates {Duplicates}.",
            provider.Key,
            run.Status,
            import.Inserted,
            import.Duplicates);

        return new MarketProviderRefreshResponse
        {
            FetchStatus = fetch.Status,
            Message = fetch.Message,
            Import = import,
            Run = run,
        };
    }

    private async Task<MarketImportRunDto> RecordAsync(MarketImportRun run, CancellationToken cancellationToken)
    {
        db.MarketImportRuns.Add(run);
        await db.SaveChangesAsync(cancellationToken);
        return ToDto(run);
    }

    private static MarketImportRunDto ToDto(MarketImportRun run) =>
        new()
        {
            ProviderKey = run.ProviderKey,
            Status = run.Status,
            StartedUtc = run.StartedUtc,
            CompletedUtc = run.CompletedUtc,
            ObservedFrom = run.ObservedFrom,
            ObservedTo = run.ObservedTo,
            Inserted = run.Inserted,
            Updated = run.Updated,
            Skipped = run.Skipped,
            Duplicates = run.Duplicates,
            Invalid = run.Invalid,
            ErrorCount = run.ErrorCount,
            Summary = run.Summary,
        };

    private static MarketProviderDescriptor CsvChannel() =>
        new()
        {
            Key = "csv_file",
            DisplayName = "CSV import (existing workflow)",
            Kind = "file",
            FetchSupported = false,
            Configured = true,
            LiveFeed = false,
            Coverage = "Any locality the file contains. The bundled fixture is synthetic test data only.",
            AccessNotes = "Use POST /api/market-observations/import/preview then /import/commit. This is not a live feed.",
            DocumentationUrl = "/api/market-observations/template",
        };

    private string Redact(string message)
    {
        var text = message.Length > 1000 ? message[..1000] : message;
        var secret = options.Value.LicensedFeed.ApiKey;
        if (!string.IsNullOrEmpty(secret))
        {
            text = text.Replace(secret, "[redacted]", StringComparison.Ordinal);
        }

        return System.Text.RegularExpressions.Regex.Replace(
            text,
            @"(?i)(bearer|api[_-]?key|token|authorization)\s*[:=]\s*\S+",
            "$1=[redacted]");
    }
}
