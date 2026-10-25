using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PropertyInvestmentAnalyst.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ComparisonAnalyses",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    SchemaVersion = table.Column<int>(type: "int", nullable: false, defaultValue: 1)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ComparisonAnalyses", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ComparedProperties",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ComparisonAnalysisId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    Locality = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    BuilderProject = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    PropertyType = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    PurchasePrice = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    SuperBuiltUpAreaSqFt = table.Column<decimal>(type: "decimal(12,2)", precision: 12, scale: 2, nullable: true),
                    MonthlyRent = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    MonthlyMaintenance = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    DownPaymentPercent = table.Column<decimal>(type: "decimal(9,4)", precision: 9, scale: 4, nullable: false),
                    AnnualInterestRatePercent = table.Column<decimal>(type: "decimal(9,4)", precision: 9, scale: 4, nullable: false),
                    LoanTenureYears = table.Column<decimal>(type: "decimal(9,4)", precision: 9, scale: 4, nullable: false),
                    AcquisitionCostPercent = table.Column<decimal>(type: "decimal(9,4)", precision: 9, scale: 4, nullable: false),
                    HoldingPeriodYears = table.Column<int>(type: "int", nullable: false),
                    VacancyPercent = table.Column<decimal>(type: "decimal(9,4)", precision: 9, scale: 4, nullable: false),
                    AnnualPropertyTax = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    AnnualRepairs = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ComparedProperties", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ComparedProperties_ComparisonAnalyses_ComparisonAnalysisId",
                        column: x => x.ComparisonAnalysisId,
                        principalTable: "ComparisonAnalyses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PropertyScenarioAssumptions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ComparedPropertyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ScenarioName = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    PropertyAppreciationPercent = table.Column<decimal>(type: "decimal(9,4)", precision: 9, scale: 4, nullable: false),
                    RentGrowthPercent = table.Column<decimal>(type: "decimal(9,4)", precision: 9, scale: 4, nullable: false),
                    VacancyPercent = table.Column<decimal>(type: "decimal(9,4)", precision: 9, scale: 4, nullable: false),
                    OperatingExpenseGrowthPercent = table.Column<decimal>(type: "decimal(9,4)", precision: 9, scale: 4, nullable: false),
                    SellingCostPercent = table.Column<decimal>(type: "decimal(9,4)", precision: 9, scale: 4, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PropertyScenarioAssumptions", x => x.Id);
                    table.CheckConstraint("CK_PropertyScenarioAssumptions_ScenarioName", "[ScenarioName] IN ('conservative', 'base', 'optimistic')");
                    table.ForeignKey(
                        name: "FK_PropertyScenarioAssumptions_ComparedProperties_ComparedPropertyId",
                        column: x => x.ComparedPropertyId,
                        principalTable: "ComparedProperties",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ComparedProperties_ComparisonAnalysisId_SortOrder",
                table: "ComparedProperties",
                columns: new[] { "ComparisonAnalysisId", "SortOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_ComparisonAnalyses_UpdatedUtc",
                table: "ComparisonAnalyses",
                column: "UpdatedUtc");

            migrationBuilder.CreateIndex(
                name: "IX_PropertyScenarioAssumptions_ComparedPropertyId_ScenarioName",
                table: "PropertyScenarioAssumptions",
                columns: new[] { "ComparedPropertyId", "ScenarioName" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PropertyScenarioAssumptions");

            migrationBuilder.DropTable(
                name: "ComparedProperties");

            migrationBuilder.DropTable(
                name: "ComparisonAnalyses");
        }
    }
}
