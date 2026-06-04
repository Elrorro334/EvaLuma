using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Rodnix.EvaLuma.Data;
using Rodnix.EvaLuma.DTOs;
using Rodnix.EvaLuma.Models;
using System.Security.Claims;

namespace Rodnix.EvaLuma.Endpoints
{
    public static class CampanasEndpoints
    {
        public static IEndpointRouteBuilder MapCampanasEndpoints(this IEndpointRouteBuilder routes)
        {
            var group = routes.MapGroup("/api/campanas").WithTags("Campañas");

            // GET: Ver campañas (Abierto para cualquier logueado, incluyendo Empleados)
            group.MapGet("/", [Authorize] async (EvalumaDbContext context, ILogger<Program> logger) =>
            {
                try
                {
                    var campanas = await context.Campanas
                        .AsNoTracking()
                        .Include(c => c.Auditor)
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

                    return Results.Ok(campanas);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Fallo al obtener campañas");
                    return Results.Problem("Error interno al procesar el catálogo.");
                }
            });

            // POST: Crear campaña (Candado estricto)
            group.MapPost("/", [Authorize(Roles = "Auditor, Administrador")] async (CrearCampanaDto request, HttpContext httpContext, EvalumaDbContext context) =>
            {
                if (request.FechaLimite <= request.FechaInicio)
                    return Results.BadRequest(new { Error = "La fecha límite debe ser posterior al inicio." });

                var userIdClaim = httpContext.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (!int.TryParse(userIdClaim, out int idAuditor)) return Results.Unauthorized();

                var nuevaCampana = new Campana
                {
                    IdAuditor = idAuditor,
                    NombreCampana = request.NombreCampana,
                    Descripcion = request.Descripcion ?? string.Empty,
                    FechaInicio = request.FechaInicio,
                    FechaLimite = request.FechaLimite,
                    Estricta = request.Estricta
                };

                context.Campanas.Add(nuevaCampana);
                await context.SaveChangesAsync();

                return Results.Created($"/api/campanas/{nuevaCampana.IdCampana}", nuevaCampana);
            });

            // PUT: Editar campaña (Candado estricto)
            group.MapPut("/{idCampana:int}", [Authorize(Roles = "Auditor, Administrador")] async (int idCampana, EditarCampanaDto request, EvalumaDbContext context) =>
            {
                var campana = await context.Campanas.FirstOrDefaultAsync(c => c.IdCampana == idCampana);
                if (campana == null) return Results.NotFound(new { Error = "La campaña no existe." });

                if (request.FechaLimite.HasValue && request.FechaLimite.Value <= campana.FechaInicio)
                    return Results.BadRequest(new { Error = "Fecha límite inválida." });

                if (!string.IsNullOrWhiteSpace(request.NombreCampana)) campana.NombreCampana = request.NombreCampana;
                if (request.Descripcion != null) campana.Descripcion = request.Descripcion;
                if (request.FechaLimite.HasValue) campana.FechaLimite = request.FechaLimite.Value;

                context.Campanas.Update(campana);
                await context.SaveChangesAsync();

                return Results.Ok(new { Mensaje = "Campaña actualizada", Campana = campana });
            });

            // POST: Agregar simulación (Candado estricto)
            group.MapPost("/{idCampana:int}/simulaciones", [Authorize(Roles = "Auditor, Administrador")] async (int idCampana, CrearSimulacionDto request, EvalumaDbContext context) =>
            {
                var existe = await context.Campanas.AnyAsync(c => c.IdCampana == idCampana);
                if (!existe) return Results.NotFound(new { Error = "Campaña no existe." });

                var nuevaSimulacion = new Simulacion
                {
                    IdCampana = idCampana,
                    Titulo = request.Titulo,
                    TotalPreguntas = request.TotalPreguntas,
                    TiempoEstimadoMinutos = request.TiempoEstimadoMinutos
                };

                context.Simulaciones.Add(nuevaSimulacion);
                await context.SaveChangesAsync();

                return Results.Created($"/api/campanas/{idCampana}/simulaciones/{nuevaSimulacion.IdSimulacion}", nuevaSimulacion);
            });

            // GET: Obtener simulaciones de una campaña
            group.MapGet("/{idCampana:int}/simulaciones", [Authorize] async (int idCampana, EvalumaDbContext context) =>
            {
                var campanaExiste = await context.Campanas.AnyAsync(c => c.IdCampana == idCampana);
                if (!campanaExiste) return Results.NotFound(new { Error = "La campaña especificada no existe." });

                var simulaciones = await context.Simulaciones
                    .AsNoTracking()
                    .Where(s => s.IdCampana == idCampana)
                    .ToListAsync();

                return Results.Ok(simulaciones);
            });

            // GET: Evaluaciones asignadas al empleado
            group.MapGet("/mis-asignaciones", [Authorize(Roles = "Empleado")] async (HttpContext httpContext, EvalumaDbContext context) =>
            {
                var userIdClaim = httpContext.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (!int.TryParse(userIdClaim, out int idEmpleado)) return Results.Unauthorized();

                var misEvaluaciones = await context.AsignacionesProgreso
                    .AsNoTracking()
                    .Include(a => a.Simulacion).ThenInclude(s => s!.Campana)
                    .Where(a => a.IdEmpleado == idEmpleado)
                    .Select(a => new
                    {
                        a.IdAsignacion,
                        a.Estado,
                        a.UltimoCheckpoint,
                        a.CalificacionTemporal,
                        Simulacion = a.Simulacion!.Titulo,
                        a.Simulacion.TotalPreguntas,
                        a.Simulacion.TiempoEstimadoMinutos,
                        Campana = a.Simulacion.Campana!.NombreCampana,
                        a.Simulacion.Campana.FechaLimite
                    })
                    .ToListAsync();

                return Results.Ok(misEvaluaciones);
            });

            return routes;
        }
    }
}