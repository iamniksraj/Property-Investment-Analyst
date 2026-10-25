namespace PropertyInvestmentAnalyst.Api.Domain;

public sealed class MarketImportRun
{
    public Guid Id { get; set; }
    public required string ProviderKey { get; set; }
    public required string Status { get; set; }
    public DateTimeOffset StartedUtc { get; set; }
    public DateTimeOffset CompletedUtc { get; set; }
    public DateOnly? ObservedFrom { get; set; }
    public DateOnly? ObservedTo { get; set; }
    public int Inserted { get; set; }
    public int Updated { get; set; }
    public int Skipped { get; set; }
    public int Duplicates { get; set; }
    public int Invalid { get; set; }
    public int ErrorCount { get; set; }
    public required string Summary { get; set; }
}
