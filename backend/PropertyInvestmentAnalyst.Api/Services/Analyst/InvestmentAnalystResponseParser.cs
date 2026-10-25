using System.Text.Json;
using PropertyInvestmentAnalyst.Api.Contracts;

namespace PropertyInvestmentAnalyst.Api.Services.Analyst;

public static class InvestmentAnalystResponseParser
{
    public const int MaxSectionChars = 4000;

    private static readonly (string Json, string Key, string Title)[] Fields =
    [
        ("summary", "summary", "Summary"),
        ("calculatedNumbers", "calculatedNumbers", "What the calculated numbers show"),
        ("assumptions", "assumptions", "Main assumptions"),
        ("risksAndSensitivity", "risksAndSensitivity", "Risks and sensitivity"),
        ("evidenceAvailable", "evidenceAvailable", "Evidence available"),
        ("limitations", "limitations", "Missing information and limitations"),
        ("nextQuestions", "nextQuestions", "Questions to investigate next"),
    ];

    public static bool TryParse(string content, out IReadOnlyList<AnalystSectionDto> sections, out string? error)
    {
        sections = [];
        error = null;
        var json = ExtractJsonObject(content);
        if (json is null)
        {
            error = "The AI provider response was not a JSON object.";
            return false;
        }

        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(json);
        }
        catch (JsonException)
        {
            error = "The AI provider returned malformed JSON.";
            return false;
        }

        using (document)
        {
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                error = "The AI provider response was not a JSON object.";
                return false;
            }

            var parsed = new List<AnalystSectionDto>();
            foreach (var field in Fields)
            {
                if (!document.RootElement.TryGetProperty(field.Json, out var value)
                    || value.ValueKind != JsonValueKind.String)
                {
                    error = $"The AI provider omitted required field '{field.Json}'.";
                    return false;
                }

                var body = Truncate(value.GetString() ?? string.Empty);
                parsed.Add(new AnalystSectionDto { Key = field.Key, Title = field.Title, Body = body });
            }

            sections = parsed;
            return true;
        }
    }

    public static string Truncate(string value) =>
        value.Length <= MaxSectionChars ? value : value[..MaxSectionChars];

    private static string? ExtractJsonObject(string content)
    {
        var trimmed = content.Trim();
        if (trimmed.StartsWith('{') && trimmed.EndsWith('}'))
        {
            return trimmed;
        }

        var start = trimmed.IndexOf('{');
        var end = trimmed.LastIndexOf('}');
        if (start < 0 || end <= start)
        {
            return null;
        }

        return trimmed[start..(end + 1)];
    }
}
