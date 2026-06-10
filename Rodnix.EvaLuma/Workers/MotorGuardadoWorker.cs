using Confluent.Kafka;
using System.Text.Json;
using Rodnix.EvaLuma.DTOs;
using System.Threading.Tasks;
using System;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using System.Threading;
using Rodnix.EvaLuma.Data;
using System.Data;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;
using System.Text;
using Rodnix.EvaLuma.Models;
using System.Linq;

namespace Rodnix.EvaLuma.Workers
{
    public class MotorGuardadoWorker : BackgroundService
    {
        private readonly ILogger<MotorGuardadoWorker> _logger;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly string _topic = "evaluma-payloads";
        private readonly IConsumer<Ignore, string> _consumer;

        public MotorGuardadoWorker(ILogger<MotorGuardadoWorker> logger, IServiceScopeFactory scopeFactory)
        {
            _logger = logger;
            _scopeFactory = scopeFactory;

            var config = new ConsumerConfig
            {
                BootstrapServers = "localhost:9092",
                GroupId = "motor-guardado-group",
                AutoOffsetReset = AutoOffsetReset.Earliest,
                EnableAutoCommit = false // Autocommit apagado, comiteamos manualmente después de la transacción ACID
            };
            _consumer = new ConsumerBuilder<Ignore, string>(config).Build();
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Iniciando MotorGuardadoWorker (Kafka Consumer).");
            _consumer.Subscribe(_topic);

            await Task.Yield(); 

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    var consumeResult = _consumer.Consume(stoppingToken);

                    if (consumeResult != null && !string.IsNullOrEmpty(consumeResult.Message.Value))
                    {
                        var payload = JsonSerializer.Deserialize<ProgresoPayload>(consumeResult.Message.Value);
                        if (payload != null)
                        {
                            bool success = await ProcesarPayloadConBackoffAsync(payload, stoppingToken);
                            if (success)
                            {
                                _consumer.Commit(consumeResult);
                            }
                        }
                    }
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error consumiendo de Kafka.");
                    await Task.Delay(5000, stoppingToken);
                }
            }
            _consumer.Close();
        }

        private async Task<bool> ProcesarPayloadConBackoffAsync(ProgresoPayload payload, CancellationToken cancellationToken)
        {
            int maxRetries = 5;
            int delayMs = 100;

            for (int attempt = 1; attempt <= maxRetries; attempt++)
            {
                try
                {
                    using var scope = _scopeFactory.CreateScope();
                    var context = scope.ServiceProvider.GetRequiredService<EvalumaDbContext>();

                    await using var transaction = await context.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, cancellationToken);

                    try
                    {
                        // Bloqueo pesimista para prevenir condiciones de carrera en alta concurrencia
                        var asignacion = await context.AsignacionesProgreso
                            .FromSqlInterpolated($"SELECT * FROM Asignacion_Progreso WHERE id_asignacion = {payload.IdAsignacion} FOR UPDATE")
                            .FirstOrDefaultAsync(cancellationToken);

                        if (asignacion == null)
                        {
                            _logger.LogWarning("Asignación no encontrada: {IdAsignacion}", payload.IdAsignacion);
                            await transaction.RollbackAsync(cancellationToken);
                            return true; // Mensaje descartado para no atascar la cola
                        }

                        // Calcular hash criptográfico
                        var ultimoRegistro = await context.BitacorasAuditoria
                            .Where(b => b.IdAsignacion == payload.IdAsignacion)
                            .OrderByDescending(b => b.IdEvento)
                            .FirstOrDefaultAsync(cancellationToken);

                        var hashPrevio = ultimoRegistro?.HashCriptografico ?? "0";

                        var datosAHashear = $"{payload.IdAsignacion}|{payload.Accion}|{payload.Checkpoint}|{payload.TiempoMs}|{hashPrevio}";
                        string hashCriptografico;
                        using (var sha256 = SHA256.Create())
                        {
                            var bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(datosAHashear));
                            hashCriptografico = Convert.ToHexString(bytes);
                        }

                        asignacion.UltimoCheckpoint = payload.Checkpoint;
                        asignacion.FechaUltimaAccion = DateTime.UtcNow;
                        context.AsignacionesProgreso.Update(asignacion);

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

                        await context.SaveChangesAsync(cancellationToken);
                        await transaction.CommitAsync(cancellationToken);

                        _logger.LogInformation("Progreso guardado (Kafka). Asignacion:{Id} Hash:{Hash}", payload.IdAsignacion, hashCriptografico);
                        return true;
                    }
                    catch (Exception ex)
                    {
                        await transaction.RollbackAsync(cancellationToken);
                        throw new Exception("Fallo en la transacción ACID.", ex);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Fallo procesando payload (Intento {Attempt}/{MaxRetries}). Aplicando Exponential Backoff.", attempt, maxRetries);
                    if (attempt == maxRetries) return false;
                    await Task.Delay(delayMs, cancellationToken);
                    delayMs *= 2; // Exponential backoff
                }
            }
            return false;
        }
    }
}
