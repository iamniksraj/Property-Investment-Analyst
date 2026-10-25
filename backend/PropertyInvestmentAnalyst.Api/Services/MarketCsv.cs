using System.Globalization;
using System.Text;

namespace PropertyInvestmentAnalyst.Api.Services;

public static class MarketCsv
{
    public const int MaxRows = 500;
    public const int MaxBytes = 1_000_000;

    public static readonly string[] RequiredHeaders =
    [
        "locality",
        "builderProject",
        "propertyType",
        "configuration",
        "recordType",
        "amount",
        "areaValue",
        "areaType",
        "areaUnit",
        "sourceName",
        "sourceUrl",
        "sourceRecordId",
        "observedOn",
        "verificationStatus",
        "qualityNotes",
        "licenceOrUsageNotes",
        "isSyntheticTestData",
    ];

    public static string Template { get; } =
        string.Join(",", RequiredHeaders) + """

Whitefield,Synthetic fixture tower A,Apartment,2 BHK,asking_price,12000000,1200,super_built_up,sq_ft,Synthetic fixture — not a live listing,,SYN-WF-001,2026-10-01,source_cited,Test fixture. Do not treat as a live asking price.,Test data only. Not for valuation or publication as market evidence.,true
""";

    public static List<string[]> Parse(string csv)
    {
        var rows = new List<string[]>();
        using var reader = new StringReader(csv);
        var buffer = new StringBuilder();
        var fields = new List<string>();
        var inQuotes = false;
        int current;

        while ((current = reader.Read()) != -1)
        {
            var ch = (char)current;
            if (inQuotes)
            {
                if (ch == '"')
                {
                    var peek = reader.Peek();
                    if (peek == '"')
                    {
                        reader.Read();
                        buffer.Append('"');
                    }
                    else
                    {
                        inQuotes = false;
                    }
                }
                else
                {
                    buffer.Append(ch);
                }

                continue;
            }

            switch (ch)
            {
                case '"':
                    inQuotes = true;
                    break;
                case ',':
                    fields.Add(buffer.ToString());
                    buffer.Clear();
                    break;
                case '\r':
                    break;
                case '\n':
                    fields.Add(buffer.ToString());
                    buffer.Clear();
                    if (fields.Any(item => item.Length > 0))
                    {
                        rows.Add([.. fields]);
                    }

                    fields.Clear();
                    break;
                default:
                    buffer.Append(ch);
                    break;
            }
        }

        if (buffer.Length > 0 || fields.Count > 0)
        {
            fields.Add(buffer.ToString());
            if (fields.Any(item => item.Length > 0))
            {
                rows.Add([.. fields]);
            }
        }

        return rows;
    }

    public static string? Cell(IReadOnlyDictionary<string, int> headers, string[] row, string name)
    {
        if (!headers.TryGetValue(name, out var index) || index >= row.Length)
        {
            return null;
        }

        var value = row[index].Trim();
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }

    public static Dictionary<string, int> HeaderMap(string[] headerRow)
    {
        var map = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (var index = 0; index < headerRow.Length; index += 1)
        {
            var name = headerRow[index].Trim().TrimStart('\uFEFF');
            if (name.Length > 0)
            {
                map[name] = index;
            }
        }

        return map;
    }

    public static bool TryDecimal(string? value, out decimal parsed)
    {
        parsed = 0;
        return value is not null
            && decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out parsed);
    }

    public static bool TryDate(string? value, out DateOnly parsed)
    {
        parsed = default;
        return value is not null
            && DateOnly.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out parsed);
    }

    public static bool TryBool(string? value, out bool parsed)
    {
        parsed = false;
        if (string.IsNullOrWhiteSpace(value))
        {
            return true;
        }

        if (value.Equals("true", StringComparison.OrdinalIgnoreCase)
            || value.Equals("yes", StringComparison.OrdinalIgnoreCase)
            || value == "1")
        {
            parsed = true;
            return true;
        }

        if (value.Equals("false", StringComparison.OrdinalIgnoreCase)
            || value.Equals("no", StringComparison.OrdinalIgnoreCase)
            || value == "0")
        {
            parsed = false;
            return true;
        }

        return false;
    }
}
