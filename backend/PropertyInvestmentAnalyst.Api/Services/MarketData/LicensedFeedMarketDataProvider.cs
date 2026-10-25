using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;

namespace PropertyInvestmentAnalyst.Api.Services.MarketData;

public sealed class LicensedFeedMarketDataProvider(
    HttpClient http,
    IOptions<MarketDataOptions> options,
    ILogger<LicensedFeedMarketDataProvider> logger) : IMarketDataProvider
{
    public const string ProviderKey = "licensed_feed";

    public string Key => ProviderKey;

    public MarketProviderDescriptor Describe()
    {
        var configured = options.Value.LicensedFeed.IsConfigured;
        return new MarketProviderDescriptor
        {
            Key = ProviderKey,
            DisplayName = "Licensed listing feed (not configured)",
            Kind = "licensed_api",
            FetchSupported = true,
            Configured = configured,
            LiveFeed = false,
            Coverage = "Would map permitted listing or transaction rows into the existing observation model. No vendor is connected.",
            AccessNotes = configured
                ? "Credentials are present on the server. Refresh still does not imply Bengaluru market coverage until the contract is reviewed."
                : "No BaseUrl and ApiKey are configured. Do not scrape listing portals. Obtain a written licence before enabling this adapter.",
            DocumentationUrl = "https://www.99acres.com/info/terms-and-conditions",
        };
    }

    public async Task<ProviderFetchResult> FetchAsync(MarketProviderFetchRequest request, CancellationToken cancellationToken)
    {
        var feed = options.Value.LicensedFeed;
        if (!feed.IsConfigured)
        {
            logger.LogInformation("Licensed feed refresh skipped because BaseUrl or ApiKey is not configured.");
            return new ProviderFetchResult
            {
                Status = "not_configured",
                Message = "No licensed feed is configured. Set MarketData:LicensedFeed:BaseUrl and ApiKey only after a written provider agreement. This application has no genuine live market feed.",
            };
        }

        http.Timeout = TimeSpan.FromSeconds(Math.Clamp(feed.TimeoutSeconds, 1, 30));
        var retries = Math.Clamp(feed.MaxRetries, 0, 5);
        HttpResponseMessage? response = null;
        for (var attempt = 0; attempt <= retries; attempt += 1)
        {
            using var outbound = new HttpRequestMessage(HttpMethod.Get, AppendQuery(feed.BaseUrl, request));
            outbound.Headers.TryAddWithoutValidation(feed.ApiKeyHeader, feed.ApiKey);
            try
            {
                response = await http.SendAsync(outbound, cancellationToken);
            }
            catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                logger.LogWarning("Licensed feed timed out on attempt {Attempt}.", attempt + 1);
                if (attempt == retries)
                {
                    return Fail("The licensed feed timed out. No observations were imported.");
                }

                continue;
            }
            catch (HttpRequestException exception)
            {
                logger.LogWarning(exception, "Licensed feed request failed on attempt {Attempt}.", attempt + 1);
                if (attempt == retries)
                {
                    return Fail("The licensed feed could not be reached. No observations were imported.");
                }

                continue;
            }

            if (response.StatusCode is HttpStatusCode.TooManyRequests or HttpStatusCode.ServiceUnavailable)
            {
                var delay = ParseRetryAfter(response) ?? TimeSpan.FromMilliseconds(250 * (attempt + 1));
                logger.LogInformation("Licensed feed returned {Status}; waiting {Delay} before retry.", (int)response.StatusCode, delay);
                if (attempt == retries)
                {
                    return Fail($"The licensed feed returned HTTP {(int)response.StatusCode} (rate limited or unavailable).");
                }

                await Task.Delay(delay, cancellationToken);
                continue;
            }

            break;
        }

        if (response is null)
        {
            return Fail("The licensed feed returned no response.");
        }

        if (!response.IsSuccessStatusCode)
        {
            logger.LogWarning("Licensed feed returned HTTP {Status}.", (int)response.StatusCode);
            return Fail($"The licensed feed returned HTTP {(int)response.StatusCode}. No observations were imported.");
        }

        LicensedFeedEnvelope? envelope;
        try
        {
            envelope = await response.Content.ReadFromJsonAsync<LicensedFeedEnvelope>(JsonOptions, cancellationToken);
        }
        catch (JsonException exception)
        {
            logger.LogWarning(exception, "Licensed feed JSON was malformed.");
            return Fail("The licensed feed response was not valid JSON. No observations were imported.");
        }

        if (envelope?.Observations is null)
        {
            return Fail("The licensed feed JSON did not contain an observations array.");
        }

        if (envelope.Observations.Count > 500)
        {
            return Fail("The licensed feed returned more than 500 rows; bound the request before importing.");
        }

        return new ProviderFetchResult
        {
            Status = "fetched",
            Message = $"Fetched {envelope.Observations.Count} row(s) from the configured licensed feed. Asking prices remain advertisements unless recordType is verified_transaction.",
            Observations = envelope.Observations,
        };
    }

    private static Uri AppendQuery(string baseUrl, MarketProviderFetchRequest request)
    {
        var parts = new List<string>();
        if (request.ObservedFrom is not null)
        {
            parts.Add($"observedFrom={Uri.EscapeDataString(request.ObservedFrom.Value.ToString("yyyy-MM-dd"))}");
        }

        if (request.ObservedTo is not null)
        {
            parts.Add($"observedTo={Uri.EscapeDataString(request.ObservedTo.Value.ToString("yyyy-MM-dd"))}");
        }

        if (request.Localities.Count > 0)
        {
            parts.Add($"localities={Uri.EscapeDataString(string.Join(",", request.Localities))}");
        }

        if (parts.Count == 0)
        {
            return new Uri(baseUrl);
        }

        var separator = baseUrl.Contains('?', StringComparison.Ordinal) ? "&" : "?";
        return new Uri(baseUrl + separator + string.Join("&", parts));
    }

    private static TimeSpan? ParseRetryAfter(HttpResponseMessage response)
    {
        if (response.Headers.RetryAfter?.Delta is { } delta)
        {
            return delta;
        }

        return null;
    }

    private static ProviderFetchResult Fail(string message) =>
        new()
        {
            Status = "error",
            Message = message,
            ErrorCount = 1,
        };

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private sealed class LicensedFeedEnvelope
    {
        public List<NormalizedMarketObservation>? Observations { get; set; }
    }
}
