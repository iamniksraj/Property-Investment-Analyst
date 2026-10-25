using Microsoft.EntityFrameworkCore;
using PropertyInvestmentAnalyst.Api.Domain;

namespace PropertyInvestmentAnalyst.Api.Data;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<ComparisonAnalysis> ComparisonAnalyses => Set<ComparisonAnalysis>();
    public DbSet<ComparedProperty> ComparedProperties => Set<ComparedProperty>();
    public DbSet<PropertyScenarioAssumption> PropertyScenarioAssumptions => Set<PropertyScenarioAssumption>();
    public DbSet<MarketObservation> MarketObservations => Set<MarketObservation>();
    public DbSet<MarketImportRun> MarketImportRuns => Set<MarketImportRun>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ComparisonAnalysis>(entity =>
        {
            entity.ToTable("ComparisonAnalyses");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.Name).HasMaxLength(200).IsRequired();
            entity.Property(item => item.Description).HasMaxLength(1000);
            entity.Property(item => item.SchemaVersion).HasDefaultValue(ComparisonAnalysis.CurrentSchemaVersion);
            entity.HasIndex(item => item.UpdatedUtc);
            entity.HasMany(item => item.Properties)
                .WithOne(item => item.Comparison)
                .HasForeignKey(item => item.ComparisonAnalysisId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ComparedProperty>(entity =>
        {
            entity.ToTable("ComparedProperties");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.Name).HasMaxLength(120).IsRequired();
            entity.Property(item => item.Locality).HasMaxLength(80).IsRequired();
            entity.Property(item => item.BuilderProject).HasMaxLength(120).IsRequired();
            entity.Property(item => item.PropertyType).HasMaxLength(40).IsRequired();
            entity.Property(item => item.PurchasePrice).HasPrecision(18, 2);
            entity.Property(item => item.SuperBuiltUpAreaSqFt).HasPrecision(12, 2);
            entity.Property(item => item.MonthlyRent).HasPrecision(18, 2);
            entity.Property(item => item.MonthlyMaintenance).HasPrecision(18, 2);
            entity.Property(item => item.DownPaymentPercent).HasPrecision(9, 4);
            entity.Property(item => item.AnnualInterestRatePercent).HasPrecision(9, 4);
            entity.Property(item => item.LoanTenureYears).HasPrecision(9, 4);
            entity.Property(item => item.AcquisitionCostPercent).HasPrecision(9, 4);
            entity.Property(item => item.VacancyPercent).HasPrecision(9, 4);
            entity.Property(item => item.AnnualPropertyTax).HasPrecision(18, 2);
            entity.Property(item => item.AnnualRepairs).HasPrecision(18, 2);
            entity.HasIndex(item => new { item.ComparisonAnalysisId, item.SortOrder });
            entity.HasMany(item => item.Scenarios)
                .WithOne(item => item.Property)
                .HasForeignKey(item => item.ComparedPropertyId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<PropertyScenarioAssumption>(entity =>
        {
            entity.ToTable("PropertyScenarioAssumptions");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.ScenarioName).HasMaxLength(32).IsRequired();
            entity.Property(item => item.PropertyAppreciationPercent).HasPrecision(9, 4);
            entity.Property(item => item.RentGrowthPercent).HasPrecision(9, 4);
            entity.Property(item => item.VacancyPercent).HasPrecision(9, 4);
            entity.Property(item => item.OperatingExpenseGrowthPercent).HasPrecision(9, 4);
            entity.Property(item => item.SellingCostPercent).HasPrecision(9, 4);
            entity.HasIndex(item => new { item.ComparedPropertyId, item.ScenarioName }).IsUnique();
            entity.ToTable(table =>
                table.HasCheckConstraint(
                    "CK_PropertyScenarioAssumptions_ScenarioName",
                    "[ScenarioName] IN ('conservative', 'base', 'optimistic')"));
        });

        modelBuilder.Entity<MarketObservation>(entity =>
        {
            entity.HasKey(item => item.Id);
            entity.Property(item => item.Locality).HasMaxLength(80).IsRequired();
            entity.Property(item => item.BuilderProject).HasMaxLength(120).IsRequired();
            entity.Property(item => item.PropertyType).HasMaxLength(40).IsRequired();
            entity.Property(item => item.Configuration).HasMaxLength(40).IsRequired();
            entity.Property(item => item.RecordType).HasMaxLength(32).IsRequired();
            entity.Property(item => item.VerificationStatus).HasMaxLength(32).IsRequired();
            entity.Property(item => item.Amount).HasPrecision(18, 2);
            entity.Property(item => item.AreaValue).HasPrecision(12, 2);
            entity.Property(item => item.AreaType).HasMaxLength(32).IsRequired();
            entity.Property(item => item.AreaUnit).HasMaxLength(16).IsRequired();
            entity.Property(item => item.PricePerSqFt).HasPrecision(18, 2);
            entity.Property(item => item.MonthlyRentPerSqFt).HasPrecision(18, 4);
            entity.Property(item => item.SourceName).HasMaxLength(160).IsRequired();
            entity.Property(item => item.SourceUrl).HasMaxLength(500);
            entity.Property(item => item.SourceRecordId).HasMaxLength(120);
            entity.Property(item => item.QualityNotes).HasMaxLength(1000);
            entity.Property(item => item.LicenceOrUsageNotes).HasMaxLength(500);
            entity.Property(item => item.DuplicateKey).HasMaxLength(64).IsRequired();
            entity.Property(item => item.ProviderKey).HasMaxLength(64);
            entity.HasIndex(item => item.DuplicateKey).IsUnique();
            entity.HasIndex(item => item.ProviderKey);
            entity.HasIndex(item => item.Locality);
            entity.HasIndex(item => new { item.Locality, item.RecordType, item.Configuration });
            entity.HasIndex(item => item.ObservedOn);
            entity.ToTable("MarketObservations", table =>
            {
                table.HasCheckConstraint(
                    "CK_MarketObservations_RecordType",
                    "[RecordType] IN ('asking_price', 'advertised_rent', 'verified_transaction', 'other')");
                table.HasCheckConstraint(
                    "CK_MarketObservations_VerificationStatus",
                    "[VerificationStatus] IN ('unverified', 'source_cited', 'reviewed')");
                table.HasCheckConstraint(
                    "CK_MarketObservations_AreaType",
                    "[AreaType] IN ('super_built_up', 'built_up', 'carpet', 'plot', 'unspecified')");
                table.HasCheckConstraint(
                    "CK_MarketObservations_AreaUnit",
                    "[AreaUnit] IN ('sq_ft', 'sq_m')");
            });
        });

        modelBuilder.Entity<MarketImportRun>(entity =>
        {
            entity.ToTable("MarketImportRuns");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.ProviderKey).HasMaxLength(64).IsRequired();
            entity.Property(item => item.Status).HasMaxLength(40).IsRequired();
            entity.Property(item => item.Summary).HasMaxLength(1000).IsRequired();
            entity.HasIndex(item => item.CompletedUtc);
            entity.HasIndex(item => new { item.ProviderKey, item.CompletedUtc });
        });
    }
}
