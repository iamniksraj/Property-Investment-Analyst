using Microsoft.Extensions.Options;
using PropertyInvestmentAnalyst.Api.Contracts;

namespace PropertyInvestmentAnalyst.Api.Services.Analyst;

public sealed class InvestmentAnalystApplicationService(
    IInvestmentAnalystProvider provider,
    ComparisonApplicationService comparisons,
    MarketObservationApplicationService market,
    IOptions<InvestmentAnalystOptions> options,
    ILogger<InvestmentAnalystApplicationService> logger)
{
    public InvestmentAnalystStatusResponse Status()
    {
        var model = options.Value.OpenAi.Model;
        return new InvestmentAnalystStatusResponse
        {
            Configured = provider.IsConfigured,
            Provider = provider.DisplayName,
            Model = provider.IsConfigured ? model : string.Empty,
            SetupGuidance = InvestmentAnalystPrompts.SetupGuidance,
            Disclaimer = InvestmentAnalystPrompts.Disclaimer,
            HasGenuineLiveFeed = false,
        };
    }

    public async Task<InvestmentAnalystAskResponse> AskAsync(
        InvestmentAnalystAskRequest request,
        CancellationToken cancellationToken)
    {
        InvestmentAnalystContextBuilder.Validate(request);

        ComparisonDetailDto? saved = null;
        if (request.ComparisonId is { } comparisonId)
        {
            saved = await comparisons.GetAsync(comparisonId, cancellationToken);
        }

        var observations = new List<MarketObservationDto>();
        foreach (var id in request.ObservationIds.Distinct().Take(InvestmentAnalystContextBuilder.MaxObservationIds))
        {
            observations.Add(await market.GetAsync(id, cancellationToken));
        }

        if (request.IncludeMarketEvidence)
        {
            await AttachLocalityEvidenceAsync(saved, request.Properties, observations, cancellationToken);
        }

        var warnings = InvestmentAnalystContextBuilder.BuildWarnings(saved, request.Properties, observations);
        var contextJson = InvestmentAnalystContextBuilder.Serialize(
            request.Question,
            saved,
            request.Properties,
            observations,
            warnings);

        logger.LogInformation(
            "Investment analyst ask: provider={Provider}, configured={Configured}, questionChars={Chars}, comparison={Comparison}, observations={Observations}, properties={Properties}.",
            provider.Key,
            provider.IsConfigured,
            request.Question.Trim().Length,
            request.ComparisonId,
            observations.Count,
            request.Properties.Count);

        var completion = await provider.CompleteAsync(
            new AnalystProviderRequest
            {
                Question = request.Question.Trim(),
                ContextJson = contextJson,
            },
            cancellationToken);

        if (completion.Status == "not_configured" || !provider.IsConfigured)
        {
            return new InvestmentAnalystAskResponse
            {
                Status = "not_configured",
                Disclaimer = InvestmentAnalystPrompts.Disclaimer,
                SetupGuidance = InvestmentAnalystPrompts.SetupGuidance,
                Sections = [],
                Evidence = ToEvidence(saved, observations, request.Properties.Count > 0),
                Warnings = warnings,
                WroteToDatabase = false,
            };
        }

        if (!completion.Succeeded)
        {
            var title = completion.Status switch
            {
                "timeout" => "AI provider timed out",
                "rate_limited" => "AI provider rate limited",
                "malformed" => "AI provider response was invalid",
                _ => "AI provider unavailable",
            };
            var statusCode = completion.Status == "rate_limited" ? StatusCodes.Status429TooManyRequests : StatusCodes.Status502BadGateway;
            throw new AnalystProviderException(
                title,
                completion.Message ?? "The AI provider failed. No fabricated answer was returned.",
                statusCode);
        }

        return new InvestmentAnalystAskResponse
        {
            Status = "answered",
            Disclaimer = InvestmentAnalystPrompts.Disclaimer,
            Sections = completion.Sections
                .Select(item => new AnalystSectionDto
                {
                    Key = item.Key,
                    Title = item.Title,
                    Body = InvestmentAnalystResponseParser.Truncate(item.Body),
                })
                .ToArray(),
            Evidence = ToEvidence(saved, observations, request.Properties.Count > 0),
            Warnings = warnings,
            WroteToDatabase = false,
        };
    }

    private async Task AttachLocalityEvidenceAsync(
        ComparisonDetailDto? saved,
        IReadOnlyList<AnalystPropertyContextDto> properties,
        List<MarketObservationDto> observations,
        CancellationToken cancellationToken)
    {
        var localities = (saved?.Properties.Select(item => item.Locality) ?? [])
            .Concat(properties.Select(item => item.Locality))
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Select(item => item!.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(3)
            .ToArray();

        foreach (var locality in localities)
        {
            if (observations.Count >= InvestmentAnalystContextBuilder.MaxMarketRows)
            {
                break;
            }

            var remaining = InvestmentAnalystContextBuilder.MaxMarketRows - observations.Count;
            var search = await market.SearchAsync(
                new MarketFilter
                {
                    Locality = locality,
                    IncludeSynthetic = true,
                    IncludeOutliers = true,
                    Take = remaining,
                },
                cancellationToken);

            foreach (var item in search.Items)
            {
                if (observations.All(existing => existing.Id != item.Id))
                {
                    observations.Add(item);
                }
            }
        }
    }

    private static IReadOnlyList<AnalystEvidenceReferenceDto> ToEvidence(
        ComparisonDetailDto? saved,
        IReadOnlyList<MarketObservationDto> observations,
        bool hasCalculated)
    {
        var items = new List<AnalystEvidenceReferenceDto>();
        if (saved is not null)
        {
            items.Add(new AnalystEvidenceReferenceDto
            {
                Kind = "comparison",
                Id = saved.Id.ToString(),
                Label = saved.Name,
                Href = "/compare",
            });
        }

        if (hasCalculated)
        {
            items.Add(new AnalystEvidenceReferenceDto
            {
                Kind = "calculated_metrics",
                Label = "Browser-calculated metrics (TypeScript engine)",
                Href = "/compare",
            });
        }

        foreach (var observation in observations)
        {
            items.Add(new AnalystEvidenceReferenceDto
            {
                Kind = "observation",
                Id = observation.Id.ToString(),
                Label = $"{observation.Locality} {observation.RecordType} ({observation.SourceName})",
                Href = "/market",
                IsSyntheticTestData = observation.IsSyntheticTestData,
                IsStale = observation.IsStale,
                RecordType = observation.RecordType,
                VerificationStatus = observation.VerificationStatus,
            });
        }

        return items;
    }
}
