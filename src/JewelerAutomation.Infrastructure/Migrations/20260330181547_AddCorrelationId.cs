using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace JewelerAutomation.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCorrelationId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "CorrelationId",
                table: "Transactions",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CorrelationId",
                table: "SafeMovements",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CorrelationId",
                table: "LedgerEntries",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CorrelationId",
                table: "CashPeggingLogs",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.CreateIndex(
                name: "IX_Transactions_CorrelationId",
                table: "Transactions",
                column: "CorrelationId");

            migrationBuilder.CreateIndex(
                name: "IX_SafeMovements_CorrelationId",
                table: "SafeMovements",
                column: "CorrelationId");

            migrationBuilder.CreateIndex(
                name: "IX_LedgerEntries_CorrelationId",
                table: "LedgerEntries",
                column: "CorrelationId");

            migrationBuilder.CreateIndex(
                name: "IX_CashPeggingLogs_CorrelationId",
                table: "CashPeggingLogs",
                column: "CorrelationId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Transactions_CorrelationId",
                table: "Transactions");

            migrationBuilder.DropIndex(
                name: "IX_SafeMovements_CorrelationId",
                table: "SafeMovements");

            migrationBuilder.DropIndex(
                name: "IX_LedgerEntries_CorrelationId",
                table: "LedgerEntries");

            migrationBuilder.DropIndex(
                name: "IX_CashPeggingLogs_CorrelationId",
                table: "CashPeggingLogs");

            migrationBuilder.DropColumn(
                name: "CorrelationId",
                table: "Transactions");

            migrationBuilder.DropColumn(
                name: "CorrelationId",
                table: "SafeMovements");

            migrationBuilder.DropColumn(
                name: "CorrelationId",
                table: "LedgerEntries");

            migrationBuilder.DropColumn(
                name: "CorrelationId",
                table: "CashPeggingLogs");
        }
    }
}
