using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using PropertyInvestmentAnalyst.Api.Contracts;
using PropertyInvestmentAnalyst.Api.Domain;
using PropertyInvestmentAnalyst.Api.Services;

namespace PropertyInvestmentAnalyst.Api.Tests;

public class MarketInsightsCalculatorTests
{
    [Fact]
    public void Percentiles_and_median_match_the_documented_sample_thresholds()
    {
        var amounts = new decimal[] { 11_000_000, 11_500_000, 11_800_000, 11_900_000, 12_000_000, 12_200_000, 12_500_000, 12_800_000, 13_000_000 };
        Assert.Equal(12_000_000m, MarketObservationMapper.Median(amounts));
        var quartiles = MarketObservationMapper.Quartiles(amounts);
        Assert.NotNull(quartiles);
        Assert.Equal(MarketObservationMapper.Percentile(amounts.OrderBy(item => item).ToArray(), 0.25m), quartiles.Value.Q1);
        Assert.Equal(MarketObservationMapper.Percentile(amounts.OrderBy(item => item).ToArray(), 0.75m), quartiles.Value.Q3);
        Assert.Null(MarketObservationMapper.Quartiles([1, 2, 3, 4]));
    }

    [Fact]
    public void History_is_insufficient_when_months_are_thin_and_never_interpolates()
    {
        var rows = new[]
        {
            Observation(new DateOnly(2026, 1, 1), 10),
            Observation(new DateOnly(2026, 3, 1), 11),
        };
        var history = MarketInsightsCalculator.BuildHistory(rows, usingSynthetic: false);
        Assert.False(history.Sufficient);
        Assert.Equal(3, history.Points.Count);
        Assert.Equal(0, history.Points.Single(item => item.YearMonth == "2026-02").Count);
        Assert.All(history.Points, point => Assert.Null(point.MedianAmount));
    }

    [Fact]
    public void Synthetic_history_is_never_described_as_real_market_trend()
    {
        var history = MarketInsightsCalculator.BuildHistory(
            [Observation(new DateOnly(2026, 10, 1), 12_000_000)],
            usingSynthetic: true);
        Assert.False(history.Sufficient);
        Assert.True(history.UsingSyntheticTestData);
        Assert.Contains("not Bengaluru market history", history.Reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Assumption_compare_requires_acknowledgement_for_incompatible_area()
    {
        var sample = Observation(new DateOnly(2026, 10, 1), 12_000_000, MarketObservation.AreaCarpet);
        var peers = new[]
        {
            sample,
            Observation(new DateOnly(2026, 10, 2), 11_000_000, MarketObservation.AreaCarpet),
            Observation(new DateOnly(2026, 10, 3), 13_000_000, MarketObservation.AreaCarpet),
        };
        var blocked = MarketInsightsCalculator.CompareAssumption(
            new AssumptionComparisonRequest
            {
                Locality = "Whitefield",
                PurchasePrice = 12_000_000,
                AreaType = MarketObservation.AreaSuperBuiltUp,
                AreaUnit = MarketObservation.UnitSqFt,
                AreaValue = 1200,
            },
            peers,
            [],
            usingSynthetic: false);
        Assert.Equal("needs_area_acknowledgement", blocked.AskingPrice?.Status);

        var allowed = MarketInsightsCalculator.CompareAssumption(
            new AssumptionComparisonRequest
            {
                Locality = "Whitefield",
                PurchasePrice = 12_000_000,
                AreaType = MarketObservation.AreaSuperBuiltUp,
                AreaUnit = MarketObservation.UnitSqFt,
                AreaValue = 1200,
                AcknowledgeIncompatibleArea = true,
            },
            peers,
            [],
            usingSynthetic: false);
        Assert.True(allowed.AskingPrice?.Compared);
        Assert.True(allowed.AskingPrice?.IncompatibleArea);
        Assert.Null(allowed.AskingPrice?.UserPerSqFt);
        Assert.Contains("not proof that a property is undervalued or overpriced", allowed.Disclaimer, StringComparison.OrdinalIgnoreCase);
    }

    private static MarketObservationDto Observation(
        DateOnly observedOn,
        decimal amount,
        string areaType = MarketObservation.AreaSuperBuiltUp) => new()
    {
        Id = Guid.NewGuid(),
        Locality = "Whitefield",
        BuilderProject = "Tower",
        PropertyType = "Apartment",
        Configuration = "2 BHK",
        RecordType = MarketObservation.RecordTypeAskingPrice,
        VerificationStatus = MarketObservation.VerificationSourceCited,
        Amount = amount,
        AreaValue = 1200,
        AreaType = areaType,
        AreaUnit = MarketObservation.UnitSqFt,
        PricePerSqFt = areaType == MarketObservation.AreaSuperBuiltUp ? amount / 1200 : null,
        MonthlyRentPerSqFt = null,
        SourceName = "Test",
        SourceUrl = null,
        SourceRecordId = Guid.NewGuid().ToString(),
        ObservedOn = observedOn,
        ImportedUtc = DateTimeOffset.UtcNow,
        QualityNotes = null,
        LicenceOrUsageNotes = null,
        IsSyntheticTestData = false,
        IsOutlier = false,
        IsStale = false,
        IsUnverified = false,
        HasCompleteSource = true,
    };
}

[Collection("SqlServer")]
public class MarketInsightsApiTests : IAsyncLifetime
{
    private readonly ApiFactory _factory;
    private readonly HttpClient _client;

    public MarketInsightsApiTests(ApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    public Task InitializeAsync() => _factory.ResetDatabaseAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Default_stats_do_not_treat_synthetic_fixture_as_genuine_market()
    {
        await ImportAsync(FixtureCsv());
        var excluded = await _client.GetFromJsonAsync<MarketStatsResponse>(
            "/api/market-observations/stats?locality=Whitefield");
        Assert.True(excluded!.OnlySyntheticAvailable);
        Assert.False(excluded.UsingSyntheticTestData);
        Assert.Empty(excluded.Buckets);
        Assert.False(excluded.History.Sufficient);

        var included = await _client.GetFromJsonAsync<MarketStatsResponse>(
            "/api/market-observations/stats?locality=Whitefield&configuration=2%20BHK&includeSynthetic=true");
        Assert.True(included!.UsingSyntheticTestData);
        var asking = included.Buckets.Single(item => item.RecordType == MarketObservation.RecordTypeAskingPrice);
        Assert.Equal(9, asking.SampleSize);
        Assert.Equal(1, asking.ExcludedOutliers);
        Assert.Equal(11_000_000m, asking.MinAmount);
        Assert.Equal(13_000_000m, asking.MaxAmount);
        Assert.Equal(12_000_000m, asking.MedianAmount);
        Assert.NotNull(asking.Percentile25);
        Assert.NotNull(asking.Percentile75);
        Assert.True(asking.MixedAreaDefinitions);
        Assert.Null(asking.MedianPricePerSqFt);
        Assert.DoesNotContain(included.Buckets, item => item.RecordType == MarketObservation.RecordTypeVerifiedTransaction);
        Assert.Contains(asking.Quality.Notes, note => note.Contains("synthetic", StringComparison.OrdinalIgnoreCase));
        Assert.False(included.History.Sufficient);
        Assert.Contains(included.History.Points, point => point.YearMonth == "2025-01");
        Assert.Contains(included.History.Points, point => point.Count == 0);
    }

    [Fact]
    public async Task Filters_separate_area_verification_dates_and_never_mix_genuine_with_synthetic()
    {
        await ImportAsync(FixtureCsv());
        var genuine = RequestFactory.ValidMarketObservation();
        genuine.IsSyntheticTestData = false;
        genuine.SourceName = "Cited research note";
        genuine.SourceRecordId = "GENUINE-TEST-001";
        genuine.Amount = 12_400_000;
        var created = await _client.PostAsJsonAsync("/api/market-observations", genuine);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);

        var mixed = await _client.GetFromJsonAsync<MarketStatsResponse>(
            "/api/market-observations/stats?locality=Whitefield&configuration=2%20BHK&includeSynthetic=true");
        Assert.False(mixed!.UsingSyntheticTestData);
        Assert.Equal(1, mixed.GenuineCount);
        Assert.True(mixed.SyntheticCount > 0);
        Assert.Equal(1, mixed.Buckets.Single().SampleSize);

        var carpet = await _client.GetFromJsonAsync<MarketSearchResponse>(
            "/api/market-observations?locality=Whitefield&areaType=carpet&includeSynthetic=true");
        Assert.Equal(1, carpet!.TotalCount);
        Assert.All(carpet.Items, item => Assert.Equal(MarketObservation.AreaCarpet, item.AreaType));

        var recent = await _client.GetFromJsonAsync<MarketSearchResponse>(
            "/api/market-observations?locality=Whitefield&observedFrom=2026-10-01&observedTo=2026-10-31&includeSynthetic=true");
        Assert.DoesNotContain(recent!.Items, item => item.ObservedOn.Year == 2025);

        var unverified = await _client.GetFromJsonAsync<MarketSearchResponse>(
            "/api/market-observations?locality=Whitefield&verificationStatus=unverified&includeSynthetic=true");
        Assert.All(unverified!.Items, item => Assert.True(item.IsUnverified));

        var noSynthetic = await _client.GetFromJsonAsync<MarketSearchResponse>(
            "/api/market-observations?locality=Whitefield&includeSynthetic=false");
        Assert.Equal(1, noSynthetic!.TotalCount);
        Assert.False(noSynthetic.ContainsSynthetic);
    }

    [Fact]
    public async Task Outliers_stay_listed_and_are_included_in_stats_only_when_requested()
    {
        await ImportAsync(FixtureCsv());
        var excluded = await _client.GetFromJsonAsync<MarketStatsResponse>(
            "/api/market-observations/stats?locality=Whitefield&includeSynthetic=true&areaType=super_built_up");
        var asking = excluded!.Buckets.Single(item => item.RecordType == MarketObservation.RecordTypeAskingPrice);
        Assert.Equal(1, asking.ExcludedOutliers);
        Assert.Equal(8, asking.SampleSize);
        Assert.NotNull(asking.MedianPricePerSqFt);
        Assert.Contains(asking.Exclusions, item => item.Reason == "outlier");

        var included = await _client.GetFromJsonAsync<MarketStatsResponse>(
            "/api/market-observations/stats?locality=Whitefield&includeSynthetic=true&includeOutliers=true&areaType=super_built_up");
        Assert.Equal(90_000_000m, included!.Buckets.Single().MaxAmount);

        var listed = await _client.GetFromJsonAsync<MarketSearchResponse>(
            "/api/market-observations?locality=Whitefield&includeSynthetic=true&includeOutliers=false");
        Assert.DoesNotContain(listed!.Items, item => item.IsOutlier);
    }

    [Fact]
    public async Task Assumption_comparison_and_validation_errors()
    {
        await ImportAsync(FixtureCsv());
        var compared = await _client.PostAsJsonAsync("/api/market-observations/assumption-comparison", new AssumptionComparisonRequest
        {
            Locality = "Whitefield",
            Configuration = "2 BHK",
            IncludeSynthetic = true,
            PurchasePrice = 12_000_000,
            AreaValue = 1200,
            AreaType = MarketObservation.AreaSuperBuiltUp,
            AreaUnit = MarketObservation.UnitSqFt,
        });
        compared.EnsureSuccessStatusCode();
        var body = await compared.Content.ReadFromJsonAsync<AssumptionComparisonResponse>();
        Assert.True(body!.UsingSyntheticTestData);
        Assert.True(body.AskingPrice?.Compared);
        Assert.NotNull(body.AskingPrice?.MedianAmount);
        Assert.NotNull(body.AskingPrice?.AmountDifferencePercent);
        Assert.False(body.AdvertisedRent?.Compared);

        var inverted = await _client.GetAsync(
            "/api/market-observations?locality=Whitefield&observedFrom=2026-12-01&observedTo=2026-01-01");
        Assert.Equal(HttpStatusCode.BadRequest, inverted.StatusCode);

        var badType = await _client.GetAsync("/api/market-observations/stats?locality=Whitefield&recordType=not_a_type");
        Assert.Equal(HttpStatusCode.BadRequest, badType.StatusCode);
    }

    [Fact]
    public async Task Missing_area_omits_price_per_square_foot()
    {
        var request = RequestFactory.ValidMarketObservation();
        request.AreaValue = null;
        request.SourceRecordId = "NO-AREA-001";
        var created = await _client.PostAsJsonAsync("/api/market-observations", request);
        created.EnsureSuccessStatusCode();
        var dto = await created.Content.ReadFromJsonAsync<MarketObservationDto>();
        Assert.Null(dto?.PricePerSqFt);

        var stats = await _client.GetFromJsonAsync<MarketStatsResponse>(
            "/api/market-observations/stats?locality=Whitefield&includeSynthetic=true");
        Assert.Null(stats!.Buckets.Single().MedianPricePerSqFt);
    }

    private async Task<MarketImportResult> ImportAsync(string csv)
    {
        using var content = new MultipartFormDataContent();
        var file = new ByteArrayContent(Encoding.UTF8.GetBytes(csv));
        file.Headers.ContentType = new MediaTypeHeaderValue("text/csv");
        content.Add(file, "file", "observations.csv");
        var response = await _client.PostAsync("/api/market-observations/import/commit", content);
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.IsSuccessStatusCode, body);
        return (await response.Content.ReadFromJsonAsync<MarketImportResult>())!;
    }

    private static string FixtureCsv() =>
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "bengaluru-market-observations.synthetic.csv"));
}
