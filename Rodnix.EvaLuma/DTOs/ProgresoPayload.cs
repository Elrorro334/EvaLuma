
using System.ComponentModel.DataAnnotations;

namespace Rodnix.EvaLuma.DTOs
{
    public class ProgresoPayload
    {
        [Required(ErrorMessage = "IdAsignacion es requerido")]
        public int IdAsignacion { get; set; }

        [Required(ErrorMessage = "Accion es requerida")]
        [StringLength(255, MinimumLength = 1, ErrorMessage = "La acción debe tener entre 1 y 255 caracteres")]
        public string Accion { get; set; } = null!;

        [Required(ErrorMessage = "Checkpoint es requerido")]
        [StringLength(255, MinimumLength = 1, ErrorMessage = "El checkpoint debe tener entre 1 y 255 caracteres")]
        public string Checkpoint { get; set; } = null!;

        [Required(ErrorMessage = "TiempoMs es requerido")]
        [Range(0, int.MaxValue, ErrorMessage = "TiempoMs debe ser un valor no negativo")]
        public int TiempoMs { get; set; }
    }
}