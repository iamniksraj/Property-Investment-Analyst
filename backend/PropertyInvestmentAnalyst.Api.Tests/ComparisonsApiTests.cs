using System.Net;
using System.Net.Http.Json;
using PropertyInvestmentAnalyst.Api.Contracts;

namespace PropertyInvestmentAnalyst.Api.Tests;

[Collection("SqlServer")]
public class ComparisonsApiTests : IAsyncLifetime
{
    private readonly ApiFactory _factory;
    private readonly HttpClient _client;

    public ComparisonsApiTests(ApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    public Task InitializeAsync() => _factory.ResetDatabaseAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Health_returns_ok()
    {
        var response = await _client.GetAsync("/api/health");
        response.EnsureSuccessStatusCode();
        var payload = await response.Content.ReadFromJsonAsync<HealthResponse>();
        Assert.Equal("ok", payload?.Status);
    }

    [Fact]
    public async Task Create_and_get_round_trip_independent_scenarios()
    {
        var request = RequestFactory.ValidRequest();
        var second = RequestFactory.ValidProperty("HSR Layout 3 BHK");
        second.PurchasePrice = 18_000_000;
        second.DownPaymentPercent = 25;
        second.Base = RequestFactory.Scenario(9, 4, 5, 5, 2);
        request.Properties.Add(second);

        var createdResponse = await _client.PostAsJsonAsync("/api/comparisons", request);
        Assert.Equal(HttpStatusCode.Created, createdResponse.StatusCode);
        var created = await createdResponse.Content.ReadFromJsonAsync<ComparisonDetailDto>();
        Assert.NotNull(created);
        Assert.Equal(2, created.Properties.Count);
        Assert.Equal(20, created.Properties[0].DownPaymentPercent);
        Assert.Equal(25, created.Properties[1].DownPaymentPercent);
        Assert.Equal(5, created.Properties[0].Base.PropertyAppreciationPercent);
        Assert.Equal(9, created.Properties[1].Base.PropertyAppreciationPercent);

        var loaded = await _client.GetFromJsonAsync<ComparisonDetailDto>($"/api/comparisons/{created.Id}");
        Assert.Equal(created.Name, loaded?.Name);
        Assert.Equal(9, loaded!.Properties[1].Base.PropertyAppreciationPercent);
        Assert.Equal(5, loaded.Properties[0].Base.PropertyAppreciationPercent);
    }

    [Fact]
    public async Task Update_replaces_properties_and_assumptions_transactionally()
    {
        var created = await CreateAsync(RequestFactory.ValidRequest());
        var update = RequestFactory.ValidRequest("Updated desk");
        update.ExpectedUpdatedUtc = created.UpdatedUtc;
        update.Properties[0].Id = created.Properties[0].Id;
        update.Properties[0].MonthlyRent = 55_000;
        update.Properties[0].Base = RequestFactory.Scenario(6, 4, 5, 5, 2);
        update.Properties.Add(RequestFactory.ValidProperty("Second"));

        var updatedResponse = await _client.PutAsJsonAsync($"/api/comparisons/{created.Id}", update);
        var updateBody = await updatedResponse.Content.ReadAsStringAsync();
        Assert.True(updatedResponse.IsSuccessStatusCode, updateBody);
        var updated = await updatedResponse.Content.ReadFromJsonAsync<ComparisonDetailDto>();
        Assert.Equal("Updated desk", updated?.Name);
        Assert.Equal(2, updated!.Properties.Count);
        Assert.Equal(55_000, updated.Properties[0].MonthlyRent);
        Assert.Equal(6, updated.Properties[0].Base.PropertyAppreciationPercent);
        Assert.Contains(updated.Properties, item => item.Name == "Second");
    }

    [Fact]
    public async Task Update_with_stale_timestamp_returns_conflict()
    {
        var created = await CreateAsync(RequestFactory.ValidRequest());
        var stale = RequestFactory.ValidRequest("Stale");
        stale.ExpectedUpdatedUtc = created.UpdatedUtc.AddMinutes(-5);
        stale.Properties[0].Id = created.Properties[0].Id;

        var response = await _client.PutAsJsonAsync($"/api/comparisons/{created.Id}", stale);
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Delete_removes_analysis_and_children()
    {
        var created = await CreateAsync(RequestFactory.ValidRequest());
        var deleted = await _client.DeleteAsync($"/api/comparisons/{created.Id}");
        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);

        var missing = await _client.GetAsync($"/api/comparisons/{created.Id}");
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
    }

    [Fact]
    public async Task Missing_id_and_malformed_body_return_client_errors()
    {
        var missing = await _client.GetAsync($"/api/comparisons/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);

        var malformed = await _client.PostAsync(
            "/api/comparisons",
            new StringContent("{", System.Text.Encoding.UTF8, "application/json"));
        Assert.Equal(HttpStatusCode.BadRequest, malformed.StatusCode);
    }

    [Fact]
    public async Task Rejects_more_than_three_properties()
    {
        var request = RequestFactory.ValidRequest();
        request.Properties.Add(RequestFactory.ValidProperty("Two"));
        request.Properties.Add(RequestFactory.ValidProperty("Three"));
        request.Properties.Add(RequestFactory.ValidProperty("Four"));

        var response = await _client.PostAsJsonAsync("/api/comparisons", request);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Update_rejects_property_ids_from_another_comparison()
    {
        var created = await CreateAsync(RequestFactory.ValidRequest());
        var update = RequestFactory.ValidRequest("Hijack");
        update.ExpectedUpdatedUtc = created.UpdatedUtc;
        update.Properties[0].Id = Guid.NewGuid();

        var response = await _client.PutAsJsonAsync($"/api/comparisons/{created.Id}", update);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private async Task<ComparisonDetailDto> CreateAsync(SaveComparisonRequest request)
    {
        var response = await _client.PostAsJsonAsync("/api/comparisons", request);
        response.EnsureSuccessStatusCode();
        var created = await response.Content.ReadFromJsonAsync<ComparisonDetailDto>();
        Assert.NotNull(created);
        return created;
    }
}
