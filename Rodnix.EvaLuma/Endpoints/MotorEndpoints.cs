using System;
using System.Data;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Http;
using Rodnix.EvaLuma.Data;
using Rodnix.EvaLuma.DTOs;
using Rodnix.EvaLuma.Models;

namespace Rodnix.EvaLuma.Endpoints
{
    public static class MotorEndpoints
    {
        public static IEndpointRouteBuilder MapMotorEndpoints(this IEndpointRouteBuilder routes)
        {
            // POST /api/motor/registrar
            routes.MapPost("/api/motor/registrar", async (ProgresoPayload payload, EvalumaDbContext context, ILogger<Program> logger) =>
            {
                if (payload == null)
                {
                    return Results.BadRequest(new { error = "Payload inválido" });
                }

                try
                {
                    // Iniciar transacción con aislamiento serializable para MySQL
                    await using var transaction = await context.Database.BeginTransactionAsync(IsolationLevel.Serializable);

                    try
                    {
                        // Bloquear la fila de asignación para evitar condiciones de carrera (SELECT ... FOR UPDATE)
                        var asignacion = await context.AsignacionesProgreso
                            .FromSqlInterpolated($"SELECT * FROM Asignacion_Progreso WHERE id_asignacion = {payload.IdAsignacion} FOR UPDATE")
                            .FirstOrDefaultAsync();

                        if (asignacion == null)
                        {
                            logger.LogWarning("Asignación no encontrada: {IdAsignacion}", payload.IdAsignacion);
                            await transaction.RollbackAsync();
                            return Results.NotFound(new { error = "Asignación no encontrada" });
                        }

                        // Leer último registro de bitácora (dentro de la misma transacción)
                        var ultimoRegistro = await context.BitacorasAuditoria
                            .Where(b => b.IdAsignacion == payload.IdAsignacion)
                            .OrderByDescending(b => b.IdEvento)
                            .FirstOrDefaultAsync();

                        var hashPrevio = ultimoRegistro?.HashCriptografico ?? "0";

                        // Construir cadena y calcular SHA-256 encadenado
                        var datosAHashear = $"{payload.IdAsignacion}|{payload.Accion}|{payload.Checkpoint}|{payload.TiempoMs}|{hashPrevio}";
                        string hashCriptografico;
                        using (var sha256 = SHA256.Create())
                        {
                            var bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(datosAHashear));
                            hashCriptografico = Convert.ToHexString(bytes);
                        }

                        // Actualizar checkpoint en Asignacion_Progreso
                        asignacion.UltimoCheckpoint = payload.Checkpoint;
                        asignacion.FechaUltimaAccion = DateTime.UtcNow;
                        context.AsignacionesProgreso.Update(asignacion);

                        // Insertar nuevo registro en Bitacora_Auditoria (append-only)
                        var nuevoRegistro = new BitacoraAuditoria
                        {
                            IdAsignacion = payload.IdAsignacion,
                            AccionRealizada = payload.Accion,
                            TiempoRespuestaMs = payload.TiempoMs,
                            MarcaTiempo = DateTime.UtcNow,
                            HashPrevio = hashPrevio,
                            HashCriptografico = hashCriptografico
                        };

                        context.BitacorasAuditoria.Add(nuevoRegistro);

                        await context.SaveChangesAsync();
                        await transaction.CommitAsync();

                        logger.LogInformation("Progreso registrado. IdAsignacion:{IdAsignacion} Hash:{Hash}",
                            payload.IdAsignacion, hashCriptografico);

                        return Results.Ok(new
                        {
                            idEvento = nuevoRegistro.IdEvento,
                            checkpoint = payload.Checkpoint,
                            hashCriptografico,
                            marcaTiempo = nuevoRegistro.MarcaTiempo
                        });
                    }
                    catch (Exception ex)
                    {
                        await transaction.RollbackAsync();
                        logger.LogError(ex, "Fallo en transacción de registro de progreso. IdAsignacion:{IdAsignacion}", payload.IdAsignacion);
                        return Results.StatusCode(StatusCodes.Status500InternalServerError);
                    }
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Error en endpoint /api/motor/registrar");
                    return Results.StatusCode(StatusCodes.Status500InternalServerError);
                }
            }).WithName("RegistrarProgreso");

            // GET /api/motor/checkpoint/{id}
            routes.MapGet("/api/motor/checkpoint/{id:int}", async (int id, EvalumaDbContext context, ILogger<Program> logger) =>
            {
                try
                {
                    var asignacion = await context.AsignacionesProgreso
                        .AsNoTracking()
                        .Where(a => a.IdAsignacion == id)
                        .Select(a => new
                        {
                            idAsignacion = a.IdAsignacion,
                            estado = a.Estado,
                            ultimoCheckpoint = a.UltimoCheckpoint,
                            calificacionTemporal = a.CalificacionTemporal,
                            fechaInicio = a.FechaInicio,
                            fechaUltimaAccion = a.FechaUltimaAccion,
                            idSimulacion = a.IdSimulacion,
                            idEmpleado = a.IdEmpleado
                        })
                        .FirstOrDefaultAsync();

                    if (asignacion == null)
                    {
                        logger.LogWarning("Asignación no encontrada para retomar: {IdAsignacion}", id);
                        return Results.NotFound(new { error = "Asignación no encontrada" });
                    }

                    var eventos = await context.BitacorasAuditoria
                        .AsNoTracking()
                        .Where(b => b.IdAsignacion == id)
                        .OrderByDescending(b => b.IdEvento)
                        .Take(5)
                        .Select(b => new
                        {
                            idEvento = b.IdEvento,
                            accionRealizada = b.AccionRealizada,
                            tiempoRespuestaMs = b.TiempoRespuestaMs,
                            marcaTiempo = b.MarcaTiempo,
                            hashCriptografico = b.HashCriptografico
                        })
                        .ToListAsync();

                    logger.LogInformation("Checkpoint retomado. IdAsignacion:{IdAsignacion} Checkpoint:{Checkpoint}",
                        id, asignacion.ultimoCheckpoint);

                    return Results.Ok(new
                    {
                        asignacion,
                        ultimosEventos = eventos
                    });
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Error al obtener checkpoint. IdAsignacion:{IdAsignacion}", id);
                    return Results.StatusCode(StatusCodes.Status500InternalServerError);
                }
            }).WithName("ObtenerCheckpoint");

            return routes;
        }
    }
}