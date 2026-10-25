using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PropertyInvestmentAnalyst.Api.Data;
using PropertyInvestmentAnalyst.Api.Services.Analyst;

namespace PropertyInvestmentAnalyst.Api.Tests;

public sealed class ApiFactory : WebApplicationFactory<Program>
{
    public const string TestConnectionString =
        "Server=localhost;Database=PropertyInvestmentAnalyst_Tests;Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=True";

    public RecordingAnalystProvider Analyst { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = TestConnectionString,
                ["Cors:AllowedOrigins:0"] = "http://localhost:4200",
                ["MarketData:AllowManualRefresh"] = "true",
            });
        });
        builder.ConfigureTestServices(services =>
        {
            services.AddSingleton<IInvestmentAnalystProvider>(Analyst);
        });
    }

    public async Task ResetDatabaseAsync()
    {
        Analyst.Reset();
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.EnsureDeletedAsync();
        await db.Database.MigrateAsync();
    }
}
