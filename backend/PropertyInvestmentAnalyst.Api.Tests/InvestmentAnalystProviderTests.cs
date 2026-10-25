using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using PropertyInvestmentAnalyst.Api.Contracts;
using PropertyInvestmentAnalyst.Api.Services.Analyst;

namespace PropertyInvestmentAnalyst.Api.Tests;

public class InvestmentAnalystProviderTests
{
    [Fact]
    public async Task Missing_api_key_returns_not_configured_without_http()
    {
        var handler = new QueueHandler();
        var provider = Create(handler, configured: false);
        var result = await provider.CompleteAsync(Request(), CancellationToken.None);
        Assert.Equal("not_configured", result.Status);
        Assert.Equal(0, handler.Calls);
    }

    [Fact]
    public void Placeholder_api_key_is_not_configured()
    {
        Assert.False(new OpenAiAnalystOptions { ApiKey = "SET_VIA_INVESTMENTANALYST__OPENAI__APIKEY" }.IsConfigured);
    }

    [Fact]
    public async Task Rate_limit_does_not_fabricate_an_answer()
    {
        var handler = new QueueHandler();
        handler.Enqueue(new HttpResponseMessage(HttpStatusCode.TooManyRequests));
        var result = await Create(handler, configured: true).CompleteAsync(Request(), CancellationToken.None);
        Assert.Equal("rate_limited", result.Status);
        Assert.Empty(result.Sections);
    }

    [Fact]
    public async Task Server_failure_does_not_fabricate_an_answer()
    {
        var handler = new QueueHandler();
        handler.Enqueue(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));
        var result = await Create(handler, configured: true).CompleteAsync(Request(), CancellationToken.None);
        Assert.Equal("unavailable", result.Status);
        Assert.Empty(result.Sections);
    }

    [Fact]
    public async Task Malformed_completion_does_not_fabricate_an_answer()
    {
        var handler = new QueueHandler();
        handler.Enqueue(Json(HttpStatusCode.OK, """{"choices":[{"message":{"content":"not-json"}}]}"""));
        var result = await Create(handler, configured: true).CompleteAsync(Request(), CancellationToken.None);
        Assert.Equal("malformed", result.Status);
        Assert.Empty(result.Sections);
    }

    [Fact]
    public async Task Timeout_does_not_fabricate_an_answer()
    {
        var provider = Create(new HangHandler(), configured: true, timeoutSeconds: 1);
        var result = await provider.CompleteAsync(Request(), CancellationToken.None);
        Assert.Equal("timeout", result.Status);
        Assert.Empty(result.Sections);
    }

    [Fact]
    public async Task Valid_json_completion_maps_sections()
    {
        var handler = new QueueHandler();
        var inner =
            """{"summary":"Cash flow is negative.","calculatedNumbers":"Yield is supplied.","assumptions":"Base rates.","risksAndSensitivity":"Lower appreciation.","evidenceAvailable":"Synthetic only.","limitations":"No live feed.","nextQuestions":"Check vacancy."}""";
        handler.Enqueue(Json(
            HttpStatusCode.OK,
            JsonSerializer.Serialize(new
            {
                model = "gpt-4o-mini",
                choices = new[] { new { message = new { content = inner } } },
            })));
        var result = await Create(handler, configured: true).CompleteAsync(Request(), CancellationToken.None);
        Assert.Equal("completed", result.Status);
        Assert.Equal(7, result.Sections.Count);
        Assert.Contains(result.Sections, item => item.Key == "summary" && item.Body.Contains("negative"));
        Assert.Equal(1, handler.Calls);
        Assert.NotNull(handler.LastRequest);
        Assert.Equal("Bearer test-secret-key", handler.LastRequest!.Headers.Authorization?.ToString());
    }

    [Fact]
    public void Redaction_strips_keys_and_bearer_tokens()
    {
        var redacted = AnalystSecretRedaction.Redact(
            "Authorization: Bearer test-secret-key apiKey=test-secret-key sk-abc123",
            "test-secret-key");
        Assert.DoesNotContain("test-secret-key", redacted);
        Assert.DoesNotContain("sk-abc123", redacted);
        Assert.Contains("[redacted]", redacted);
    }

    [Fact]
    public void Parser_rejects_missing_required_fields()
    {
        var ok = InvestmentAnalystResponseParser.TryParse("""{"summary":"x"}""", out _, out var error);
        Assert.False(ok);
        Assert.Contains("calculatedNumbers", error);
    }

    private static AnalystProviderRequest Request() => new()
    {
        Question = "Explain cash flow.",
        ContextJson = """{"warnings":["synthetic"]}""",
    };

    private static OpenAiInvestmentAnalystProvider Create(
        HttpMessageHandler handler,
        bool configured,
        int timeoutSeconds = 5)
    {
        var http = new HttpClient(handler);
        var options = Options.Create(new InvestmentAnalystOptions
        {
            OpenAi = new OpenAiAnalystOptions
            {
                ApiKey = configured ? "test-secret-key" : "",
                BaseUrl = "https://api.openai.com/v1",
                Model = "gpt-4o-mini",
                TimeoutSeconds = timeoutSeconds,
            },
        });
        return new OpenAiInvestmentAnalystProvider(http, options, NullLogger<OpenAiInvestmentAnalystProvider>.Instance);
    }

    private static HttpResponseMessage Json(HttpStatusCode status, string body) =>
        new(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

    private sealed class QueueHandler : HttpMessageHandler
    {
        private readonly Queue<HttpResponseMessage> _responses = new();
        public int Calls { get; private set; }
        public HttpRequestMessage? LastRequest { get; private set; }

        public void Enqueue(HttpResponseMessage response) => _responses.Enqueue(response);

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Calls += 1;
            LastRequest = request;
            return Task.FromResult(_responses.Dequeue());
        }
    }

    private sealed class HangHandler : HttpMessageHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            await Task.Delay(TimeSpan.FromSeconds(30), cancellationToken);
            return new HttpResponseMessage(HttpStatusCode.OK);
        }
    }
}
