using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PropertyInvestmentAnalyst.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class MarketImportFoundation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ProviderKey",
                table: "MarketObservations",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "MarketImportRuns",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProviderKey = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    StartedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CompletedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ObservedFrom = table.Column<DateOnly>(type: "date", nullable: true),
                    ObservedTo = table.Column<DateOnly>(type: "date", nullable: true),
                    Inserted = table.Column<int>(type: "int", nullable: false),
                    Updated = table.Column<int>(type: "int", nullable: false),
                    Skipped = table.Column<int>(type: "int", nullable: false),
                    Duplicates = table.Column<int>(type: "int", nullable: false),
                    Invalid = table.Column<int>(type: "int", nullable: false),
                    ErrorCount = table.Column<int>(type: "int", nullable: false),
                    Summary = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MarketImportRuns", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MarketObservations_ProviderKey",
                table: "MarketObservations",
                column: "ProviderKey");

            migrationBuilder.CreateIndex(
                name: "IX_MarketImportRuns_CompletedUtc",
                table: "MarketImportRuns",
                column: "CompletedUtc");

            migrationBuilder.CreateIndex(
                name: "IX_MarketImportRuns_ProviderKey_CompletedUtc",
                table: "MarketImportRuns",
                columns: new[] { "ProviderKey", "CompletedUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MarketImportRuns");

            migrationBuilder.DropIndex(
                name: "IX_MarketObservations_ProviderKey",
                table: "MarketObservations");

            migrationBuilder.DropColumn(
                name: "ProviderKey",
                table: "MarketObservations");
        }
    }
}
