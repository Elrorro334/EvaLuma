using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Rodnix.EvaLuma.Models;

public class Pregunta
{
    [Key]
    public int IdPregunta { get; set; }

    [Required]
    public int IdSimulacion { get; set; }

    [Required]
    [StringLength(1000)]
    public string TextoPregunta { get; set; } = string.Empty;

    // Útil si algunas preguntas éticas tienen más peso que otras
    public int ValorPuntos { get; set; } = 1;

    [ForeignKey("IdSimulacion")]
    public Simulacion? Simulacion { get; set; }

    public ICollection<OpcionRespuesta> Opciones { get; set; } = new List<OpcionRespuesta>();
}