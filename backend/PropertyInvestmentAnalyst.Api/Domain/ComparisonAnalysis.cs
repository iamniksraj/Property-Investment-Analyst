namespace PropertyInvestmentAnalyst.Api.Domain;

public sealed class ComparisonAnalysis
{
    public const int CurrentSchemaVersion = 1;

    public Guid Id { get; set; }
    public required string Name { get; set; }
    public string? Description { get; set; }
    public DateTimeOffset CreatedUtc { get; set; }
    public DateTimeOffset UpdatedUtc { get; set; }
    public int SchemaVersion { get; set; } = CurrentSchemaVersion;
    public ICollection<ComparedProperty> Properties { get; set; } = new List<ComparedProperty>();
}
