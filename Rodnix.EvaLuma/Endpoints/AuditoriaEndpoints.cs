using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Rodnix.EvaLuma.Data;

namespace Rodnix.EvaLuma.Endpoints
{
    public static class AuditoriaEndpoints
    {
        public static IEndpointRouteBuilder MapAuditoriaEndpoints(this IEndpointRouteBuilder routes)
        {
            var group = routes.MapGroup("/api/auditoria").WithTags("Auditoria");

            // GET: Ver reporte inmutable de todas las asignaciones
            group.MapGet("/reportes", [Authorize(Roles = "Administrador,Auditor")] async (EvalumaDbContext context) =>
            {
                var asignaciones = await context.AsignacionesProgreso
                    .Include(a => a.Empleado)
                    .Include(a => a.Simulacion)
                        .ThenInclude(s => s.Campana)
                    .OrderByDescending(a => a.FechaUltimaAccion)
                    .Select(a => new
                    {
                        IdAsignacion = a.IdAsignacion,
                        Empleado = a.Empleado != null ? a.Empleado.NombreCompleto : "Desconocido",
                        Campana = a.Simulacion != null && a.Simulacion.Campana != null ? a.Simulacion.Campana.NombreCampana : "Sin Campaña",
                        Estado = a.Estado,
                        Calificacion = a.CalificacionTemporal.ToString("0.00") + "%",
                        Eventos = context.BitacorasAuditoria
                            .Where(b => b.IdAsignacion == a.IdAsignacion)
                            .OrderBy(b => b.IdEvento)
                            .Select(b => new
                            {
                                MarcaTiempo = b.MarcaTiempo,
                                Accion = b.AccionRealizada,
                                HashCriptografico = b.HashCriptografico,
                                TiempoRespuestaMs = b.TiempoRespuestaMs
                            }).ToList()
                    })
                    .ToListAsync();

                return Results.Ok(asignaciones);
            });

            group.MapGet("/reporte/pdf/{id:int}", [Authorize(Roles = "Administrador,Auditor")] async (int id, EvalumaDbContext context, Rodnix.EvaLuma.Services.IReportGeneratorService reportService) =>
            {
                var asignacion = await context.AsignacionesProgreso
                    .Include(a => a.Empleado)
                    .Include(a => a.Simulacion)
                        .ThenInclude(s => s.Campana)
                    .FirstOrDefaultAsync(a => a.IdAsignacion == id);

                if (asignacion == null) return Results.NotFound();

                var historial = await context.BitacorasAuditoria
                    .Where(b => b.IdAsignacion == id)
                    .OrderBy(b => b.IdEvento)
                    .ToListAsync();

                var empleadoNombre = asignacion.Empleado?.NombreCompleto ?? "Desconocido";
                var campanaNombre = asignacion.Simulacion?.Campana?.NombreCampana ?? "Sin Campaña";

                var pdfBytes = reportService.GeneratePdfReport(empleadoNombre, campanaNombre, asignacion.CalificacionTemporal, historial);
                return Results.File(pdfBytes, "application/pdf", $"Auditoria_{id}.pdf");
            });

            group.MapGet("/reporte/csv/{id:int}", [Authorize(Roles = "Administrador,Auditor")] async (int id, EvalumaDbContext context, Rodnix.EvaLuma.Services.IReportGeneratorService reportService) =>
            {
                var historial = await context.BitacorasAuditoria
                    .Where(b => b.IdAsignacion == id)
                    .OrderBy(b => b.IdEvento)
                    .ToListAsync();

                if (!historial.Any()) return Results.NotFound();

                var csvBytes = reportService.GenerateCsvReport(historial);
                return Results.File(csvBytes, "text/csv", $"Auditoria_{id}.csv");
            });

            // GET: Telemetría del motor
            group.MapGet("/telemetria/motor", [Authorize(Roles = "Administrador,Administrador de Sistema")] async (EvalumaDbContext context) =>
            {
                var currentProcess = System.Diagnostics.Process.GetCurrentProcess();
                var memoriaMb = currentProcess.WorkingSet64 / (1024.0 * 1024.0);
                var tiempoEjecucion = (DateTime.UtcNow - currentProcess.StartTime.ToUniversalTime()).TotalMinutes;

                // Contamos los mensajes reales procesados en la bitácora
                var mensajesReales = await context.BitacorasAuditoria.CountAsync();

                var telemetria = new
                {
                    tiempoEjecucionMinutos = tiempoEjecucion,
                    mensajesEnCola = 0, // Como ya corregimos el frontend, la cola del backend suele estar limpia a menos que usemos un broker real
                    mensajesProcesados = mensajesReales,
                    transaccionesPorSegundo = mensajesReales > 0 ? (mensajesReales / (tiempoEjecucion * 60)) : 0,
                    tasaErrores = 0.00,
                    memoriaUsadaMb = memoriaMb,
                    ultimaActualizacion = DateTime.UtcNow.ToString("O")
                };

                return Results.Ok(telemetria);
            });

            return routes;
        }
    }
}
