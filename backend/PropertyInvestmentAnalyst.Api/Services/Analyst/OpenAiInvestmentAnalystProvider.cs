using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;
using PropertyInvestmentAnalyst.Api.Contracts;

namespace PropertyInvestmentAnalyst.Api.Services.Analyst;

public sealed class OpenAiInvestmentAnalystProvider(
    HttpClient http,
    IOptions<InvestmentAnalystOptions> options,
    ILogger<OpenAiInvestmentAnalystProvider> logger) : IInvestmentAnalystProvider
{
    public const string ProviderKey = "openai";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public string Key => ProviderKey;
    public string DisplayName => "OpenAI Chat Completions";
    public string Model => options.Value.OpenAi.Model;
    public bool IsConfigured => options.Value.OpenAi.IsConfigured;

    public async Task<AnalystProviderResult> CompleteAsync(
        AnalystProviderRequest request,
        CancellationToken cancellationToken)
    {
        var openAi = options.Value.OpenAi;
        if (!openAi.IsConfigured)
        {
            logger.LogInformation("Investment analyst skipped: OpenAI API key is not configured.");
            return new AnalystProviderResult
            {
                Status = "not_configured",
                Message = "AI not configured",
                Model = openAi.Model,
            };
        }

        http.Timeout = TimeSpan.FromSeconds(Math.Clamp(openAi.TimeoutSeconds, 1, 60));
        using var outbound = new HttpRequestMessage(HttpMethod.Post, Combine(openAi.BaseUrl, "chat/completions"));
        outbound.Headers.Authorization = new AuthenticationHeaderValue("Bearer", openAi.ApiKey);
        outbound.Content = new StringContent(
            JsonSerializer.Serialize(new
            {
                model = openAi.Model,
                temperature = 0.2,
                max_tokens = Math.Clamp(openAi.MaxOutputTokens, 200, 2000),
                response_format = new { type = "json_object" },
                messages = new object[]
                {
                    new { role = "system", content = InvestmentAnalystPrompts.System },
                    new
                    {
                        role = "user",
                        content = "User question:\n" + request.Question + "\n\nApplication context JSON:\n" + request.ContextJson,
                    },
                },
            }, JsonOptions),
            Encoding.UTF8,
            "application/json");

        HttpResponseMessage response;
        try
        {
            response = await http.SendAsync(outbound, cancellationToken);
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning("OpenAI request timed out for model {Model}.", openAi.Model);
            return Fail("timeout", "The AI provider timed out. No fabricated answer was returned.");
        }
        catch (HttpRequestException exception)
        {
            logger.LogWarning(
                exception,
                "OpenAI request failed: {Message}",
                AnalystSecretRedaction.Redact(exception.Message, openAi.ApiKey));
            return Fail("unavailable", "The AI provider could not be reached. No fabricated answer was returned.");
        }

        var status = (int)response.StatusCode;
        logger.LogInformation("OpenAI completed with HTTP {Status} for model {Model}.", status, openAi.Model);

        if (response.StatusCode == HttpStatusCode.TooManyRequests)
        {
            return Fail("rate_limited", "The AI provider returned HTTP 429. Try again later.");
        }

        if (status >= 500)
        {
            return Fail("unavailable", $"The AI provider returned HTTP {status}. No fabricated answer was returned.");
        }

        if (!response.IsSuccessStatusCode)
        {
            return Fail("unavailable", $"The AI provider returned HTTP {status}. Check the server configuration.");
        }

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        ChatCompletionResponse? payload;
        try
        {
            payload = await JsonSerializer.DeserializeAsync<ChatCompletionResponse>(stream, JsonOptions, cancellationToken);
        }
        catch (JsonException)
        {
            return Fail("malformed", "The AI provider returned malformed JSON.");
        }

        var content = payload?.Choices is { Count: > 0 } choices ? choices[0].Message?.Content : null;
        if (string.IsNullOrWhiteSpace(content))
        {
            return Fail("malformed", "The AI provider returned an empty completion.");
        }

        if (!InvestmentAnalystResponseParser.TryParse(content, out var sections, out var error))
        {
            return Fail("malformed", error ?? "The AI provider response could not be parsed.");
        }

        return new AnalystProviderResult
        {
            Status = "completed",
            Sections = sections,
            Model = payload?.Model ?? openAi.Model,
        };
    }

    private static AnalystProviderResult Fail(string status, string message) =>
        new() { Status = status, Message = message };

    private static string Combine(string baseUrl, string path)
    {
        var trimmed = baseUrl.TrimEnd('/');
        return trimmed.EndsWith("/v1", StringComparison.OrdinalIgnoreCase)
            ? trimmed + "/" + path
            : trimmed + "/v1/" + path;
    }

    private sealed class ChatCompletionResponse
    {
        public string? Model { get; set; }
        public List<ChatChoice> Choices { get; set; } = [];
    }

    private sealed class ChatChoice
    {
        public ChatMessage? Message { get; set; }
    }

    private sealed class ChatMessage
    {
        public string? Content { get; set; }
    }
}
