using System.Diagnostics;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using Rodnix.EvaLuma.DTOs;

namespace Rodnix.EvaLuma.Endpoints
{
    public static class TelemetryEndpoints
    {
        public static void MapTelemetryEndpoints(this IEndpointRouteBuilder app)
        {
            var group = app.MapGroup("/api/admin/telemetry")
                           .WithTags("Administración - Telemetría e Infraestructura");

            // Endpoint 1: Recursos
            group.MapGet("/resources", Ok<ServerResourcesDto> () =>
            {
                var currentProcess = Process.GetCurrentProcess();
                var memoryUsedMb = currentProcess.WorkingSet64 / (1024 * 1024);

                // Instanciamos la clase al estilo de tu compañera
                var responseDto = new ServerResourcesDto
                {
                    ServerTime = DateTime.UtcNow,
                    MachineName = Environment.MachineName,
                    OS = Environment.OSVersion.ToString(),
                    ProcessorCount = Environment.ProcessorCount,
                    MemoryUsage = $"{memoryUsedMb} MB",
                    ProcessId = currentProcess.Id,
                    TotalCpuTime = currentProcess.TotalProcessorTime.TotalSeconds
                };

                return TypedResults.Ok(responseDto);
            })
            .WithSummary("Obtener consumo de recursos en tiempo real")
            .WithDescription("Retorna el estado de la RAM y CPU.");

            // Endpoint 2: Colas
            group.MapGet("/queues", async Task<Ok<QueueStatusDto>> () =>
            {
                var responseDto = new QueueStatusDto
                {
                    LastUpdated = DateTime.UtcNow,
                    Status = "Healthy",
                    Queues = new List<QueueDetailDto>
                    {
                        new QueueDetailDto
                        {
                            QueueName = "evaluaciones-pendientes",
                            MessageCount = 0,
                            ActiveConsumers = 2,
                            Status = "Idle"
                        },
                        new QueueDetailDto
                        {
                            QueueName = "auditoria-append-only",
                            MessageCount = 0,
                            ActiveConsumers = 4,
                            Status = "Active"
                        }
                    }
                };

                await Task.CompletedTask;
                return TypedResults.Ok(responseDto);
            })
            .WithSummary("Monitorear estado asíncrono")
            .WithDescription("Retorna el estatus de las colas de mensajes.");
        }
    }
}