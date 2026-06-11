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
    public static class SimulacionesEndpoints
    {
        public static IEndpointRouteBuilder MapSimulacionesEndpoints(this IEndpointRouteBuilder routes)
        {
            var group = routes.MapGroup("/api/simulaciones").WithTags("Simulaciones y Exámenes");

            // POST: Agregar una pregunta con sus opciones (SOLO AUDITOR/ADMINISTRADOR)
            group.MapPost("/{idSimulacion:int}/preguntas", [Authorize(Roles = "Auditor, Administrador")] async (int idSimulacion, CrearPreguntaConOpcionesDto request, EvalumaDbContext context) =>
            {
                var simulacionExiste = await context.Simulaciones.AnyAsync(s => s.IdSimulacion == idSimulacion);
                if (!simulacionExiste) return Results.NotFound(new { Error = "La simulación no existe." });

                // Verificamos que al menos una opción esté marcada como correcta
                if (!request.Opciones.Any(o => o.EsCorrecta))
                {
                    return Results.BadRequest(new { Error = "Debe haber al menos una respuesta correcta." });
                }

                // Iniciamos transacción para que se guarde todo o nada
                using var transaction = await context.Database.BeginTransactionAsync();
                try
                {
                    var nuevaPregunta = new Pregunta
                    {
                        IdSimulacion = idSimulacion,
                        TextoPregunta = request.TextoPregunta,
                        ValorPuntos = request.ValorPuntos
                    };

                    context.Preguntas.Add(nuevaPregunta);
                    await context.SaveChangesAsync(); // Guardamos para generar el IdPregunta

                    var opciones = request.Opciones.Select(o => new OpcionRespuesta
                    {
                        IdPregunta = nuevaPregunta.IdPregunta,
                        TextoOpcion = o.TextoOpcion,
                        EsCorrecta = o.EsCorrecta
                    }).ToList();

                    context.OpcionesRespuesta.AddRange(opciones);
                    await context.SaveChangesAsync();

                    await transaction.CommitAsync();

                    return Results.Created($"/api/simulaciones/{idSimulacion}/preguntas/{nuevaPregunta.IdPregunta}", new { Mensaje = "Pregunta y opciones guardadas con éxito." });
                }
                catch (Exception)
                {
                    await transaction.RollbackAsync();
                    return Results.Problem("Fallo al guardar la pregunta en la base de datos.");
                }
            });

            // GET: Obtener todas las preguntas de una simulación (SOLO AUDITOR/ADMIN) - Expone cuál es la correcta
            group.MapGet("/{idSimulacion:int}/preguntas-admin", [Authorize(Roles = "Auditor, Administrador")] async (int idSimulacion, EvalumaDbContext context) =>
            {
                var simulacion = await context.Simulaciones
                    .AsNoTracking()
                    .Include(s => s.Preguntas)
                        .ThenInclude(p => p.Opciones)
                    .FirstOrDefaultAsync(s => s.IdSimulacion == idSimulacion);

                if (simulacion == null) return Results.NotFound(new { Error = "Simulación no encontrada." });

                // Retornamos todo directamente, incluyendo el flag EsCorrecta
                return Results.Ok(new
                {
                    simulacion.IdSimulacion,
                    simulacion.Titulo,
                    Preguntas = simulacion.Preguntas.Select(p => new
                    {
                        p.IdPregunta,
                        p.TextoPregunta,
                        p.ValorPuntos,
                        Opciones = p.Opciones.Select(o => new
                        {
                            o.IdOpcion,
                            o.TextoOpcion,
                            o.EsCorrecta
                        })
                    })
                });
            });

            // DELETE: Eliminar una pregunta específica (SOLO AUDITOR/ADMIN)
            group.MapDelete("/preguntas/{idPregunta:int}", [Authorize(Roles = "Auditor, Administrador")] async (int idPregunta, EvalumaDbContext context) =>
            {
                var pregunta = await context.Preguntas.FindAsync(idPregunta);
                if (pregunta == null) return Results.NotFound(new { Error = "La pregunta no existe." });

                // Esto eliminará en cascada las opciones gracias a la configuración de EF Core
                context.Preguntas.Remove(pregunta);
                await context.SaveChangesAsync();

                return Results.Ok(new { Mensaje = "Pregunta eliminada correctamente." });
            });

            // GET: Obtener el examen completo para el empleado (SOLO EMPLEADO)
            group.MapGet("/{idSimulacion:int}/examen", [Authorize(Roles = "Empleado")] async (int idSimulacion, EvalumaDbContext context) =>
            {
                var simulacion = await context.Simulaciones
                    .AsNoTracking()
                    .Include(s => s.Preguntas)
                        .ThenInclude(p => p.Opciones)
                    .FirstOrDefaultAsync(s => s.IdSimulacion == idSimulacion);

                if (simulacion == null) return Results.NotFound(new { Error = "Examen no encontrado." });

                // Mapeamos al DTO seguro (ignoramos el campo 'EsCorrecta' a propósito)
                var examenSeguro = new ExamenDto
                {
                    IdSimulacion = simulacion.IdSimulacion,
                    Titulo = simulacion.Titulo,
                    TiempoEstimadoMinutos = simulacion.TiempoEstimadoMinutos ?? 0,
                    Preguntas = simulacion.Preguntas.Select(p => new PreguntaSeguraDto
                    {
                        IdPregunta = p.IdPregunta,
                        TextoPregunta = p.TextoPregunta,
                        ValorPuntos = p.ValorPuntos,
                        Opciones = p.Opciones.Select(o => new OpcionSeguraDto
                        {
                            IdOpcion = o.IdOpcion,
                            TextoOpcion = o.TextoOpcion
                        }).ToList()
                    }).ToList()
                };

                return Results.Ok(examenSeguro);
            });

            // POST: Asignar una simulación a un empleado (SOLO AUDITOR/ADMINISTRADOR)
            group.MapPost("/{idSimulacion:int}/asignar", [Authorize(Roles = "Auditor, Administrador")] async (int idSimulacion, AsignarSimulacionDto request, EvalumaDbContext context, ILogger<Program> logger) =>
            {
                // 1. Validar que la simulación exista
                var simulacionExiste = await context.Simulaciones.AnyAsync(s => s.IdSimulacion == idSimulacion);
                if (!simulacionExiste) return Results.NotFound(new { Error = "La simulación especificada no existe." });

                // 2. Validar que el usuario exista y sea de rol Empleado
                var empleado = await context.Usuarios.FirstOrDefaultAsync(u => u.IdUsuario == request.IdEmpleado);
                if (empleado == null || empleado.Rol != "Empleado")
                {
                    return Results.BadRequest(new { Error = "El usuario especificado no existe o no tiene el rol de Empleado." });
                }

                // 3. Validar que no se le haya asignado ya esta simulación previamente
                var asignacionPrevia = await context.AsignacionesProgreso
                    .AnyAsync(a => a.IdSimulacion == idSimulacion && a.IdEmpleado == request.IdEmpleado);

                if (asignacionPrevia)
                {
                    return Results.BadRequest(new { Error = "El empleado ya tiene asignada esta evaluación." });
                }

                // 4. Crear la asignación inicial limpia
                var nuevaAsignacion = new AsignacionProgreso
                {
                    IdSimulacion = idSimulacion,
                    IdEmpleado = request.IdEmpleado,
                    Estado = "Pendiente",
                    UltimoCheckpoint = string.Empty,
                    CalificacionTemporal = 0,
                    FechaInicio = DateTime.UtcNow,
                    FechaUltimaAccion = DateTime.UtcNow
                };

                context.AsignacionesProgreso.Add(nuevaAsignacion);
                await context.SaveChangesAsync();

                logger.LogInformation("Simulación {IdSimulacion} asignada al empleado {IdEmpleado}. AsignacionID: {IdAsignacion}",
                    idSimulacion, request.IdEmpleado, nuevaAsignacion.IdAsignacion);

                return Results.Created($"/api/simulaciones/{idSimulacion}/asignar", new
                {
                    Mensaje = "Evaluación asignada exitosamente al empleado.",
                    IdAsignacion = nuevaAsignacion.IdAsignacion
                });
            });

            // POST: Calificar el examen del empleado (SOLO EMPLEADO)
            group.MapPost("/calificar", [Authorize(Roles = "Empleado")] async (EnviarExamenDto request, HttpContext httpContext, EvalumaDbContext context) =>
            {
                var userIdClaim = httpContext.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (!int.TryParse(userIdClaim, out int idEmpleado)) return Results.Unauthorized();

                // 1. Buscar la asignación y verificar que le pertenece a este empleado
                var asignacion = await context.AsignacionesProgreso
                    .Include(a => a.Simulacion)
                    .FirstOrDefaultAsync(a => a.IdAsignacion == request.IdAsignacion && a.IdEmpleado == idEmpleado);

                if (asignacion == null)
                    return Results.NotFound(new { Error = "Asignación no encontrada." });

                if (asignacion.Estado == "Completado")
                    return Results.BadRequest(new { Error = "Este examen ya fue completado y calificado previamente." });

                // 2. Traer las opciones correctas de esta simulación desde la BD
                var opcionesCorrectas = await context.OpcionesRespuesta
                    .Include(o => o.Pregunta)
                    .Where(o => o.Pregunta!.IdSimulacion == asignacion.IdSimulacion && o.EsCorrecta)
                    .ToListAsync();

                // 3. Lógica de Calificación
                int puntosObtenidos = 0;
                int puntosTotales = opcionesCorrectas.Sum(o => o.Pregunta!.ValorPuntos);

                foreach (var respuesta in request.Respuestas)
                {
                    // Verificamos si la opción que mandó el empleado coincide con la correcta en la BD
                    var esCorrecta = opcionesCorrectas.Any(o => o.IdPregunta == respuesta.IdPregunta && o.IdOpcion == respuesta.IdOpcion);
                    if (esCorrecta)
                    {
                        var pregunta = opcionesCorrectas.First(o => o.IdPregunta == respuesta.IdPregunta).Pregunta;
                        puntosObtenidos += pregunta!.ValorPuntos;
                    }
                }

                // Calcular el porcentaje (0 a 100)
                decimal calificacionFinal = puntosTotales > 0 ? Math.Round(((decimal)puntosObtenidos / puntosTotales) * 100, 2) : 0;

                // 4. Actualizar y cerrar la asignación
                asignacion.CalificacionTemporal = calificacionFinal;
                asignacion.Estado = "Completado";
                asignacion.FechaUltimaAccion = DateTime.UtcNow;

                context.AsignacionesProgreso.Update(asignacion);
                await context.SaveChangesAsync();

                return Results.Ok(new
                {
                    Mensaje = "Examen calificado exitosamente.",
                    Calificacion = calificacionFinal,
                    Aprobado = calificacionFinal >= 80, // Puedes ajustar este 80 si la empresa requiere otro mínimo
                    TotalPuntos = puntosObtenidos
                });
            });

            return routes;
        }
    }
}