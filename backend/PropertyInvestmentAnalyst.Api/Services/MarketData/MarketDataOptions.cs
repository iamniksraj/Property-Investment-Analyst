namespace PropertyInvestmentAnalyst.Api.Services.MarketData;

public sealed class MarketDataOptions
{
    public const string SectionName = "MarketData";

    public bool AllowManualRefresh { get; set; }
    public LicensedFeedOptions LicensedFeed { get; set; } = new();
}

public sealed class LicensedFeedOptions
{
    public string BaseUrl { get; set; } = string.Empty;
    public string ApiKey { get; set; } = string.Empty;
    public string ApiKeyHeader { get; set; } = "X-Api-Key";
    public int TimeoutSeconds { get; set; } = 10;
    public int MaxRetries { get; set; } = 2;

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(BaseUrl) && !string.IsNullOrWhiteSpace(ApiKey);
}
