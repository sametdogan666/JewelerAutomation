using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace JewelerAutomation.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ProductTemplatesAndTransactionItemTemplate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ProductTemplateId",
                table: "TransactionItems",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ProductTemplates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    AyarMilyem = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: false),
                    DefaultLaborPrice = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    Category = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductTemplates", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TransactionItems_ProductTemplateId",
                table: "TransactionItems",
                column: "ProductTemplateId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductTemplates_Name",
                table: "ProductTemplates",
                column: "Name");

            migrationBuilder.AddForeignKey(
                name: "FK_TransactionItems_ProductTemplates_ProductTemplateId",
                table: "TransactionItems",
                column: "ProductTemplateId",
                principalTable: "ProductTemplates",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_TransactionItems_ProductTemplates_ProductTemplateId",
                table: "TransactionItems");

            migrationBuilder.DropTable(
                name: "ProductTemplates");

            migrationBuilder.DropIndex(
                name: "IX_TransactionItems_ProductTemplateId",
                table: "TransactionItems");

            migrationBuilder.DropColumn(
                name: "ProductTemplateId",
                table: "TransactionItems");
        }
    }
}
