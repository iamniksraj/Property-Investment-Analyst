using Microsoft.AspNetCore.Mvc;
using PropertyInvestmentAnalyst.Api.Contracts;
using PropertyInvestmentAnalyst.Api.Services;

namespace PropertyInvestmentAnalyst.Api.Controllers;

[ApiController]
[Route("api/market-observations")]
public sealed class MarketObservationsController(MarketObservationApplicationService market) : ControllerBase
{
    [HttpGet("template")]
    public IActionResult Template() =>
        Content(MarketCsv.Template, "text/csv");

    [HttpGet]
    public async Task<ActionResult<MarketSearchResponse>> Search(
        [FromQuery] string locality,
        [FromQuery] string? propertyType,
        [FromQuery] string? configuration,
        [FromQuery] string? recordType,
        [FromQuery] string? areaType,
        [FromQuery] string? areaUnit,
        [FromQuery] string? verificationStatus,
        [FromQuery] bool? includeSynthetic,
        [FromQuery] bool includeOutliers = true,
        [FromQuery] DateOnly? observedFrom = null,
        [FromQuery] DateOnly? observedTo = null,
        [FromQuery] int skip = 0,
        [FromQuery] int? take = null,
        CancellationToken cancellationToken = default) =>
        Ok(await market.SearchAsync(
            locality,
            propertyType,
            configuration,
            recordType,
            cancellationToken,
            includeSynthetic ?? true,
            includeOutliers,
            areaType,
            areaUnit,
            verificationStatus,
            observedFrom,
            observedTo,
            skip,
            take ?? MarketInsightsCalculator.DefaultPageSize));

    [HttpGet("stats")]
    public async Task<ActionResult<MarketStatsResponse>> Stats(
        [FromQuery] string locality,
        [FromQuery] string? propertyType,
        [FromQuery] string? configuration,
        [FromQuery] string? recordType,
        [FromQuery] string? areaType,
        [FromQuery] string? areaUnit,
        [FromQuery] string? verificationStatus,
        [FromQuery] bool? includeSynthetic,
        [FromQuery] bool includeOutliers = false,
        [FromQuery] DateOnly? observedFrom = null,
        [FromQuery] DateOnly? observedTo = null,
        CancellationToken cancellationToken = default)
    {
        var filter = new MarketFilter
        {
            Locality = locality,
            PropertyType = propertyType,
            Configuration = configuration,
            RecordType = recordType,
            AreaType = areaType,
            AreaUnit = areaUnit,
            VerificationStatus = verificationStatus,
            IncludeSynthetic = includeSynthetic ?? false,
            IncludeOutliers = includeOutliers,
            ObservedFrom = observedFrom,
            ObservedTo = observedTo,
            Take = MarketInsightsCalculator.MaxPageSize,
        };
        return Ok(await market.StatsAsync(filter, cancellationToken));
    }

    [HttpPost("assumption-comparison")]
    public async Task<ActionResult<AssumptionComparisonResponse>> CompareAssumption(
        [FromBody] AssumptionComparisonRequest request,
        CancellationToken cancellationToken) =>
        Ok(await market.CompareAssumptionAsync(request, cancellationToken));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<MarketObservationDto>> Get(Guid id, CancellationToken cancellationToken) =>
        Ok(await market.GetAsync(id, cancellationToken));

    [HttpPost]
    public async Task<ActionResult<MarketObservationDto>> Create(
        [FromBody] SaveMarketObservationRequest request,
        CancellationToken cancellationToken)
    {
        var created = await market.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(Get), new { id = created.Id }, created);
    }

    [HttpPost("import/preview")]
    [RequestSizeLimit(MarketCsv.MaxBytes)]
    public async Task<ActionResult<MarketImportResult>> Preview(IFormFile? file, CancellationToken cancellationToken) =>
        Ok(await market.PreviewAsync(await ReadCsvAsync(file), cancellationToken));

    [HttpPost("import/commit")]
    [RequestSizeLimit(MarketCsv.MaxBytes)]
    public async Task<ActionResult<MarketImportResult>> Commit(IFormFile? file, CancellationToken cancellationToken) =>
        Ok(await market.CommitAsync(await ReadCsvAsync(file), cancellationToken));

    private static async Task<string> ReadCsvAsync(IFormFile? file)
    {
        if (file is null || file.Length == 0)
        {
            throw new MarketValidationException(new Dictionary<string, string[]>
            {
                ["file"] = ["Upload a CSV file."],
            });
        }

        if (file.Length > MarketCsv.MaxBytes)
        {
            throw new MarketValidationException(new Dictionary<string, string[]>
            {
                ["file"] = ["CSV files larger than 1 MB are not accepted."],
            });
        }

        using var reader = new StreamReader(file.OpenReadStream());
        return await reader.ReadToEndAsync();
    }
}
