using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ValeriosPizza.Migrations
{
    /// <inheritdoc />
    public partial class QuitarPreciosInventario : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Precio",
                table: "Productos");

            migrationBuilder.DropColumn(
                name: "PrecioUnitario",
                table: "Ingredientes");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<double>(
                name: "Precio",
                table: "Productos",
                type: "REAL",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<double>(
                name: "PrecioUnitario",
                table: "Ingredientes",
                type: "REAL",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0.0);
        }
    }
}
