using Microsoft.AspNetCore.Mvc;
using PropertyInvestmentAnalyst.Api.Contracts;
using PropertyInvestmentAnalyst.Api.Services.MarketData;

namespace PropertyInvestmentAnalyst.Api.Controllers;

[ApiController]
[Route("api/market-providers")]
public sealed class MarketProvidersController(MarketProviderCatalogService catalog) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<MarketProviderStatusResponse>> Status(CancellationToken cancellationToken) =>
        Ok(await catalog.StatusAsync(cancellationToken));

    [HttpPost("{providerKey}/refresh")]
    public async Task<ActionResult<MarketProviderRefreshResponse>> Refresh(
        string providerKey,
        [FromBody] MarketProviderRefreshRequest? body,
        CancellationToken cancellationToken)
    {
        var request = new MarketProviderFetchRequest
        {
            ObservedFrom = body?.ObservedFrom,
            ObservedTo = body?.ObservedTo,
        };
        return Ok(await catalog.RefreshAsync(providerKey, request, cancellationToken));
    }
}
