using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace JewelerAutomation.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ProductTemplateDualMilyem : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "AyarMilyem",
                table: "ProductTemplates",
                newName: "MilyemSatis");

            migrationBuilder.AddColumn<decimal>(
                name: "MilyemAlis",
                table: "ProductTemplates",
                type: "numeric(18,6)",
                precision: 18,
                scale: 6,
                nullable: false,
                defaultValue: 0m);

            // Eski tek milyem değerini hem alış hem satışa kopyala (başlangıçta aynı kalsın).
            migrationBuilder.Sql("UPDATE \"ProductTemplates\" SET \"MilyemAlis\" = \"MilyemSatis\";");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "MilyemAlis",
                table: "ProductTemplates");

            migrationBuilder.RenameColumn(
                name: "MilyemSatis",
                table: "ProductTemplates",
                newName: "AyarMilyem");
        }
    }
}
