using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using Rodnix.EvaLuma.DTOs;
using Rodnix.EvaLuma.Services;
using System.Diagnostics;

namespace Rodnix.EvaLuma.Endpoints
{
    public static class TelemetryEndpoints
    {
        public static void MapTelemetryEndpoints(this IEndpointRouteBuilder app)
        {
            var group = app.MapGroup("/api/admin/telemetry")
                           .WithTags("Administración - Telemetría e Infraestructura")
                           .RequireAuthorization();

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
            group.MapGet("/queues", async Task<Ok<QueueStatusDto>> (IQueueMonitorService queueService) =>
            {
                // Llamamos a la lógica dinámica
                var responseDto = await queueService.GetCurrentQueueStatusAsync();

                return TypedResults.Ok(responseDto);
            })
              .WithSummary("Monitorear estado asíncrono")
              .WithDescription("Retorna el estatus dinámico de las colas de mensajes.");
        }
    }
}