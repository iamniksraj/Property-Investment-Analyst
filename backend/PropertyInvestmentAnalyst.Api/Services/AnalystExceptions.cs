namespace PropertyInvestmentAnalyst.Api.Services;

public sealed class AnalystValidationException(IDictionary<string, string[]> errors)
    : Exception("The investment-analyst request is invalid.")
{
    public IDictionary<string, string[]> Errors { get; } = errors;
}

public sealed class AnalystProviderException(string title, string detail, int statusCode)
    : Exception(detail)
{
    public string Title { get; } = title;
    public int StatusCode { get; } = statusCode;
}
