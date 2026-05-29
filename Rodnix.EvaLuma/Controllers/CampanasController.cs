using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Rodnix.EvaLuma.Data;
using Rodnix.EvaLuma.Models;
using System.Security.Claims;

namespace Rodnix.EvaLuma.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "Auditor, Administrador")]
public class CampanasController : ControllerBase
{
    private readonly EvalumaDbContext _context;
    private readonly ILogger<CampanasController> _logger;

    public CampanasController(EvalumaDbContext context, ILogger<CampanasController> logger)
    {
        _context = context;
        _logger = logger;
    }

    // GET: api/campanas
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> ObtenerCampanas()
    {
        var campanas = await _context.Campanas
            .AsNoTracking()
            .Select(c => new
            {
                c.IdCampana,
                c.NombreCampana,
                c.Descripcion,
                c.FechaInicio,
                c.FechaLimite,
                c.Estricta,
                Auditor = c.Auditor != null ? c.Auditor.NombreCompleto : "Desconocido"
            })
            .ToListAsync();

        return Ok(campanas);
    }

    // POST: api/campanas
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CrearCampana([FromBody] CrearCampanaRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.NombreCampana) || request.FechaLimite <= request.FechaInicio)
        {
            return BadRequest(new { Error = "Datos inválidos. Verifica el nombre y que la fecha límite sea posterior a la de inicio." });
        }

        // Extraer el ID del usuario autenticado desde el Token JWT
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!int.TryParse(userIdClaim, out int idAuditor))
        {
            return Unauthorized(new { Error = "No se pudo identificar al auditor de la sesión." });
        }

        var nuevaCampana = new Campana
        {
            IdAuditor = idAuditor,
            NombreCampana = request.NombreCampana,
            Descripcion = request.Descripcion ?? string.Empty,
            FechaInicio = request.FechaInicio,
            FechaLimite = request.FechaLimite,
            Estricta = request.Estricta
        };

        _context.Campanas.Add(nuevaCampana);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Nueva campaña creada: {Nombre} por Auditor ID: {AuditorId}", nuevaCampana.NombreCampana, idAuditor);

        return StatusCode(201, nuevaCampana);
    }

    // POST: api/campanas/{idCampana}/simulaciones
    [HttpPost("{idCampana}/simulaciones")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> AgregarSimulacion(int idCampana, [FromBody] CrearSimulacionRequest request)
    {
        var campanaExiste = await _context.Campanas.AnyAsync(c => c.IdCampana == idCampana);
        if (!campanaExiste)
        {
            return NotFound(new { Error = "La campaña especificada no existe." });
        }

        if (string.IsNullOrWhiteSpace(request.Titulo) || request.TotalPreguntas <= 0)
        {
            return BadRequest(new { Error = "El título es obligatorio y el total de preguntas debe ser mayor a 0." });
        }

        var nuevaSimulacion = new Simulacion
        {
            IdCampana = idCampana,
            Titulo = request.Titulo,
            TotalPreguntas = request.TotalPreguntas,
            TiempoEstimadoMinutos = request.TiempoEstimadoMinutos
        };

        _context.Simulaciones.Add(nuevaSimulacion);
        await _context.SaveChangesAsync();

        return StatusCode(201, nuevaSimulacion);
    }

    // DTOs (Data Transfer Objects) para las peticiones
    public class CrearCampanaRequest
    {
        public string NombreCampana { get; set; } = string.Empty;
        public string? Descripcion { get; set; }
        public DateTime FechaInicio { get; set; }
        public DateTime FechaLimite { get; set; }
        public bool Estricta { get; set; } = true;
    }

    public class CrearSimulacionRequest
    {
        public string Titulo { get; set; } = string.Empty;
        public int TotalPreguntas { get; set; }
        public int? TiempoEstimadoMinutos { get; set; }
    }
}