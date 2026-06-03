using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Rodnix.EvaLuma.Data;
using Rodnix.EvaLuma.Models;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

namespace Rodnix.EvaLuma.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "Empleado")] // Seguridad estricta: Solo los empleados interactúan aquí
public class EvaluacionController : ControllerBase
{
    private readonly EvalumaDbContext _context;
    private readonly ILogger<EvaluacionController> _logger;

    public EvaluacionController(EvalumaDbContext context, ILogger<EvaluacionController> logger)
    {
        _context = context;
        _logger = logger;
    }

    // GET: api/evaluacion/mis-asignaciones
    [HttpGet("mis-asignaciones")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> ObtenerMisAsignaciones()
    {
        // Extraemos el ID del empleado directamente de su token seguro
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!int.TryParse(userIdClaim, out int idEmpleado))
        {
            return Unauthorized(new { Error = "No se pudo identificar tu sesión." });
        }

        // Buscamos solo las evaluaciones asignadas a este empleado específico
        var misEvaluaciones = await _context.AsignacionesProgreso
            .AsNoTracking()
            .Include(a => a.Simulacion)
                .ThenInclude(s => s!.Campana)
            .Where(a => a.IdEmpleado == idEmpleado)
            .Select(a => new
            {
                a.IdAsignacion,
                a.Estado,
                a.UltimoCheckpoint,
                a.CalificacionTemporal,
                Simulacion = a.Simulacion!.Titulo,
                TotalPreguntas = a.Simulacion.TotalPreguntas,
                TiempoMinutos = a.Simulacion.TiempoEstimadoMinutos,
                Campana = a.Simulacion.Campana!.NombreCampana,
                FechaLimite = a.Simulacion.Campana.FechaLimite
            })
            .ToListAsync();

        return Ok(misEvaluaciones);
    }

    // POST: api/evaluacion/guardar-checkpoint
    [HttpPost("guardar-checkpoint")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GuardarProgresoAsincrono([FromBody] CheckpointRequest request)
    {
        // Aislamiento Transaccional: Iniciamos la transacción para asegurar propiedades ACID
        using var transaction = await _context.Database.BeginTransactionAsync();

        try
        {
            var asignacion = await _context.AsignacionesProgreso
                .FirstOrDefaultAsync(a => a.IdAsignacion == request.IdAsignacion);

            if (asignacion == null)
            {
                return NotFound(new { Error = "Asignación no encontrada." });
            }

            // Validar que el empleado que manda la petición es el dueño de la evaluación
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (asignacion.IdEmpleado.ToString() != userIdClaim)
            {
                return Forbid();
            }

            // Lógica de la Bitácora Inmutable (Append-Only)
            var ultimoEvento = await _context.BitacorasAuditoria
                .Where(b => b.IdAsignacion == request.IdAsignacion)
                .OrderByDescending(b => b.IdEvento)
                .FirstOrDefaultAsync();

            string hashPrevio = ultimoEvento?.HashCriptografico ?? GenerarHashGenesis(request.IdAsignacion.ToString());

            // Generar el nuevo hash encadenado
            string cadenaParaHash = $"{request.IdAsignacion}-{request.AccionRealizada}-{request.TiempoRespuestaMs}-{hashPrevio}";
            string nuevoHash = GenerarSha256(cadenaParaHash);

            var nuevoRegistroAuditoria = new BitacoraAuditoria
            {
                IdAsignacion = request.IdAsignacion,
                AccionRealizada = request.AccionRealizada,
                TiempoRespuestaMs = request.TiempoRespuestaMs,
                MarcaTiempo = DateTime.UtcNow,
                HashPrevio = hashPrevio,
                HashCriptografico = nuevoHash
            };

            // Actualizar el Checkpoint del Empleado
            asignacion.UltimoCheckpoint = request.PuntoDeControlFrontEnd;
            asignacion.FechaUltimaAccion = DateTime.UtcNow;
            if (asignacion.Estado == "Pendiente") asignacion.Estado = "En Progreso";

            // Guardar cambios en la base de datos
            _context.BitacorasAuditoria.Add(nuevoRegistroAuditoria);
            _context.AsignacionesProgreso.Update(asignacion);

            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            return Ok(new { Mensaje = "Checkpoint guardado exitosamente.", Checkpoint = asignacion.UltimoCheckpoint });
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            _logger.LogError(ex, "Fallo transaccional al guardar checkpoint para asignación {IdAsignacion}", request.IdAsignacion);

            return StatusCode(500, new { Error = "Fallo de persistencia. El frontend debe reintentar (Fallback local)." });
        }
    }

    // Funciones criptográficas privadas
    private string GenerarSha256(string rawData)
    {
        byte[] bytes = SHA256.HashData(Encoding.UTF8.GetBytes(rawData));
        StringBuilder builder = new StringBuilder();
        for (int i = 0; i < bytes.Length; i++)
        {
            builder.Append(bytes[i].ToString("x2"));
        }
        return builder.ToString();
    }

    private string GenerarHashGenesis(string semilla)
    {
        return GenerarSha256($"GENESIS-EVALUMA-{semilla}");
    }

    // DTO
    public class CheckpointRequest
    {
        public int IdAsignacion { get; set; }
        public string AccionRealizada { get; set; } = string.Empty;
        public int TiempoRespuestaMs { get; set; }
        public string PuntoDeControlFrontEnd { get; set; } = string.Empty;
    }
}