using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace JewelerAutomation.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddGbpForexTransactionKind : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "ForexAmountBase",
                table: "Transactions",
                type: "numeric(18,6)",
                precision: 18,
                scale: 6,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ForexBaseCurrency",
                table: "Transactions",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "ForexCounterTry",
                table: "Transactions",
                type: "numeric(18,6)",
                precision: 18,
                scale: 6,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "ForexIsBuy",
                table: "Transactions",
                type: "boolean",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "ForexRateTryPerUnit",
                table: "Transactions",
                type: "numeric(18,6)",
                precision: 18,
                scale: 6,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Kind",
                table: "Transactions",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<decimal>(
                name: "NetCashAmountGbp",
                table: "Transactions",
                type: "numeric(18,6)",
                precision: 18,
                scale: 6,
                nullable: false,
                defaultValue: 0m);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ForexAmountBase",
                table: "Transactions");

            migrationBuilder.DropColumn(
                name: "ForexBaseCurrency",
                table: "Transactions");

            migrationBuilder.DropColumn(
                name: "ForexCounterTry",
                table: "Transactions");

            migrationBuilder.DropColumn(
                name: "ForexIsBuy",
                table: "Transactions");

            migrationBuilder.DropColumn(
                name: "ForexRateTryPerUnit",
                table: "Transactions");

            migrationBuilder.DropColumn(
                name: "Kind",
                table: "Transactions");

            migrationBuilder.DropColumn(
                name: "NetCashAmountGbp",
                table: "Transactions");
        }
    }
}
