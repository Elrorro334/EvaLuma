using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Rodnix.EvaLuma.Migrations
{
    /// <inheritdoc />
    public partial class AddRamificationAndAES : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "IdSiguientePregunta",
                table: "OpcionesRespuesta",
                type: "int",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IdSiguientePregunta",
                table: "OpcionesRespuesta");
        }
    }
}
