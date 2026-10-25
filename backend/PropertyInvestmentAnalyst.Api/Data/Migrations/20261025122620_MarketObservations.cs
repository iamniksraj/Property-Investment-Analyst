using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PropertyInvestmentAnalyst.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class MarketObservations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MarketObservations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Locality = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    BuilderProject = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    PropertyType = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Configuration = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    RecordType = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    VerificationStatus = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    AreaValue = table.Column<decimal>(type: "decimal(12,2)", precision: 12, scale: 2, nullable: true),
                    AreaType = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    AreaUnit = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    PricePerSqFt = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    MonthlyRentPerSqFt = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: true),
                    SourceName = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                    SourceUrl = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    SourceRecordId = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    ObservedOn = table.Column<DateOnly>(type: "date", nullable: false),
                    ImportedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    QualityNotes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    LicenceOrUsageNotes = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    IsSyntheticTestData = table.Column<bool>(type: "bit", nullable: false),
                    IsOutlier = table.Column<bool>(type: "bit", nullable: false),
                    DuplicateKey = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MarketObservations", x => x.Id);
                    table.CheckConstraint("CK_MarketObservations_AreaType", "[AreaType] IN ('super_built_up', 'built_up', 'carpet', 'plot', 'unspecified')");
                    table.CheckConstraint("CK_MarketObservations_AreaUnit", "[AreaUnit] IN ('sq_ft', 'sq_m')");
                    table.CheckConstraint("CK_MarketObservations_RecordType", "[RecordType] IN ('asking_price', 'advertised_rent', 'verified_transaction', 'other')");
                    table.CheckConstraint("CK_MarketObservations_VerificationStatus", "[VerificationStatus] IN ('unverified', 'source_cited', 'reviewed')");
                });

            migrationBuilder.CreateIndex(
                name: "IX_MarketObservations_DuplicateKey",
                table: "MarketObservations",
                column: "DuplicateKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MarketObservations_Locality",
                table: "MarketObservations",
                column: "Locality");

            migrationBuilder.CreateIndex(
                name: "IX_MarketObservations_Locality_RecordType_Configuration",
                table: "MarketObservations",
                columns: new[] { "Locality", "RecordType", "Configuration" });

            migrationBuilder.CreateIndex(
                name: "IX_MarketObservations_ObservedOn",
                table: "MarketObservations",
                column: "ObservedOn");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MarketObservations");
        }
    }
}
