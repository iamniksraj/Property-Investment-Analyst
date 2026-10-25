using PropertyInvestmentAnalyst.Api.Contracts;
using PropertyInvestmentAnalyst.Api.Domain;

namespace PropertyInvestmentAnalyst.Api.Services;

public static class MarketObservationValidator
{
    public static void Validate(SaveMarketObservationRequest request)
    {
        var errors = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        Validate(request, errors, prefix: null);
        if (errors.Count > 0)
        {
            throw new MarketValidationException(
                errors.ToDictionary(pair => pair.Key, pair => pair.Value.ToArray(), StringComparer.OrdinalIgnoreCase));
        }
    }

    public static List<string> Validate(SaveMarketObservationRequest request, string? prefix)
    {
        var errors = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        Validate(request, errors, prefix);
        return errors.SelectMany(pair => pair.Value).ToList();
    }

    private static void Validate(
        SaveMarketObservationRequest request,
        IDictionary<string, List<string>> errors,
        string? prefix)
    {
        Require(request.Locality, Key(prefix, "locality"), "Locality is required.", errors);
        Require(request.BuilderProject, Key(prefix, "builderProject"), "Project or building name is required.", errors);
        Require(request.Configuration, Key(prefix, "configuration"), "Configuration such as 2 BHK is required.", errors);
        Require(request.SourceName, Key(prefix, "sourceName"), "A source name is required. Do not invent a source.", errors);

        if (request.Amount <= 0)
        {
            Add(errors, Key(prefix, "amount"), "Amount must be greater than zero.");
        }

        if (request.AreaValue is < 0)
        {
            Add(errors, Key(prefix, "areaValue"), "Area cannot be negative.");
        }

        RequireAllowed(request.RecordType, MarketObservation.RecordTypes, Key(prefix, "recordType"), errors,
            "Record type must be asking_price, advertised_rent, verified_transaction or other.");
        RequireAllowed(
            string.IsNullOrWhiteSpace(request.AreaType) ? MarketObservation.AreaUnspecified : request.AreaType,
            MarketObservation.AreaTypes,
            Key(prefix, "areaType"),
            errors,
            "Area type must be super_built_up, built_up, carpet, plot or unspecified.");
        RequireAllowed(
            string.IsNullOrWhiteSpace(request.AreaUnit) ? MarketObservation.UnitSqFt : request.AreaUnit,
            MarketObservation.AreaUnits,
            Key(prefix, "areaUnit"),
            errors,
            "Area unit must be sq_ft or sq_m.");

        if (!string.IsNullOrWhiteSpace(request.VerificationStatus))
        {
            RequireAllowed(
                request.VerificationStatus,
                MarketObservation.VerificationStatuses,
                Key(prefix, "verificationStatus"),
                errors,
                "Verification status must be unverified, source_cited or reviewed.");
        }

        if (!string.IsNullOrWhiteSpace(request.SourceUrl)
            && (!Uri.TryCreate(request.SourceUrl, UriKind.Absolute, out var uri)
                || uri.Scheme is not ("http" or "https")))
        {
            Add(errors, Key(prefix, "sourceUrl"), "Source URL must be an absolute http or https address, or empty.");
        }

        if (request.ObservedOn == default)
        {
            Add(errors, Key(prefix, "observedOn"), "Observation date is required as yyyy-MM-dd.");
        }
        else if (request.ObservedOn > DateOnly.FromDateTime(DateTime.UtcNow.Date.AddDays(1)))
        {
            Add(errors, Key(prefix, "observedOn"), "Observation date cannot be in the future.");
        }
    }

    private static void Require(string value, string key, string message, IDictionary<string, List<string>> errors)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            Add(errors, key, message);
        }
    }

    private static void RequireAllowed(
        string? value,
        IReadOnlyList<string> allowed,
        string key,
        IDictionary<string, List<string>> errors,
        string message)
    {
        if (string.IsNullOrWhiteSpace(value)
            || allowed.All(item => !item.Equals(value.Trim(), StringComparison.OrdinalIgnoreCase)))
        {
            Add(errors, key, message);
        }
    }

    private static string Key(string? prefix, string name) => prefix is null ? name : $"{prefix}.{name}";

    private static void Add(IDictionary<string, List<string>> errors, string key, string message)
    {
        if (!errors.TryGetValue(key, out var list))
        {
            list = [];
            errors[key] = list;
        }

        list.Add(message);
    }
}
