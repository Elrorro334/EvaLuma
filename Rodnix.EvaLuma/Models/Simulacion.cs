using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Rodnix.EvaLuma.Models
{
    [Table("Simulaciones")]
    public class Simulacion
    {
        [Key]
        [Column("id_simulacion")]
        public int IdSimulacion { get; set; }

        [Column("id_campana")]
        public int IdCampana { get; set; }

        [Column("titulo", TypeName = "varchar(200)")]
        public string Titulo { get; set; } = string.Empty;

        [Column("total_preguntas")]
        public int TotalPreguntas { get; set; }

        [Column("tiempo_estimado_minutos")]
        public int? TiempoEstimadoMinutos { get; set; }

        [ForeignKey("IdCampana")]
        public virtual Campana? Campana { get; set; }
    }
}