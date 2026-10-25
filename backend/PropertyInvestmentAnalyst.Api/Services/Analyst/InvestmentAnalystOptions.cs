namespace PropertyInvestmentAnalyst.Api.Services.Analyst;

public sealed class InvestmentAnalystOptions
{
    public const string SectionName = "InvestmentAnalyst";

    public OpenAiAnalystOptions OpenAi { get; set; } = new();
}

public sealed class OpenAiAnalystOptions
{
    public string ApiKey { get; set; } = string.Empty;
    public string BaseUrl { get; set; } = "https://api.openai.com/v1";
    public string Model { get; set; } = "gpt-4o-mini";
    public int TimeoutSeconds { get; set; } = 20;
    public int MaxOutputTokens { get; set; } = 1500;

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(ApiKey)
        && !ApiKey.StartsWith("SET_VIA_", StringComparison.Ordinal)
        && !string.IsNullOrWhiteSpace(BaseUrl)
        && !string.IsNullOrWhiteSpace(Model);
}
