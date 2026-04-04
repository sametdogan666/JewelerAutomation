using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace JewelerAutomation.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class GoldRatesTableAndIsManual : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsManual",
                table: "ManualGoldRateDays",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.DropIndex(
                name: "IX_ManualGoldRateDays_EffectiveDate",
                table: "ManualGoldRateDays");

            migrationBuilder.RenameTable(
                name: "ManualGoldRateDays",
                newName: "GoldRates");

            migrationBuilder.CreateIndex(
                name: "IX_GoldRates_EffectiveDate_IsManual",
                table: "GoldRates",
                columns: new[] { "EffectiveDate", "IsManual" },
                unique: true);

            migrationBuilder.Sql(
                """ALTER TABLE "GoldRates" RENAME CONSTRAINT "PK_ManualGoldRateDays" TO "PK_GoldRates";""");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """ALTER TABLE "GoldRates" RENAME CONSTRAINT "PK_GoldRates" TO "PK_ManualGoldRateDays";""");

            migrationBuilder.DropIndex(
                name: "IX_GoldRates_EffectiveDate_IsManual",
                table: "GoldRates");

            migrationBuilder.RenameTable(
                name: "GoldRates",
                newName: "ManualGoldRateDays");

            migrationBuilder.CreateIndex(
                name: "IX_ManualGoldRateDays_EffectiveDate",
                table: "ManualGoldRateDays",
                column: "EffectiveDate",
                unique: true);

            migrationBuilder.DropColumn(
                name: "IsManual",
                table: "ManualGoldRateDays");
        }
    }
}
