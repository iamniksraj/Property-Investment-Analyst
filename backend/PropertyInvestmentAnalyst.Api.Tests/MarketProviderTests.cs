using System.Net;
using System.Net.Http.Json;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using PropertyInvestmentAnalyst.Api.Contracts;
using PropertyInvestmentAnalyst.Api.Data;
using PropertyInvestmentAnalyst.Api.Domain;
using PropertyInvestmentAnalyst.Api.Services;
using PropertyInvestmentAnalyst.Api.Services.MarketData;

namespace PropertyInvestmentAnalyst.Api.Tests;

public class NormalizedObservationMapperTests
{
    [Fact]
    public void Provider_rows_keep_record_types_separate_and_do_not_become_reviewed()
    {
        var asking = NormalizedObservationMapper.ToRequest(new NormalizedMarketObservation
        {
            Locality = "Whitefield",
            BuilderProject = "Tower",
            Configuration = "2 BHK",
            RecordType = MarketObservation.RecordTypeAskingPrice,
            Amount = 12_000_000,
            SourceName = "Licensed feed",
            SourceRecordId = "L-1",
            ObservedOn = new DateOnly(2026, 10, 1),
        });
        Assert.Equal(MarketObservation.RecordTypeAskingPrice, asking.RecordType);
        Assert.Equal(MarketObservation.VerificationSourceCited, asking.VerificationStatus);
        Assert.False(asking.IsSyntheticTestData);

        var unverified = NormalizedObservationMapper.ToRequest(new NormalizedMarketObservation
        {
            Locality = "Whitefield",
            BuilderProject = "Tower",
            Configuration = "2 BHK",
            RecordType = MarketObservation.RecordTypeAdvertisedRent,
            Amount = 40_000,
            SourceName = " ",
            ObservedOn = new DateOnly(2026, 10, 1),
        });
        Assert.Equal(MarketObservation.VerificationUnverified, unverified.VerificationStatus);
    }
}

public class LicensedFeedMarketDataProviderTests
{
    [Fact]
    public async Task Missing_credentials_do_not_call_the_network()
    {
        var handler = new QueueHandler();
        var provider = Create(handler, configured: false);
        var result = await provider.FetchAsync(new MarketProviderFetchRequest(), CancellationToken.None);
        Assert.Equal("not_configured", result.Status);
        Assert.Equal(0, handler.Calls);
        Assert.Empty(result.Observations);
    }

    [Fact]
    public async Task Malformed_json_and_rate_limits_are_reported_without_inventing_rows()
    {
        var handler = new QueueHandler();
        handler.Enqueue(new HttpResponseMessage(HttpStatusCode.TooManyRequests));
        handler.Enqueue(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("{not-json", Encoding.UTF8, "application/json"),
        });
        var provider = Create(handler, configured: true);
        var result = await provider.FetchAsync(new MarketProviderFetchRequest(), CancellationToken.None);
        Assert.Equal("error", result.Status);
        Assert.Contains("JSON", result.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(result.Observations);
        Assert.Equal(2, handler.Calls);
    }

    [Fact]
    public async Task Service_unavailable_after_retries_returns_no_observations()
    {
        var handler = new QueueHandler();
        handler.Enqueue(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));
        handler.Enqueue(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));
        var logs = new ListLogger<LicensedFeedMarketDataProvider>();
        var provider = Create(handler, configured: true, logger: logs, retries: 1);
        var result = await provider.FetchAsync(new MarketProviderFetchRequest(), CancellationToken.None);
        Assert.Equal("error", result.Status);
        Assert.Contains("503", result.Message);
        Assert.Empty(result.Observations);
        Assert.All(logs.Messages, message => Assert.DoesNotContain("test-key", message, StringComparison.Ordinal));
    }

    [Fact]
    public async Task Timeout_returns_no_observations_and_does_not_log_the_api_key()
    {
        var logs = new ListLogger<LicensedFeedMarketDataProvider>();
        var provider = Create(new HangHandler(), configured: true, logger: logs, retries: 0, timeoutSeconds: 1);
        var result = await provider.FetchAsync(new MarketProviderFetchRequest(), CancellationToken.None);
        Assert.Equal("error", result.Status);
        Assert.Contains("timed out", result.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(result.Observations);
        Assert.All(logs.Messages, message => Assert.DoesNotContain("test-key", message, StringComparison.Ordinal));
    }

    [Fact]
    public async Task Valid_envelope_maps_asking_prices_without_claiming_transactions()
    {
        var handler = new QueueHandler();
        handler.Enqueue(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("""
                {"observations":[{"locality":"Whitefield","builderProject":"A","configuration":"2 BHK","recordType":"asking_price","amount":12000000,"sourceName":"Licensed","sourceRecordId":"L-1","observedOn":"2026-10-01","areaType":"super_built_up","areaUnit":"sq_ft","areaValue":1200}]}
                """, Encoding.UTF8, "application/json"),
        });
        var provider = Create(handler, configured: true);
        var result = await provider.FetchAsync(new MarketProviderFetchRequest(), CancellationToken.None);
        Assert.Equal("fetched", result.Status);
        Assert.Equal(MarketObservation.RecordTypeAskingPrice, result.Observations[0].RecordType);
    }

    private static LicensedFeedMarketDataProvider Create(
        HttpMessageHandler handler,
        bool configured,
        ILogger<LicensedFeedMarketDataProvider>? logger = null,
        int retries = 1,
        int timeoutSeconds = 5)
    {
        var http = new HttpClient(handler);
        var options = Options.Create(new MarketDataOptions
        {
            LicensedFeed = new LicensedFeedOptions
            {
                BaseUrl = configured ? "https://licensed.example.test/observations" : "",
                ApiKey = configured ? "test-key" : "",
                MaxRetries = retries,
                TimeoutSeconds = timeoutSeconds,
            },
        });
        return new LicensedFeedMarketDataProvider(
            http,
            options,
            logger ?? NullLogger<LicensedFeedMarketDataProvider>.Instance);
    }

    private sealed class QueueHandler : HttpMessageHandler
    {
        private readonly Queue<HttpResponseMessage> _responses = new();
        public int Calls { get; private set; }

        public void Enqueue(HttpResponseMessage response) => _responses.Enqueue(response);

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Calls += 1;
            Assert.True(request.Headers.Contains("X-Api-Key"));
            return Task.FromResult(_responses.Dequeue());
        }
    }

    private sealed class HangHandler : HttpMessageHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            await Task.Delay(TimeSpan.FromSeconds(30), cancellationToken);
            return new HttpResponseMessage(HttpStatusCode.OK);
        }
    }
}

public class MarketDataEnvironmentGuardTests
{
    [Fact]
    public void Production_forces_manual_refresh_off_even_if_configuration_enables_it()
    {
        var options = new MarketDataOptions { AllowManualRefresh = true };
        MarketDataEnvironmentGuard.Apply(new FakeHostEnvironment { EnvironmentName = "Production" }, options);
        Assert.False(options.AllowManualRefresh);
        Assert.False(MarketDataEnvironmentGuard.ManualRefreshAllowed(
            new FakeHostEnvironment { EnvironmentName = "Production" },
            new MarketDataOptions { AllowManualRefresh = true }));
    }

    [Fact]
    public void Development_may_enable_manual_refresh_explicitly()
    {
        var options = new MarketDataOptions { AllowManualRefresh = true };
        MarketDataEnvironmentGuard.Apply(new FakeHostEnvironment { EnvironmentName = "Development" }, options);
        Assert.True(options.AllowManualRefresh);
    }
}

[Collection("SqlServer")]
public class MarketProviderApiTests : IAsyncLifetime
{
    private readonly ApiFactory _factory;
    private readonly HttpClient _client;

    public MarketProviderApiTests(ApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    public Task InitializeAsync() => _factory.ResetDatabaseAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Provider_status_is_not_a_live_feed_and_refresh_without_credentials_does_not_insert()
    {
        var status = await _client.GetFromJsonAsync<MarketProviderStatusResponse>("/api/market-providers");
        Assert.False(status!.HasGenuineLiveFeed);
        Assert.Contains(status.Providers, item => item.Key == "csv_file");
        Assert.Contains(status.Providers, item => item.Key == "licensed_feed" && !item.Configured && !item.LiveFeed);

        var refresh = await _client.PostAsJsonAsync("/api/market-providers/licensed_feed/refresh", new { });
        refresh.EnsureSuccessStatusCode();
        var body = await refresh.Content.ReadFromJsonAsync<MarketProviderRefreshResponse>();
        Assert.Equal("not_configured", body!.FetchStatus);
        Assert.Equal(0, body.Import.Inserted);

        var after = await _client.GetFromJsonAsync<MarketProviderStatusResponse>("/api/market-providers");
        Assert.Equal("not_configured", after!.LastImport?.Status);
        Assert.Equal(0, after.GenuineObservationCount);
    }

    [Fact]
    public async Task Ingestion_skips_older_and_lower_quality_rows_and_never_updates_in_place()
    {
        var first = RequestFactory.ValidMarketObservation();
        first.IsSyntheticTestData = false;
        first.SourceName = "Permitted source";
        first.SourceRecordId = "SRC-1";
        first.ObservedOn = new DateOnly(2026, 10, 10);
        first.VerificationStatus = MarketObservation.VerificationReviewed;
        first.Amount = 12_000_000;
        (await _client.PostAsJsonAsync("/api/market-observations", first)).EnsureSuccessStatusCode();

        var older = RequestFactory.ValidMarketObservation();
        older.IsSyntheticTestData = false;
        older.SourceName = "Permitted source";
        older.SourceRecordId = "SRC-1";
        older.ObservedOn = new DateOnly(2026, 10, 1);
        older.Amount = 11_000_000;
        older.ProviderKey = "licensed_feed";
        var olderResponse = await _client.PostAsJsonAsync("/api/market-observations", older);
        Assert.Equal(HttpStatusCode.Created, olderResponse.StatusCode);

        var csv = """
locality,builderProject,propertyType,configuration,recordType,amount,areaValue,areaType,areaUnit,sourceName,sourceUrl,sourceRecordId,observedOn,verificationStatus,qualityNotes,licenceOrUsageNotes,isSyntheticTestData
Whitefield,Synthetic fixture tower,Apartment,2 BHK,asking_price,12500000,1200,super_built_up,sq_ft,Permitted source,,SRC-1,2026-10-10,source_cited,n,n,false
""";
        using var content = new MultipartFormDataContent();
        var file = new ByteArrayContent(Encoding.UTF8.GetBytes(csv));
        file.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("text/csv");
        content.Add(file, "file", "retry.csv");
        var imported = await _client.PostAsync("/api/market-observations/import/commit", content);
        imported.EnsureSuccessStatusCode();
        var result = await imported.Content.ReadFromJsonAsync<MarketImportResult>();
        Assert.Equal(0, result!.Inserted);
        Assert.Equal(0, result.Updated);
        Assert.True(result.Duplicates + result.Skipped >= 1);

        var search = await _client.GetFromJsonAsync<MarketSearchResponse>(
            "/api/market-observations?locality=Whitefield&includeSynthetic=false");
        Assert.DoesNotContain(search!.Items, item => item.Amount == 12_500_000);
        Assert.Contains(search.Items, item => item.VerificationStatus == MarketObservation.VerificationReviewed && item.Amount == 12_000_000);

        var comparison = await _client.PostAsJsonAsync("/api/comparisons", RequestFactory.ValidRequest());
        Assert.Equal(HttpStatusCode.Created, comparison.StatusCode);
    }

    [Fact]
    public async Task Failed_and_successful_refreshes_write_accurate_runs_without_secrets_or_partial_inserts()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var before = await db.MarketObservations.CountAsync();

        var failed = Catalog(scope, new StubProvider
        {
            Result = new ProviderFetchResult
            {
                Status = "error",
                Message = "timed out using apiKey=super-secret-key",
                ErrorCount = 1,
            },
        });
        var failedResult = await failed.RefreshAsync(LicensedFeedMarketDataProvider.ProviderKey, new MarketProviderFetchRequest(), CancellationToken.None);
        Assert.Equal("error", failedResult.FetchStatus);
        Assert.Equal(0, failedResult.Import.Inserted);
        Assert.Equal(0, failedResult.Run.Inserted);
        Assert.True(failedResult.Run.ErrorCount >= 1);
        Assert.True(failedResult.Run.CompletedUtc >= failedResult.Run.StartedUtc);
        Assert.DoesNotContain("super-secret-key", failedResult.Run.Summary, StringComparison.Ordinal);
        Assert.Equal(before, await db.MarketObservations.CountAsync());

        var ok = Catalog(scope, new StubProvider
        {
            Result = new ProviderFetchResult
            {
                Status = "fetched",
                Message = "ok token=super-secret-key",
                Observations =
                [
                    new NormalizedMarketObservation
                    {
                        Locality = "Whitefield",
                        BuilderProject = "Licensed tower",
                        Configuration = "2 BHK",
                        RecordType = MarketObservation.RecordTypeAskingPrice,
                        Amount = 12_100_000,
                        AreaValue = 1200,
                        AreaType = MarketObservation.AreaSuperBuiltUp,
                        AreaUnit = MarketObservation.UnitSqFt,
                        SourceName = "Licensed feed",
                        SourceRecordId = "LIC-OK-1",
                        ObservedOn = new DateOnly(2026, 10, 12),
                    },
                ],
            },
        });
        var started = DateTimeOffset.UtcNow.AddSeconds(-1);
        var okResult = await ok.RefreshAsync(LicensedFeedMarketDataProvider.ProviderKey, new MarketProviderFetchRequest(), CancellationToken.None);
        Assert.Equal("fetched", okResult.FetchStatus);
        Assert.Equal(1, okResult.Import.Inserted);
        Assert.Equal(0, okResult.Import.Updated);
        Assert.Equal(1, okResult.Run.Inserted);
        Assert.Equal("success", okResult.Run.Status);
        Assert.True(okResult.Run.StartedUtc >= started);
        Assert.True(okResult.Run.CompletedUtc >= okResult.Run.StartedUtc);
        Assert.DoesNotContain("super-secret-key", okResult.Run.Summary, StringComparison.Ordinal);

        var retry = await ok.RefreshAsync(LicensedFeedMarketDataProvider.ProviderKey, new MarketProviderFetchRequest(), CancellationToken.None);
        Assert.Equal(0, retry.Import.Inserted);
        Assert.Equal(0, retry.Import.Updated);
        Assert.Equal(1, retry.Import.Duplicates);
    }

    [Fact]
    public async Task Ingest_skips_duplicates_older_rows_and_lower_quality_without_overwriting_verified_amounts()
    {
        using var scope = _factory.Services.CreateScope();
        var ingestion = scope.ServiceProvider.GetRequiredService<MarketIngestionService>();
        var first = RequestFactory.ValidMarketObservation();
        first.IsSyntheticTestData = false;
        first.SourceName = "Permitted source";
        first.SourceRecordId = "SRC-KEEP";
        first.ObservedOn = new DateOnly(2026, 10, 10);
        first.VerificationStatus = MarketObservation.VerificationReviewed;
        first.Amount = 12_000_000;
        first.ProviderKey = "manual";
        var inserted = await ingestion.IngestAsync([first], "manual", commit: true, CancellationToken.None);
        Assert.Equal(1, inserted.Inserted);

        var duplicate = await ingestion.IngestAsync([first], "manual", commit: true, CancellationToken.None);
        Assert.Equal(0, duplicate.Inserted);
        Assert.Equal(0, duplicate.Updated);
        Assert.Equal(1, duplicate.Duplicates);

        var older = RequestFactory.ValidMarketObservation();
        older.IsSyntheticTestData = false;
        older.SourceName = "Permitted source";
        older.SourceRecordId = "SRC-KEEP";
        older.ObservedOn = new DateOnly(2026, 10, 1);
        older.Amount = 9_000_000;
        older.VerificationStatus = MarketObservation.VerificationSourceCited;
        var olderResult = await ingestion.IngestAsync([older], "csv_file", commit: true, CancellationToken.None);
        Assert.Equal(0, olderResult.Inserted);
        Assert.Equal(1, olderResult.Skipped);
        Assert.Contains(olderResult.Rows, row => row.Status == "skipped_older");

        var weaker = RequestFactory.ValidMarketObservation();
        weaker.IsSyntheticTestData = false;
        weaker.SourceName = "Permitted source";
        weaker.SourceRecordId = "SRC-KEEP";
        weaker.ObservedOn = new DateOnly(2026, 10, 10);
        weaker.Amount = 15_000_000;
        weaker.VerificationStatus = MarketObservation.VerificationSourceCited;
        var weakerResult = await ingestion.IngestAsync([weaker], "csv_file", commit: true, CancellationToken.None);
        Assert.Equal(0, weakerResult.Inserted);
        Assert.Equal(0, weakerResult.Updated);
        Assert.Contains(weakerResult.Rows, row => row.Status == "skipped_lower_quality");

        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var kept = await db.MarketObservations.SingleAsync(item => item.SourceRecordId == "SRC-KEEP");
        Assert.Equal(12_000_000m, kept.Amount);
        Assert.Equal(MarketObservation.VerificationReviewed, kept.VerificationStatus);
        Assert.Equal(new DateOnly(2026, 10, 10), kept.ObservedOn);
    }

    [Fact]
    public async Task Production_rejects_refresh_even_when_AllowManualRefresh_is_true()
    {
        using var scope = _factory.Services.CreateScope();
        var catalog = Catalog(
            scope,
            new StubProvider { Result = new ProviderFetchResult { Status = "fetched", Message = "should not run" } },
            environmentName: "Production",
            allowRefresh: true);
        var error = await Assert.ThrowsAsync<MarketValidationException>(() =>
            catalog.RefreshAsync(LicensedFeedMarketDataProvider.ProviderKey, new MarketProviderFetchRequest(), CancellationToken.None));
        Assert.Contains(error.Errors.SelectMany(item => item.Value), message => message.Contains("Production", StringComparison.OrdinalIgnoreCase));
        Assert.False(MarketDataEnvironmentGuard.ManualRefreshAllowed(
            new FakeHostEnvironment { EnvironmentName = "Production" },
            new MarketDataOptions { AllowManualRefresh = true }));
    }

    private static MarketProviderCatalogService Catalog(
        IServiceScope scope,
        IMarketDataProvider provider,
        string environmentName = "Testing",
        bool allowRefresh = true)
    {
        return new MarketProviderCatalogService(
            [provider],
            scope.ServiceProvider.GetRequiredService<AppDbContext>(),
            scope.ServiceProvider.GetRequiredService<MarketIngestionService>(),
            Options.Create(new MarketDataOptions
            {
                AllowManualRefresh = allowRefresh,
                LicensedFeed = new LicensedFeedOptions { ApiKey = "super-secret-key" },
            }),
            new FakeHostEnvironment { EnvironmentName = environmentName },
            NullLogger<MarketProviderCatalogService>.Instance);
    }

    private sealed class StubProvider : IMarketDataProvider
    {
        public required ProviderFetchResult Result { get; init; }
        public string Key => LicensedFeedMarketDataProvider.ProviderKey;

        public MarketProviderDescriptor Describe() => new()
        {
            Key = Key,
            DisplayName = "Stub",
            Kind = "test",
            FetchSupported = true,
            Configured = true,
            LiveFeed = false,
            Coverage = "test",
            AccessNotes = "test",
            DocumentationUrl = "https://example.test",
        };

        public Task<ProviderFetchResult> FetchAsync(MarketProviderFetchRequest request, CancellationToken cancellationToken) =>
            Task.FromResult(Result);
    }
}

internal sealed class FakeHostEnvironment : IHostEnvironment
{
    public required string EnvironmentName { get; set; }
    public string ApplicationName { get; set; } = "PropertyInvestmentAnalyst.Api.Tests";
    public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
    public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
}

internal sealed class ListLogger<T> : ILogger<T>
{
    public List<string> Messages { get; } = [];

    public IDisposable BeginScope<TState>(TState state) where TState : notnull => NullDisposable.Instance;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        Messages.Add(formatter(state, exception));
    }

    private sealed class NullDisposable : IDisposable
    {
        public static readonly NullDisposable Instance = new();
        public void Dispose()
        {
        }
    }
}
