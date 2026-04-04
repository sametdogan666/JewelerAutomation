using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace JewelerAutomation.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class CascadeDeleteGoldTransactionDependents : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CashPeggingFifoDetails_GoldTransactions_GoldTransactionId",
                table: "CashPeggingFifoDetails");

            migrationBuilder.DropForeignKey(
                name: "FK_LinkingDetails_GoldTransactions_GoldTransactionId",
                table: "LinkingDetails");

            migrationBuilder.AddForeignKey(
                name: "FK_CashPeggingFifoDetails_GoldTransactions_GoldTransactionId",
                table: "CashPeggingFifoDetails",
                column: "GoldTransactionId",
                principalTable: "GoldTransactions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_LinkingDetails_GoldTransactions_GoldTransactionId",
                table: "LinkingDetails",
                column: "GoldTransactionId",
                principalTable: "GoldTransactions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CashPeggingFifoDetails_GoldTransactions_GoldTransactionId",
                table: "CashPeggingFifoDetails");

            migrationBuilder.DropForeignKey(
                name: "FK_LinkingDetails_GoldTransactions_GoldTransactionId",
                table: "LinkingDetails");

            migrationBuilder.AddForeignKey(
                name: "FK_CashPeggingFifoDetails_GoldTransactions_GoldTransactionId",
                table: "CashPeggingFifoDetails",
                column: "GoldTransactionId",
                principalTable: "GoldTransactions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_LinkingDetails_GoldTransactions_GoldTransactionId",
                table: "LinkingDetails",
                column: "GoldTransactionId",
                principalTable: "GoldTransactions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
