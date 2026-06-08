using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Rodnix.EvaLuma.Migrations
{
    /// <inheritdoc />
    public partial class AgregarPreguntasYOpciones : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Preguntas",
                columns: table => new
                {
                    IdPregunta = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    IdSimulacion = table.Column<int>(type: "int", nullable: false),
                    TextoPregunta = table.Column<string>(type: "varchar(1000)", maxLength: 1000, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ValorPuntos = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Preguntas", x => x.IdPregunta);
                    table.ForeignKey(
                        name: "FK_Preguntas_Simulaciones_IdSimulacion",
                        column: x => x.IdSimulacion,
                        principalTable: "Simulaciones",
                        principalColumn: "id_simulacion",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "OpcionesRespuesta",
                columns: table => new
                {
                    IdOpcion = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    IdPregunta = table.Column<int>(type: "int", nullable: false),
                    TextoOpcion = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    EsCorrecta = table.Column<bool>(type: "tinyint(1)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OpcionesRespuesta", x => x.IdOpcion);
                    table.ForeignKey(
                        name: "FK_OpcionesRespuesta_Preguntas_IdPregunta",
                        column: x => x.IdPregunta,
                        principalTable: "Preguntas",
                        principalColumn: "IdPregunta",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_OpcionesRespuesta_IdPregunta",
                table: "OpcionesRespuesta",
                column: "IdPregunta");

            migrationBuilder.CreateIndex(
                name: "IX_Preguntas_IdSimulacion",
                table: "Preguntas",
                column: "IdSimulacion");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "OpcionesRespuesta");

            migrationBuilder.DropTable(
                name: "Preguntas");
        }
    }
}
