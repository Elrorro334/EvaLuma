using System.ComponentModel.DataAnnotations;

namespace Rodnix.EvaLuma.DTOs
{
    public class CrearCampanaDto
    {
        [Required(ErrorMessage = "El nombre es obligatorio")]
        public string NombreCampana { get; set; } = null!;
        public string? Descripcion { get; set; }
        [Required]
        public DateTime FechaInicio { get; set; }
        [Required]
        public DateTime FechaLimite { get; set; }
        public bool Estricta { get; set; } = true;
    }

    public class EditarCampanaDto
    {
        public string? NombreCampana { get; set; }
        public string? Descripcion { get; set; }
        public DateTime? FechaLimite { get; set; }
    }

    public class CrearSimulacionDto
    {
        [Required(ErrorMessage = "El título es obligatorio")]
        public string Titulo { get; set; } = null!;
        [Required]
        public int TotalPreguntas { get; set; }
        public int? TiempoEstimadoMinutos { get; set; }
    }
}