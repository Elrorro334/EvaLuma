using Microsoft.AspNetCore.Mvc;
using Rodnix.EvaLuma.Data;
using System.Diagnostics;

namespace Rodnix.EvaLuma.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SystemController : ControllerBase
{
    private readonly EvalumaDbContext _context;
    private readonly IWebHostEnvironment _env;

    public SystemController(EvalumaDbContext context, IWebHostEnvironment env)
    {
        _context = context;
        _env = env;
    }

    [HttpGet("health")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> GetSystemHealth()
    {
        var stopwatch = Stopwatch.StartNew();
        bool dbActiva;
        string dbDetalle;

        try
        {
            dbActiva = await _context.Database.CanConnectAsync();
            dbDetalle = dbActiva ? "Conexión estable" : "Rechazo de conexión en el puerto de base de datos";
        }
        catch (Exception ex)
        {
            dbActiva = false;
            dbDetalle = ex.Message;
        }

        stopwatch.Stop();

        var tiempoActividad = TimeSpan.FromMilliseconds(Environment.TickCount64);

        var payload = new
        {
            Estado = dbActiva ? "Operativo" : "Degradado",
            Entorno = _env.EnvironmentName,
            MarcaTiempoUtc = DateTime.UtcNow,
            TiempoActividad = $"{tiempoActividad.Days}d {tiempoActividad.Hours}h {tiempoActividad.Minutes}m {tiempoActividad.Seconds}s",
            Componentes = new
            {
                Api = "Operativo",
                BaseDeDatos = new
                {
                    Estado = dbActiva ? "Operativo" : "Fallo",
                    Detalle = dbDetalle,
                    LatenciaMs = stopwatch.ElapsedMilliseconds
                }
            }
        };

        if (!dbActiva)
        {
            return StatusCode(503, payload);
        }

        return Ok(payload);
    }
}