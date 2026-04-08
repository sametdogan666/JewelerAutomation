using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace JewelerAutomation.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class SahisEmanetOpeningBalance : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsSahisEmanet",
                table: "Transactions",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "KasaHareketli",
                table: "Transactions",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<int>(
                name: "SahisEmanetMode",
                table: "Transactions",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "CashCurrency",
                table: "CustomerTransactions",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "OpeningAssetKind",
                table: "CustomerTransactions",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "OpeningCustomerIsCreditor",
                table: "CustomerTransactions",
                type: "boolean",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "PostToLedger",
                table: "CustomerTransactions",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<Guid>(
                name: "SourceBasketTransactionId",
                table: "CustomerTransactions",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_CustomerTransactions_SourceBasketTransactionId",
                table: "CustomerTransactions",
                column: "SourceBasketTransactionId");

            migrationBuilder.AddForeignKey(
                name: "FK_CustomerTransactions_Transactions_SourceBasketTransactionId",
                table: "CustomerTransactions",
                column: "SourceBasketTransactionId",
                principalTable: "Transactions",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CustomerTransactions_Transactions_SourceBasketTransactionId",
                table: "CustomerTransactions");

            migrationBuilder.DropIndex(
                name: "IX_CustomerTransactions_SourceBasketTransactionId",
                table: "CustomerTransactions");

            migrationBuilder.DropColumn(
                name: "IsSahisEmanet",
                table: "Transactions");

            migrationBuilder.DropColumn(
                name: "KasaHareketli",
                table: "Transactions");

            migrationBuilder.DropColumn(
                name: "SahisEmanetMode",
                table: "Transactions");

            migrationBuilder.DropColumn(
                name: "CashCurrency",
                table: "CustomerTransactions");

            migrationBuilder.DropColumn(
                name: "OpeningAssetKind",
                table: "CustomerTransactions");

            migrationBuilder.DropColumn(
                name: "OpeningCustomerIsCreditor",
                table: "CustomerTransactions");

            migrationBuilder.DropColumn(
                name: "PostToLedger",
                table: "CustomerTransactions");

            migrationBuilder.DropColumn(
                name: "SourceBasketTransactionId",
                table: "CustomerTransactions");
        }
    }
}
