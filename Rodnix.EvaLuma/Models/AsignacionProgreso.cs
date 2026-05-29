using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Rodnix.EvaLuma.Models
{
    [Table("Asignacion_Progreso")]
    public class AsignacionProgreso
    {
        [Key]
        [Column("id_asignacion")]
        public int IdAsignacion { get; set; }

        [Column("id_empleado")]
        public int IdEmpleado { get; set; }

        [Column("id_simulacion")]
        public int IdSimulacion { get; set; }

        [Column("estado", TypeName = "varchar(20)")]
        public string Estado { get; set; } = "Pendiente";

        [Column("ultimo_checkpoint", TypeName = "varchar(255)")]
        public string? UltimoCheckpoint { get; set; }

        [Column("calificacion_temporal", TypeName = "decimal(5,2)")]
        public decimal CalificacionTemporal { get; set; } = 0.00m;

        [Column("fecha_inicio")]
        public DateTime? FechaInicio { get; set; }

        [Column("fecha_ultima_accion")]
        public DateTime? FechaUltimaAccion { get; set; }

        [ForeignKey("IdEmpleado")]
        public virtual Usuario? Empleado { get; set; }

        [ForeignKey("IdSimulacion")]
        public virtual Simulacion? Simulacion { get; set; }
    }
}