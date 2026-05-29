using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Rodnix.EvaLuma.Models
{
    [Table("Bitacora_Auditoria")]
    public class BitacoraAuditoria
    {
        [Key]
        [Column("id_evento")]
        public long IdEvento { get; set; }

        [Column("id_asignacion")]
        public int IdAsignacion { get; set; }

        [Column("accion_realizada", TypeName = "varchar(255)")]
        public string AccionRealizada { get; set; } = string.Empty;

        [Column("tiempo_respuesta_ms")]
        public int TiempoRespuestaMs { get; set; }

        [Column("marca_tiempo")]
        public DateTime MarcaTiempo { get; set; } = DateTime.UtcNow;

        [Column("hash_previo", TypeName = "varchar(64)")]
        public string HashPrevio { get; set; } = string.Empty;

        [Column("hash_criptografico", TypeName = "varchar(64)")]
        public string HashCriptografico { get; set; } = string.Empty;

        [ForeignKey("IdAsignacion")]
        public virtual AsignacionProgreso? Asignacion { get; set; }
    }
}