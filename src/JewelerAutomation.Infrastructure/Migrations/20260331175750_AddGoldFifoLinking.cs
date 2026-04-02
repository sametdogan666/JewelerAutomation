using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace JewelerAutomation.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddGoldFifoLinking : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "GoldTransactions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TransactionId = table.Column<Guid>(type: "uuid", nullable: false),
                    TransactionItemId = table.Column<Guid>(type: "uuid", nullable: true),
                    OriginalHasGram = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    RemainingGram = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    IsFullyLinked = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GoldTransactions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GoldTransactions_TransactionItems_TransactionItemId",
                        column: x => x.TransactionItemId,
                        principalTable: "TransactionItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_GoldTransactions_Transactions_TransactionId",
                        column: x => x.TransactionId,
                        principalTable: "Transactions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "LinkingProcesses",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    LinkingDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    TargetAmount = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    TargetPrice = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    TotalProfit = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    SafeMovementId = table.Column<Guid>(type: "uuid", nullable: true),
                    Notes = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LinkingProcesses", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LinkingProcesses_SafeMovements_SafeMovementId",
                        column: x => x.SafeMovementId,
                        principalTable: "SafeMovements",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "LinkingDetails",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    LinkingProcessId = table.Column<Guid>(type: "uuid", nullable: false),
                    GoldTransactionId = table.Column<Guid>(type: "uuid", nullable: false),
                    AmountDeducted = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LinkingDetails", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LinkingDetails_GoldTransactions_GoldTransactionId",
                        column: x => x.GoldTransactionId,
                        principalTable: "GoldTransactions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LinkingDetails_LinkingProcesses_LinkingProcessId",
                        column: x => x.LinkingProcessId,
                        principalTable: "LinkingProcesses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_GoldTransactions_IsFullyLinked_RemainingGram",
                table: "GoldTransactions",
                columns: new[] { "IsFullyLinked", "RemainingGram" });

            migrationBuilder.CreateIndex(
                name: "IX_GoldTransactions_TransactionId",
                table: "GoldTransactions",
                column: "TransactionId");

            migrationBuilder.CreateIndex(
                name: "IX_GoldTransactions_TransactionItemId",
                table: "GoldTransactions",
                column: "TransactionItemId");

            migrationBuilder.CreateIndex(
                name: "IX_LinkingDetails_GoldTransactionId",
                table: "LinkingDetails",
                column: "GoldTransactionId");

            migrationBuilder.CreateIndex(
                name: "IX_LinkingDetails_LinkingProcessId",
                table: "LinkingDetails",
                column: "LinkingProcessId");

            migrationBuilder.CreateIndex(
                name: "IX_LinkingProcesses_LinkingDate",
                table: "LinkingProcesses",
                column: "LinkingDate");

            migrationBuilder.CreateIndex(
                name: "IX_LinkingProcesses_SafeMovementId",
                table: "LinkingProcesses",
                column: "SafeMovementId");

            // Mevcut satış kalemlerinden FIFO GoldTransaction satırları (Direction: Sale = 0)
            migrationBuilder.Sql(@"
INSERT INTO ""GoldTransactions"" (""Id"", ""TransactionId"", ""TransactionItemId"", ""OriginalHasGram"", ""RemainingGram"", ""IsFullyLinked"", ""CreatedAt"", ""UpdatedAt"", ""IsDeleted"", ""DeletedAt"")
SELECT gen_random_uuid(), ti.""TransactionId"", ti.""Id"", ti.""HasGram"", ti.""HasGram"", false, NOW() AT TIME ZONE 'utc', NULL, false, NULL
FROM ""TransactionItems"" ti
INNER JOIN ""Transactions"" t ON t.""Id"" = ti.""TransactionId""
WHERE ti.""Direction"" = 0 AND t.""IsDeleted"" = false AND ti.""HasGram"" > 0;

INSERT INTO ""GoldTransactions"" (""Id"", ""TransactionId"", ""TransactionItemId"", ""OriginalHasGram"", ""RemainingGram"", ""IsFullyLinked"", ""CreatedAt"", ""UpdatedAt"", ""IsDeleted"", ""DeletedAt"")
SELECT gen_random_uuid(), t.""Id"", NULL, t.""HasGram"", t.""HasGram"", false, NOW() AT TIME ZONE 'utc', NULL, false, NULL
FROM ""Transactions"" t
WHERE t.""IsDeleted"" = false AND t.""HasGram"" > 0
AND NOT EXISTS (SELECT 1 FROM ""TransactionItems"" ti WHERE ti.""TransactionId"" = t.""Id"")
AND t.""Direction"" = 0;
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LinkingDetails");

            migrationBuilder.DropTable(
                name: "GoldTransactions");

            migrationBuilder.DropTable(
                name: "LinkingProcesses");
        }
    }
}
