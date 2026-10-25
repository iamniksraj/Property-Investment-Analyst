namespace PropertyInvestmentAnalyst.Api.Services.MarketData;

public static class MarketDataEnvironmentGuard
{
    public static void Apply(IHostEnvironment environment, MarketDataOptions options)
    {
        if (environment.IsProduction())
        {
            options.AllowManualRefresh = false;
        }
    }

    public static bool ManualRefreshAllowed(IHostEnvironment environment, MarketDataOptions options) =>
        !environment.IsProduction() && options.AllowManualRefresh;
}
