using Microsoft.AspNetCore.Mvc;
using Rodnix.EvaLuma.Data;
using Rodnix.EvaLuma.Services;
using System.Diagnostics;

namespace Rodnix.EvaLuma.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SystemController : ControllerBase
{
    private readonly EvalumaDbContext _context;
    private readonly IWebHostEnvironment _env;
    private readonly IQueueMonitorService _queueMonitor;

    public SystemController(EvalumaDbContext context, IWebHostEnvironment env, IQueueMonitorService queueMonitor)
    {
        _context = context;
        _env = env;
        _queueMonitor = queueMonitor;
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

    [HttpGet("graph")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetArchitectureGraph()
    {
        // Verificar DB
        bool dbActiva;
        try { dbActiva = await _context.Database.CanConnectAsync(); }
        catch { dbActiva = false; }

        // Verificar Kafka / RAM
        var queueStatus = await _queueMonitor.GetCurrentQueueStatusAsync();
        var kafkaStatus = queueStatus.Queues.FirstOrDefault(q => q.QueueName.Contains("Kafka"));
        bool kafkaActivo = kafkaStatus?.Status == "Active";

        var nodes = new List<object>
        {
            new { id = "client", label = "Frontend (React/Next.js)", type = "client", status = "healthy" },
            new { id = "api", label = "EVALUMA API (.NET 9)", type = "service", status = "healthy", metrics = new { ram = queueStatus.Queues.FirstOrDefault(q => q.QueueName.Contains("Telemetría"))?.MessageCount + " MB" } },
            new { id = "signalr", label = "MotorHub (WebSockets)", type = "hub", status = "healthy" },
            new { id = "kafka", label = "Apache Kafka (evaluma-payloads)", type = "broker", status = kafkaActivo ? "healthy" : "error", metrics = new { consumers = kafkaStatus?.ActiveConsumers } },
            new { id = "worker", label = "MotorGuardadoWorker", type = "worker", status = "healthy" },
            new { id = "db", label = "MySQL (ACID)", type = "database", status = dbActiva ? "healthy" : "error" }
        };

        var edges = new List<object>
        {
            new { source = "client", target = "api", label = "REST (HTTP)" },
            new { source = "client", target = "signalr", label = "WebSocket (WSS)" },
            new { source = "api", target = "kafka", label = "Produce (Topic)" },
            new { source = "signalr", target = "kafka", label = "Produce (Topic)" },
            new { source = "kafka", target = "worker", label = "Consume (Group)" },
            new { source = "worker", target = "db", label = "Save (Serializable TX)" },
            new { source = "api", target = "db", label = "Read/Write" }
        };

        return Ok(new { nodes, edges, timestamp = DateTime.UtcNow });
    }
}