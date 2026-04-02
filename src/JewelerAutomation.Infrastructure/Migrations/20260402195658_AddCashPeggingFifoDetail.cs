using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace JewelerAutomation.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCashPeggingFifoDetail : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CashPeggingFifoDetails",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CashPeggingLogId = table.Column<Guid>(type: "uuid", nullable: false),
                    GoldTransactionId = table.Column<Guid>(type: "uuid", nullable: false),
                    AmountDeducted = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CashPeggingFifoDetails", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CashPeggingFifoDetails_CashPeggingLogs_CashPeggingLogId",
                        column: x => x.CashPeggingLogId,
                        principalTable: "CashPeggingLogs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CashPeggingFifoDetails_GoldTransactions_GoldTransactionId",
                        column: x => x.GoldTransactionId,
                        principalTable: "GoldTransactions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CashPeggingFifoDetails_CashPeggingLogId",
                table: "CashPeggingFifoDetails",
                column: "CashPeggingLogId");

            migrationBuilder.CreateIndex(
                name: "IX_CashPeggingFifoDetails_GoldTransactionId",
                table: "CashPeggingFifoDetails",
                column: "GoldTransactionId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CashPeggingFifoDetails");
        }
    }
}
