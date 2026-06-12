using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Rodnix.EvaLuma.Models;

public class OpcionRespuesta
{
    [Key]
    public int IdOpcion { get; set; }

    [Required]
    public int IdPregunta { get; set; }

    [Required]
    [StringLength(500)]
    public string TextoOpcion { get; set; } = string.Empty;

    // El empleado NUNCA debe ver este campo en el Frontend
    public bool EsCorrecta { get; set; }

    public int? IdSiguientePregunta { get; set; }

    [ForeignKey("IdPregunta")]
    public Pregunta? Pregunta { get; set; }
}