using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Rodnix.EvaLuma.Models
{
    [Table("Usuarios")]
    public class Usuario
    {
        [Key]
        [Column("id_usuario")]
        public int IdUsuario { get; set; }

        [Column("sso_identificador")]
        public string SsoIdentificador { get; set; } = string.Empty;

        [Column("nombre_completo")]
        public string NombreCompleto { get; set; } = string.Empty;

        [Column("email_corporativo")]
        public string EmailCorporativo { get; set; } = string.Empty;

        [Column("rol")]
        public string Rol { get; set; } = string.Empty;

        [Column("departamento")]
        public string Departamento { get; set; } = string.Empty;

        [Column("fecha_registro")]
        public DateTime FechaRegistro { get; set; }

        [Column("estatus")]
        public bool Estatus { get; set; }
    }
}
