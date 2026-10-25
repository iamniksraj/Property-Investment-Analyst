using Microsoft.AspNetCore.Mvc;
using PropertyInvestmentAnalyst.Api.Contracts;
using PropertyInvestmentAnalyst.Api.Services.Analyst;

namespace PropertyInvestmentAnalyst.Api.Controllers;

[ApiController]
[Route("api/investment-analyst")]
public sealed class InvestmentAnalystController(InvestmentAnalystApplicationService analyst) : ControllerBase
{
    [HttpGet("status")]
    public ActionResult<InvestmentAnalystStatusResponse> Status() => Ok(analyst.Status());

    [HttpPost("ask")]
    public async Task<ActionResult<InvestmentAnalystAskResponse>> Ask(
        [FromBody] InvestmentAnalystAskRequest request,
        CancellationToken cancellationToken) =>
        Ok(await analyst.AskAsync(request, cancellationToken));
}
