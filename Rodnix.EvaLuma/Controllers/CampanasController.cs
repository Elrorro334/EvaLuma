using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Rodnix.EvaLuma.Data;
using Rodnix.EvaLuma.Models;
using System.Security.Claims;

namespace Rodnix.EvaLuma.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize] // Exigimos que estén logueados, pero permitimos el paso a Empleados, Auditores y Administradores
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
    // Al no tener un rol específico aquí, el Empleado SÍ puede entrar a consultar
    [Route("get")]
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> ObtenerCampanas()
    {
        try
        {
            var campanas = await _context.Campanas
                .AsNoTracking()
                .Include(c => c.Auditor) // Obligamos a MySQL a traer la relación antes de mapearla
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
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fallo al intentar obtener la lista de campañas.");

            return StatusCode(500, new
            {
                Error = "Error interno del servidor al procesar el catálogo de campañas.",
                Detalle = ex.Message
            });
        }
    }

    // POST: api/campanas
    [HttpPost]
    [Route("post")]
    [Authorize(Roles = "Auditor, Administrador")] // Candado estricto: El empleado será bloqueado si intenta crear
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
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

    // PUT: api/campanas/{idCampana}
    [HttpPut("{idCampana}")]
    [Authorize(Roles = "Auditor, Administrador")] // Candado estricto: El empleado será bloqueado si intenta editar
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> EditarCampana(int idCampana, [FromBody] EditarCampanaRequest request)
    {
        var campana = await _context.Campanas.FirstOrDefaultAsync(c => c.IdCampana == idCampana);

        if (campana == null)
        {
            return NotFound(new { Error = "La campaña especificada no existe." });
        }

        // Validar que la nueva fecha límite siga siendo coherente
        if (request.FechaLimite.HasValue && request.FechaLimite.Value <= campana.FechaInicio)
        {
            return BadRequest(new { Error = "La nueva fecha límite no puede ser menor o igual a la fecha de inicio original." });
        }

        // Actualizar solo los campos que el Auditor haya enviado (si no vienen nulos)
        if (!string.IsNullOrWhiteSpace(request.NombreCampana))
            campana.NombreCampana = request.NombreCampana;

        if (request.Descripcion != null)
            campana.Descripcion = request.Descripcion;

        if (request.FechaLimite.HasValue)
            campana.FechaLimite = request.FechaLimite.Value;

        _context.Campanas.Update(campana);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Campaña {IdCampana} editada por el Auditor ID: {AuditorId}", campana.IdCampana, User.FindFirst(ClaimTypes.NameIdentifier)?.Value);

        return Ok(new { Mensaje = "Campaña actualizada correctamente.", Campana = campana });
    }

    // POST: api/campanas/{idCampana}/simulaciones
    [HttpPost("{idCampana}/simulaciones")]
    [Authorize(Roles = "Auditor, Administrador")] // Candado estricto: Solo roles administrativos
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
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

    // DTOs para las peticiones
    public class CrearCampanaRequest
    {
        public string NombreCampana { get; set; } = string.Empty;
        public string? Descripcion { get; set; }
        public DateTime FechaInicio { get; set; }
        public DateTime FechaLimite { get; set; }
        public bool Estricta { get; set; } = true;
    }

    public class EditarCampanaRequest
    {
        public string? NombreCampana { get; set; }
        public string? Descripcion { get; set; }
        public DateTime? FechaLimite { get; set; }
    }

    public class CrearSimulacionRequest
    {
        public string Titulo { get; set; } = string.Empty;
        public int TotalPreguntas { get; set; }
        public int? TiempoEstimadoMinutos { get; set; }
    }
}