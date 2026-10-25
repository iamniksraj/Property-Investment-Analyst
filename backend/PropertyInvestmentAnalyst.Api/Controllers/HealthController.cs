using Microsoft.AspNetCore.Mvc;
using PropertyInvestmentAnalyst.Api.Contracts;

namespace PropertyInvestmentAnalyst.Api.Controllers;

[ApiController]
[Route("api/health")]
public sealed class HealthController(IHostEnvironment environment) : ControllerBase
{
    [HttpGet]
    public ActionResult<HealthResponse> Get() => Ok(new HealthResponse
    {
        Status = "ok",
        TimestampUtc = DateTimeOffset.UtcNow,
        Environment = environment.EnvironmentName,
    });
}
