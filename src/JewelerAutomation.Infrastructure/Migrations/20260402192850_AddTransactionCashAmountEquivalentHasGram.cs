using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace JewelerAutomation.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddTransactionCashAmountEquivalentHasGram : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "CashAmount",
                table: "Transactions",
                type: "numeric(18,6)",
                precision: 18,
                scale: 6,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "EquivalentHasGram",
                table: "Transactions",
                type: "numeric(18,6)",
                precision: 18,
                scale: 6,
                nullable: true);

            // Nakit bağlama başlık satırları (kalem yok): mevcut Price / HasGram veya net alanlardan doldur
            migrationBuilder.Sql(
                """
                UPDATE "Transactions" t
                SET "CashAmount" = ABS(COALESCE(t."Price", t."NetCashAmount")),
                    "EquivalentHasGram" = ABS(COALESCE(t."HasGram", t."NetHasGram"))
                WHERE t."CorrelationId" IS NOT NULL
                  AND t."IsDeleted" = false
                  AND NOT EXISTS (
                    SELECT 1 FROM "TransactionItems" i
                    WHERE i."TransactionId" = t."Id" AND i."IsDeleted" = false);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CashAmount",
                table: "Transactions");

            migrationBuilder.DropColumn(
                name: "EquivalentHasGram",
                table: "Transactions");
        }
    }
}
