using System.Text.RegularExpressions;

namespace PropertyInvestmentAnalyst.Api.Services.Analyst;

public static class AnalystSecretRedaction
{
    private static readonly Regex SecretPattern = new(
        @"(?i)(bearer|api[_-]?key|token|authorization|sk-[a-z0-9\-]+)\s*[:=]?\s*\S+",
        RegexOptions.Compiled);

    public static string Redact(string? text, params string?[] secrets)
    {
        var value = string.IsNullOrEmpty(text) ? string.Empty : text;
        if (value.Length > 1000)
        {
            value = value[..1000];
        }

        foreach (var secret in secrets)
        {
            if (!string.IsNullOrEmpty(secret))
            {
                value = value.Replace(secret, "[redacted]", StringComparison.Ordinal);
            }
        }

        return SecretPattern.Replace(value, match =>
            match.Value.StartsWith("sk-", StringComparison.OrdinalIgnoreCase)
                ? "[redacted]"
                : Regex.Replace(match.Value, @"\S+$", "[redacted]"));
    }
}
