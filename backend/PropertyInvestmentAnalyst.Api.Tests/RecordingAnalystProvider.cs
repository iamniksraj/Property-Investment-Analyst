using PropertyInvestmentAnalyst.Api.Contracts;
using PropertyInvestmentAnalyst.Api.Services.Analyst;

namespace PropertyInvestmentAnalyst.Api.Tests;

public sealed class RecordingAnalystProvider : IInvestmentAnalystProvider
{
    public string Key => "test";
    public string DisplayName => "Test analyst";
    public string Model => "test-model";
    public bool IsConfigured { get; set; } = true;
    public int Calls { get; private set; }
    public string? LastQuestion { get; private set; }
    public string? LastContextJson { get; private set; }
    public AnalystProviderResult NextResult { get; set; } = Answered();

    public void Reset()
    {
        IsConfigured = true;
        Calls = 0;
        LastQuestion = null;
        LastContextJson = null;
        NextResult = Answered();
    }

    public Task<AnalystProviderResult> CompleteAsync(AnalystProviderRequest request, CancellationToken cancellationToken)
    {
        Calls += 1;
        LastQuestion = request.Question;
        LastContextJson = request.ContextJson;
        if (!IsConfigured)
        {
            return Task.FromResult(new AnalystProviderResult
            {
                Status = "not_configured",
                Message = "AI not configured",
            });
        }

        return Task.FromResult(NextResult);
    }

    public static AnalystProviderResult Answered() => new()
    {
        Status = "completed",
        Model = "test-model",
        Sections =
        [
            new AnalystSectionDto { Key = "summary", Title = "Summary", Body = "Year-one cash flow is negative after debt service." },
            new AnalystSectionDto { Key = "calculatedNumbers", Title = "What the calculated numbers show", Body = "Gross yield and cash flow are supplied by the TypeScript engine." },
            new AnalystSectionDto { Key = "assumptions", Title = "Main assumptions", Body = "Base appreciation, vacancy and financing are as supplied." },
            new AnalystSectionDto { Key = "risksAndSensitivity", Title = "Risks and sensitivity", Body = "Lower appreciation than Base reduces net profit." },
            new AnalystSectionDto { Key = "evidenceAvailable", Title = "Evidence available", Body = "Attached observations are labelled in context." },
            new AnalystSectionDto { Key = "limitations", Title = "Missing information and limitations", Body = "No genuine live Bengaluru feed is connected." },
            new AnalystSectionDto { Key = "nextQuestions", Title = "Questions to investigate next", Body = "Check vacancy, EMI and whether observations are synthetic." },
        ],
    };
}
