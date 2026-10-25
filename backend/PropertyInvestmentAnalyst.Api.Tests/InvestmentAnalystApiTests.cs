using System.Net;
using System.Net.Http.Json;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PropertyInvestmentAnalyst.Api.Contracts;
using PropertyInvestmentAnalyst.Api.Data;
using PropertyInvestmentAnalyst.Api.Services.Analyst;

namespace PropertyInvestmentAnalyst.Api.Tests;

[Collection("SqlServer")]
public class InvestmentAnalystApiTests : IAsyncLifetime
{
    private readonly ApiFactory _factory;
    private readonly HttpClient _client;

    public InvestmentAnalystApiTests(ApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    public Task InitializeAsync() => _factory.ResetDatabaseAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Status_reports_test_provider_without_secrets()
    {
        var status = await _client.GetFromJsonAsync<InvestmentAnalystStatusResponse>("/api/investment-analyst/status");
        Assert.True(status?.Configured);
        Assert.Equal("Test analyst", status?.Provider);
        Assert.False(status?.HasGenuineLiveFeed);
        var raw = await _client.GetStringAsync("/api/investment-analyst/status");
        Assert.DoesNotContain("sk-", raw, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Bearer ", raw, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Missing_configuration_returns_clear_status_without_an_answer()
    {
        _factory.Analyst.IsConfigured = false;
        var response = await _client.PostAsJsonAsync("/api/investment-analyst/ask", new InvestmentAnalystAskRequest
        {
            Question = "What are the key risks?",
        });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<InvestmentAnalystAskResponse>();
        Assert.Equal("not_configured", body?.Status);
        Assert.Contains("InvestmentAnalyst__OpenAI__ApiKey", body?.SetupGuidance);
        Assert.Empty(body!.Sections);
        Assert.False(body.WroteToDatabase);
    }

    [Fact]
    public async Task Empty_question_is_rejected()
    {
        var response = await _client.PostAsJsonAsync("/api/investment-analyst/ask", new InvestmentAnalystAskRequest { Question = "  " });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(0, _factory.Analyst.Calls);
    }

    [Fact]
    public async Task Oversized_question_is_rejected()
    {
        var response = await _client.PostAsJsonAsync("/api/investment-analyst/ask", new InvestmentAnalystAskRequest
        {
            Question = new string('x', InvestmentAnalystContextBuilder.MaxQuestionChars + 1),
        });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(0, _factory.Analyst.Calls);
    }

    [Fact]
    public async Task Out_of_range_metrics_are_rejected()
    {
        var response = await _client.PostAsJsonAsync("/api/investment-analyst/ask", new InvestmentAnalystAskRequest
        {
            Question = "Explain this property.",
            Properties =
            [
                new AnalystPropertyContextDto
                {
                    Name = "Out of range",
                    PurchasePrice = 2_000_000_000_000m,
                },
            ],
        });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(0, _factory.Analyst.Calls);
    }

    [Fact]
    public async Task Missing_comparison_returns_not_found()
    {
        var response = await _client.PostAsJsonAsync("/api/investment-analyst/ask", new InvestmentAnalystAskRequest
        {
            Question = "Summarise this analysis.",
            ComparisonId = Guid.Parse("aaaaaaaa-bbbb-4ccc-8ddd-eeeeeeeeeeee"),
        });
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal(0, _factory.Analyst.Calls);
    }

    [Fact]
    public async Task Missing_observation_returns_not_found()
    {
        var response = await _client.PostAsJsonAsync("/api/investment-analyst/ask", new InvestmentAnalystAskRequest
        {
            Question = "What does this observation imply?",
            ObservationIds = [Guid.Parse("aaaaaaaa-bbbb-4ccc-8ddd-eeeeeeeeeeee")],
            IncludeMarketEvidence = false,
        });
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Ask_loads_saved_comparison_and_retains_synthetic_warnings_without_writes()
    {
        var created = await CreateComparisonAsync();
        var observation = await CreateSyntheticObservationAsync();

        int comparisonsBefore;
        int observationsBefore;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            comparisonsBefore = await db.ComparisonAnalyses.CountAsync();
            observationsBefore = await db.MarketObservations.CountAsync();
        }

        var response = await _client.PostAsJsonAsync("/api/investment-analyst/ask", new InvestmentAnalystAskRequest
        {
            Question = "Compare these apartments and explain the main differences in their investment returns.",
            ComparisonId = created.Id,
            ObservationIds = [observation.Id],
            IncludeMarketEvidence = true,
            Properties =
            [
                new AnalystPropertyContextDto
                {
                    Name = "Whitefield 2 BHK",
                    Locality = "Whitefield",
                    PurchasePrice = 12_000_000,
                    Snapshot = new AnalystSnapshotMetricsDto { GrossAnnualRentalYieldPercent = 4 },
                    Mortgage = new AnalystMortgageMetricsDto
                    {
                        AnnualCashFlowAfterOperatingExpensesAndLoanPayments = -120_000,
                    },
                    Base = new AnalystScenarioMetricsDto
                    {
                        PropertyAppreciationPercent = 5,
                        NetProfit = 1_000_000,
                        AnnualisedEquityIrr = 6.5m,
                    },
                },
            ],
        });

        var bodyText = await response.Content.ReadAsStringAsync();
        Assert.True(response.IsSuccessStatusCode, bodyText);
        var body = await response.Content.ReadFromJsonAsync<InvestmentAnalystAskResponse>();
        Assert.Equal("answered", body?.Status);
        Assert.False(body!.WroteToDatabase);
        Assert.Contains(body.Evidence, item => item.Kind == "comparison" && item.Id == created.Id.ToString());
        Assert.Contains(body.Evidence, item => item.Kind == "observation" && item.IsSyntheticTestData);
        Assert.Contains(body.Warnings, item => item.Contains("synthetic", StringComparison.OrdinalIgnoreCase));
        Assert.Equal(1, _factory.Analyst.Calls);
        Assert.Contains(created.Id.ToString(), _factory.Analyst.LastContextJson);
        Assert.Contains("browser_typescript_engine", _factory.Analyst.LastContextJson);
        Assert.Contains("isSyntheticTestData", _factory.Analyst.LastContextJson);
        Assert.Contains("no verified market conclusion", _factory.Analyst.LastContextJson, StringComparison.OrdinalIgnoreCase);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            Assert.Equal(comparisonsBefore, await db.ComparisonAnalyses.CountAsync());
            Assert.Equal(observationsBefore, await db.MarketObservations.CountAsync());
        }
    }

    [Fact]
    public async Task Provider_timeout_returns_problem_details_without_an_answer()
    {
        _factory.Analyst.NextResult = new AnalystProviderResult
        {
            Status = "timeout",
            Message = "The AI provider timed out. No fabricated answer was returned.",
        };
        var response = await _client.PostAsJsonAsync("/api/investment-analyst/ask", new InvestmentAnalystAskRequest
        {
            Question = "Summarise this analysis in a report I can review before making a decision.",
        });
        Assert.Equal(HttpStatusCode.BadGateway, response.StatusCode);
        var payload = await response.Content.ReadAsStringAsync();
        Assert.Contains("timed out", payload, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Buy this", payload, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Provider_rate_limit_returns_429()
    {
        _factory.Analyst.NextResult = new AnalystProviderResult
        {
            Status = "rate_limited",
            Message = "The AI provider returned HTTP 429. Try again later.",
        };
        var response = await _client.PostAsJsonAsync("/api/investment-analyst/ask", new InvestmentAnalystAskRequest
        {
            Question = "What happens if appreciation is lower than the Base scenario?",
        });
        Assert.Equal((HttpStatusCode)429, response.StatusCode);
    }

    private async Task<ComparisonDetailDto> CreateComparisonAsync()
    {
        var createdResponse = await _client.PostAsJsonAsync("/api/comparisons", RequestFactory.ValidRequest());
        createdResponse.EnsureSuccessStatusCode();
        var created = await createdResponse.Content.ReadFromJsonAsync<ComparisonDetailDto>();
        Assert.NotNull(created);
        return created;
    }

    private async Task<MarketObservationDto> CreateSyntheticObservationAsync()
    {
        var createdResponse = await _client.PostAsJsonAsync("/api/market-observations", RequestFactory.ValidMarketObservation());
        createdResponse.EnsureSuccessStatusCode();
        var created = await createdResponse.Content.ReadFromJsonAsync<MarketObservationDto>();
        Assert.NotNull(created);
        return created;
    }
}

public class InvestmentAnalystContextBuilderTests
{
    [Fact]
    public void Serialize_keeps_synthetic_and_provenance_flags()
    {
        var json = InvestmentAnalystContextBuilder.Serialize(
            "Explain risks.",
            null,
            [],
            [
                new MarketObservationDto
                {
                    Id = Guid.Parse("11111111-1111-4111-8111-111111111111"),
                    Locality = "Whitefield",
                    BuilderProject = "Fixture",
                    PropertyType = "Apartment",
                    Configuration = "2 BHK",
                    RecordType = "asking_price",
                    VerificationStatus = "source_cited",
                    Amount = 12_000_000,
                    AreaType = "super_built_up",
                    AreaUnit = "sq_ft",
                    SourceName = "Synthetic fixture — not a live listing",
                    ObservedOn = new DateOnly(2026, 10, 1),
                    ImportedUtc = DateTimeOffset.UtcNow,
                    IsSyntheticTestData = true,
                    IsOutlier = false,
                    IsStale = true,
                    IsUnverified = false,
                    HasCompleteSource = true,
                },
            ],
            InvestmentAnalystContextBuilder.BuildWarnings(null, [], [
                new MarketObservationDto
                {
                    Id = Guid.NewGuid(),
                    Locality = "Whitefield",
                    BuilderProject = "Fixture",
                    PropertyType = "Apartment",
                    Configuration = "2 BHK",
                    RecordType = "asking_price",
                    VerificationStatus = "source_cited",
                    Amount = 1,
                    AreaType = "super_built_up",
                    AreaUnit = "sq_ft",
                    SourceName = "Synthetic",
                    ObservedOn = new DateOnly(2026, 10, 1),
                    ImportedUtc = DateTimeOffset.UtcNow,
                    IsSyntheticTestData = true,
                    IsOutlier = false,
                    IsStale = true,
                    IsUnverified = false,
                    HasCompleteSource = true,
                },
            ]));

        Assert.Contains("isSyntheticTestData", json);
        Assert.Contains("genuineMarketEvidence\":false", json);
        Assert.Contains("no verified market conclusion", json, StringComparison.OrdinalIgnoreCase);
        Assert.True(Encoding.UTF8.GetByteCount(json) > 0);
        Assert.True(json.Length <= InvestmentAnalystContextBuilder.MaxContextChars);
    }
}
