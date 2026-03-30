using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace JewelerAutomation.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddTransactionItemsBasket : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "NetCashAmount",
                table: "Transactions",
                type: "numeric(18,6)",
                precision: 18,
                scale: 6,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "NetHasGram",
                table: "Transactions",
                type: "numeric(18,6)",
                precision: 18,
                scale: 6,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.CreateTable(
                name: "TransactionItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TransactionId = table.Column<Guid>(type: "uuid", nullable: false),
                    Direction = table.Column<int>(type: "integer", nullable: false),
                    Quantity = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: false),
                    Milyem = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: false),
                    PieceCount = table.Column<int>(type: "integer", nullable: true),
                    UnitLabour = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: true),
                    TotalLabour = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: false),
                    HasGram = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: false),
                    Price = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: true),
                    Description = table.Column<string>(type: "text", nullable: true),
                    MilyemLabour = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TransactionItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TransactionItems_Transactions_TransactionId",
                        column: x => x.TransactionId,
                        principalTable: "Transactions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TransactionItems_TransactionId",
                table: "TransactionItems",
                column: "TransactionId");

            // Migrate existing single-item transactions into TransactionItems
            migrationBuilder.Sql(@"
                INSERT INTO ""TransactionItems""
                    (""Id"", ""TransactionId"", ""Direction"", ""Quantity"", ""Milyem"",
                     ""PieceCount"", ""UnitLabour"", ""TotalLabour"", ""HasGram"",
                     ""Price"", ""Description"", ""MilyemLabour"",
                     ""CreatedAt"", ""IsDeleted"", ""DeletedAt"")
                SELECT
                    gen_random_uuid(),
                    ""Id"",
                    ""Direction"",
                    ""Quantity"",
                    ""Milyem"",
                    ""PieceCount"",
                    ""UnitLabour"",
                    ""TotalLabour"",
                    ""HasGram"",
                    ""Price"",
                    ""Description"",
                    ""MilyemLabour"",
                    ""CreatedAt"",
                    ""IsDeleted"",
                    ""DeletedAt""
                FROM ""Transactions""
                WHERE ""Quantity"" > 0;
            ");

            // Set NetHasGram and NetCashAmount for existing transactions
            migrationBuilder.Sql(@"
                UPDATE ""Transactions""
                SET ""NetHasGram"" = CASE
                        WHEN ""Direction"" = 1 THEN ""HasGram""
                        ELSE -""HasGram""
                    END,
                    ""NetCashAmount"" = CASE
                        WHEN ""Direction"" = 0 THEN COALESCE(""Price"", 0)
                        ELSE -COALESCE(""Price"", 0)
                    END;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TransactionItems");

            migrationBuilder.DropColumn(
                name: "NetCashAmount",
                table: "Transactions");

            migrationBuilder.DropColumn(
                name: "NetHasGram",
                table: "Transactions");
        }
    }
}
