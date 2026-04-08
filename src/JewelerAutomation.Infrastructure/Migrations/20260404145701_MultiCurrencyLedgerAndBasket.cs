using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace JewelerAutomation.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class MultiCurrencyLedgerAndBasket : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "NetCashAmountEur",
                table: "Transactions",
                type: "numeric(18,6)",
                precision: 18,
                scale: 6,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "NetCashAmountUsd",
                table: "Transactions",
                type: "numeric(18,6)",
                precision: 18,
                scale: 6,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<int>(
                name: "PaymentCurrency",
                table: "TransactionItems",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "CashCurrency",
                table: "LedgerEntries",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "NetCashAmountEur",
                table: "Transactions");

            migrationBuilder.DropColumn(
                name: "NetCashAmountUsd",
                table: "Transactions");

            migrationBuilder.DropColumn(
                name: "PaymentCurrency",
                table: "TransactionItems");

            migrationBuilder.DropColumn(
                name: "CashCurrency",
                table: "LedgerEntries");
        }
    }
}
