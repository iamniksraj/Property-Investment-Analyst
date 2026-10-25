using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using PropertyInvestmentAnalyst.Api.Contracts;
using PropertyInvestmentAnalyst.Api.Domain;

namespace PropertyInvestmentAnalyst.Api.Tests;

[Collection("SqlServer")]
public class MarketDataApiTests : IAsyncLifetime
{
    private readonly ApiFactory _factory;
    private readonly HttpClient _client;

    public MarketDataApiTests(ApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    public Task InitializeAsync() => _factory.ResetDatabaseAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Preview_reports_invalid_rows_without_inserting()
    {
        var csv = FixtureCsv() + """
Whitefield,Bad row,Apartment,2 BHK,asking_price,-5,1200,super_built_up,sq_ft,Synthetic fixture — not a live listing,,SYN-BAD,2026-10-01,source_cited,n,n,true
""";
        var preview = await ImportAsync("/api/market-observations/import/preview", csv);
        Assert.True(preview.Invalid >= 1);
        Assert.Equal(0, preview.Inserted);

        var search = await _client.GetAsync("/api/market-observations?locality=Whitefield");
        search.EnsureSuccessStatusCode();
        var payload = await search.Content.ReadFromJsonAsync<MarketSearchResponse>();
        Assert.Equal(0, payload?.Count);
    }

    [Fact]
    public async Task Commit_imports_synthetic_fixture_preserves_source_and_skips_duplicates()
    {
        var csv = FixtureCsv();
        var preview = await ImportAsync("/api/market-observations/import/preview", csv);
        Assert.Equal(0, preview.Inserted);
        Assert.Equal(0, preview.Invalid);
        Assert.Contains(preview.Rows, row => row.Status == "valid_outlier");

        var committed = await ImportAsync("/api/market-observations/import/commit", csv);
        Assert.Equal(preview.Valid, committed.Inserted);
        Assert.Contains(committed.Rows, row => row.Observation is { SourceName: "Synthetic fixture — not a live listing", ObservedOn: var date } && date == new DateOnly(2026, 10, 1));

        var duplicate = await ImportAsync("/api/market-observations/import/commit", csv);
        Assert.Equal(0, duplicate.Inserted);
        Assert.Equal(committed.Inserted, duplicate.Duplicates);

        var search = await _client.GetFromJsonAsync<MarketSearchResponse>(
            "/api/market-observations?locality=Whitefield&configuration=2%20BHK&recordType=asking_price");
        Assert.NotNull(search);
        Assert.True(search.ContainsSynthetic);
        Assert.True(search.ContainsStale);
        Assert.True(search.ContainsUnverified);
        Assert.Contains(search.Items, item => item.IsOutlier);
        Assert.Contains(search.Items, item => item.AreaType == MarketObservation.AreaCarpet);
    }

    [Fact]
    public async Task Stats_keep_asking_prices_and_rents_separate_and_omit_mixed_area_psf()
    {
        await ImportAsync("/api/market-observations/import/commit", FixtureCsv());

        var whitefield = await _client.GetFromJsonAsync<MarketStatsResponse>(
            "/api/market-observations/stats?locality=Whitefield&configuration=2%20BHK&includeSynthetic=true");
        var asking = whitefield!.Buckets.Single(item => item.RecordType == MarketObservation.RecordTypeAskingPrice);
        Assert.True(asking.SampleSize >= 5);
        Assert.False(asking.IsSparse);
        Assert.True(asking.MixedAreaDefinitions);
        Assert.NotNull(asking.MedianAmount);
        Assert.Null(asking.MedianPricePerSqFt);
        Assert.Contains("not a completed transaction", asking.Label, StringComparison.OrdinalIgnoreCase);

        var hsr = await _client.GetFromJsonAsync<MarketStatsResponse>(
            "/api/market-observations/stats?locality=HSR%20Layout&includeSynthetic=true");
        var rents = hsr!.Buckets.Single(item => item.RecordType == MarketObservation.RecordTypeAdvertisedRent);
        Assert.True(rents.IsSparse);
        Assert.Contains("Advertised", rents.Label);
        Assert.Null(rents.MedianPricePerSqFt);
        Assert.NotNull(rents.MedianMonthlyRentPerSqFt);
    }

    [Fact]
    public async Task Missing_observation_and_short_locality_return_client_errors()
    {
        var missing = await _client.GetAsync($"/api/market-observations/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);

        var shortLocality = await _client.GetAsync("/api/market-observations?locality=W");
        Assert.Equal(HttpStatusCode.BadRequest, shortLocality.StatusCode);
    }

    [Fact]
    public async Task Manual_create_and_get_round_trip_and_existing_comparisons_still_work()
    {
        var created = await _client.PostAsJsonAsync("/api/market-observations", RequestFactory.ValidMarketObservation());
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var observation = await created.Content.ReadFromJsonAsync<MarketObservationDto>();
        Assert.Equal(new DateOnly(2026, 10, 1), observation?.ObservedOn);
        Assert.Equal("Synthetic fixture — not a live listing", observation?.SourceName);

        var loaded = await _client.GetFromJsonAsync<MarketObservationDto>($"/api/market-observations/{observation!.Id}");
        Assert.Equal(12_000_000, loaded?.Amount);

        var comparison = RequestFactory.ValidRequest();
        var saved = await _client.PostAsJsonAsync("/api/comparisons", comparison);
        Assert.Equal(HttpStatusCode.Created, saved.StatusCode);
    }

    [Fact]
    public async Task Empty_locality_search_is_sparse()
    {
        var search = await _client.GetFromJsonAsync<MarketSearchResponse>("/api/market-observations?locality=Indiranagar");
        Assert.Equal(0, search?.Count);
        Assert.True(search?.IsSparse);
    }

    private async Task<MarketImportResult> ImportAsync(string path, string csv)
    {
        using var content = new MultipartFormDataContent();
        var file = new ByteArrayContent(Encoding.UTF8.GetBytes(csv));
        file.Headers.ContentType = new MediaTypeHeaderValue("text/csv");
        content.Add(file, "file", "observations.csv");
        var response = await _client.PostAsync(path, content);
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.IsSuccessStatusCode, body);
        var result = await response.Content.ReadFromJsonAsync<MarketImportResult>();
        Assert.NotNull(result);
        return result;
    }

    private static string FixtureCsv() =>
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "bengaluru-market-observations.synthetic.csv"));
}
