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

            return routes;
        }
    }
}
