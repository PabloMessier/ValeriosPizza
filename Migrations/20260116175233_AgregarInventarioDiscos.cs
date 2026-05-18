using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ValeriosPizza.Migrations
{
    /// <inheritdoc />
    public partial class AgregarInventarioDiscos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "InventarioDiscos",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Fecha = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CantidadInicial = table.Column<int>(type: "INTEGER", nullable: false),
                    DiscosPreparados = table.Column<int>(type: "INTEGER", nullable: false),
                    DiscosUtilizados = table.Column<int>(type: "INTEGER", nullable: false),
                    DiscosMerma = table.Column<int>(type: "INTEGER", nullable: false),
                    DiscosCortesia = table.Column<int>(type: "INTEGER", nullable: false),
                    Notas = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InventarioDiscos", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "InventarioDiscos");
        }
    }
}
