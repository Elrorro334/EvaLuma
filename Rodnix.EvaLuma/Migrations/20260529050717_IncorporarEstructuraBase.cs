using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Rodnix.EvaLuma.Migrations
{
    /// <inheritdoc />
    public partial class IncorporarEstructuraBase : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Campanas",
                columns: table => new
                {
                    id_campana = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    id_auditor = table.Column<int>(type: "int", nullable: false),
                    nombre_campana = table.Column<string>(type: "varchar(200)", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    descripcion = table.Column<string>(type: "text", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    fecha_inicio = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    fecha_limite = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    estricta = table.Column<bool>(type: "tinyint(1)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Campanas", x => x.id_campana);
                    table.ForeignKey(
                        name: "FK_Campanas_Usuarios_id_auditor",
                        column: x => x.id_auditor,
                        principalTable: "Usuarios",
                        principalColumn: "id_usuario",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "Simulaciones",
                columns: table => new
                {
                    id_simulacion = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    id_campana = table.Column<int>(type: "int", nullable: false),
                    titulo = table.Column<string>(type: "varchar(200)", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    total_preguntas = table.Column<int>(type: "int", nullable: false),
                    tiempo_estimado_minutos = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Simulaciones", x => x.id_simulacion);
                    table.ForeignKey(
                        name: "FK_Simulaciones_Campanas_id_campana",
                        column: x => x.id_campana,
                        principalTable: "Campanas",
                        principalColumn: "id_campana",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "Asignacion_Progreso",
                columns: table => new
                {
                    id_asignacion = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    id_empleado = table.Column<int>(type: "int", nullable: false),
                    id_simulacion = table.Column<int>(type: "int", nullable: false),
                    estado = table.Column<string>(type: "varchar(20)", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ultimo_checkpoint = table.Column<string>(type: "varchar(255)", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    calificacion_temporal = table.Column<decimal>(type: "decimal(5,2)", nullable: false),
                    fecha_inicio = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    fecha_ultima_accion = table.Column<DateTime>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Asignacion_Progreso", x => x.id_asignacion);
                    table.ForeignKey(
                        name: "FK_Asignacion_Progreso_Simulaciones_id_simulacion",
                        column: x => x.id_simulacion,
                        principalTable: "Simulaciones",
                        principalColumn: "id_simulacion",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Asignacion_Progreso_Usuarios_id_empleado",
                        column: x => x.id_empleado,
                        principalTable: "Usuarios",
                        principalColumn: "id_usuario",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "Bitacora_Auditoria",
                columns: table => new
                {
                    id_evento = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    id_asignacion = table.Column<int>(type: "int", nullable: false),
                    accion_realizada = table.Column<string>(type: "varchar(255)", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    tiempo_respuesta_ms = table.Column<int>(type: "int", nullable: false),
                    marca_tiempo = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    hash_previo = table.Column<string>(type: "varchar(64)", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    hash_criptografico = table.Column<string>(type: "varchar(64)", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Bitacora_Auditoria", x => x.id_evento);
                    table.ForeignKey(
                        name: "FK_Bitacora_Auditoria_Asignacion_Progreso_id_asignacion",
                        column: x => x.id_asignacion,
                        principalTable: "Asignacion_Progreso",
                        principalColumn: "id_asignacion",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_Asignacion_Progreso_id_empleado",
                table: "Asignacion_Progreso",
                column: "id_empleado");

            migrationBuilder.CreateIndex(
                name: "IX_Asignacion_Progreso_id_simulacion",
                table: "Asignacion_Progreso",
                column: "id_simulacion");

            migrationBuilder.CreateIndex(
                name: "IX_Bitacora_Auditoria_hash_criptografico",
                table: "Bitacora_Auditoria",
                column: "hash_criptografico",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Bitacora_Auditoria_id_asignacion",
                table: "Bitacora_Auditoria",
                column: "id_asignacion");

            migrationBuilder.CreateIndex(
                name: "IX_Campanas_id_auditor",
                table: "Campanas",
                column: "id_auditor");

            migrationBuilder.CreateIndex(
                name: "IX_Simulaciones_id_campana",
                table: "Simulaciones",
                column: "id_campana");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Bitacora_Auditoria");

            migrationBuilder.DropTable(
                name: "Asignacion_Progreso");

            migrationBuilder.DropTable(
                name: "Simulaciones");

            migrationBuilder.DropTable(
                name: "Campanas");
        }
    }
}
