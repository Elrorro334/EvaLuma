using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Rodnix.EvaLuma.Models
{
    [Table("Campanas")]
    public class Campana
    {
        [Key]
        [Column("id_campana")]
        public int IdCampana { get; set; }

        [Column("id_auditor")]
        public int IdAuditor { get; set; }

        [Column("nombre_campana", TypeName = "varchar(200)")]
        public string NombreCampana { get; set; } = string.Empty;

        [Column("descripcion", TypeName = "text")]
        public string Descripcion { get; set; } = string.Empty;

        [Column("fecha_inicio")]
        public DateTime FechaInicio { get; set; }

        [Column("fecha_limite")]
        public DateTime FechaLimite { get; set; }

        [Column("estricta")]
        public bool Estricta { get; set; } = true;

        [ForeignKey("IdAuditor")]
        public virtual Usuario? Auditor { get; set; }
    }
}