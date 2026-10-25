using Microsoft.AspNetCore.Mvc;
using PropertyInvestmentAnalyst.Api.Contracts;
using PropertyInvestmentAnalyst.Api.Services;

namespace PropertyInvestmentAnalyst.Api.Controllers;

[ApiController]
[Route("api/comparisons")]
public sealed class ComparisonsController(ComparisonApplicationService comparisons) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<ComparisonSummaryDto>>> List(CancellationToken cancellationToken) =>
        Ok(await comparisons.ListAsync(cancellationToken));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ComparisonDetailDto>> Get(Guid id, CancellationToken cancellationToken) =>
        Ok(await comparisons.GetAsync(id, cancellationToken));

    [HttpPost]
    public async Task<ActionResult<ComparisonDetailDto>> Create(
        [FromBody] SaveComparisonRequest request,
        CancellationToken cancellationToken)
    {
        var created = await comparisons.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(Get), new { id = created.Id }, created);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<ComparisonDetailDto>> Update(
        Guid id,
        [FromBody] SaveComparisonRequest request,
        CancellationToken cancellationToken) =>
        Ok(await comparisons.UpdateAsync(id, request, cancellationToken));

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        await comparisons.DeleteAsync(id, cancellationToken);
        return NoContent();
    }
}
