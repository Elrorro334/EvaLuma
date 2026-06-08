using System.ComponentModel.DataAnnotations;

namespace Rodnix.EvaLuma.DTOs
{
    // DTO para que el Auditor cree la pregunta con sus opciones de un solo golpe
    public class CrearPreguntaConOpcionesDto
    {
        [Required(ErrorMessage = "El texto de la pregunta es obligatorio.")]
        [StringLength(1000)]
        public string TextoPregunta { get; set; } = null!;

        public int ValorPuntos { get; set; } = 1;

        [Required]
        [MinLength(2, ErrorMessage = "La pregunta debe tener al menos 2 opciones de respuesta.")]
        public List<CrearOpcionDto> Opciones { get; set; } = new();
    }

    public class CrearOpcionDto
    {
        [Required]
        [StringLength(500)]
        public string TextoOpcion { get; set; } = null!;
        public bool EsCorrecta { get; set; }
    }

    // --- DTOs PARA EL EMPLEADO (Modo seguro: sin revelar la respuesta correcta) ---
    public class ExamenDto
    {
        public int IdSimulacion { get; set; }
        public string Titulo { get; set; } = string.Empty;
        public int TiempoEstimadoMinutos { get; set; }
        public List<PreguntaSeguraDto> Preguntas { get; set; } = new();
    }

    public class PreguntaSeguraDto
    {
        public int IdPregunta { get; set; }
        public string TextoPregunta { get; set; } = string.Empty;
        public int ValorPuntos { get; set; }
        public List<OpcionSeguraDto> Opciones { get; set; } = new();
    }

    public class OpcionSeguraDto
    {
        public int IdOpcion { get; set; }
        public string TextoOpcion { get; set; } = string.Empty;
    }

    public class AsignarSimulacionDto
    {
        [Required(ErrorMessage = "El ID del empleado es obligatorio.")]
        public int IdEmpleado { get; set; }
    }

    public class EnviarExamenDto
    {
        [Required(ErrorMessage = "El ID de la asignación es obligatorio.")]
        public int IdAsignacion { get; set; }

        [Required]
        public List<RespuestaSeleccionadaDto> Respuestas { get; set; } = new();
    }

    public class RespuestaSeleccionadaDto
    {
        public int IdPregunta { get; set; }
        public int IdOpcion { get; set; }
    }
}