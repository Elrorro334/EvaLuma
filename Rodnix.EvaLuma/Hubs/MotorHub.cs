using Microsoft.AspNetCore.SignalR;
using System.Threading.Tasks;
using Rodnix.EvaLuma.DTOs;
using Rodnix.EvaLuma.Services;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;

namespace Rodnix.EvaLuma.Hubs
{
    [Authorize]
    public class MotorHub : Hub
    {
        private readonly IKafkaProducerService _kafkaProducer;
        private readonly ILogger<MotorHub> _logger;
        
        // Key: IdEmpleado, Value: ConnectionId
        private static readonly ConcurrentDictionary<int, string> _activeConnections = new();

        public MotorHub(IKafkaProducerService kafkaProducer, ILogger<MotorHub> logger)
        {
            _kafkaProducer = kafkaProducer;
            _logger = logger;
        }

        public override async Task OnConnectedAsync()
        {
            var userIdClaim = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (int.TryParse(userIdClaim, out int userId))
            {
                if (_activeConnections.TryGetValue(userId, out var existingConnectionId))
                {
                    // Si el usuario ya está conectado en otra pestaña/dispositivo, forzamos su desconexión
                    await Clients.Client(existingConnectionId).SendAsync("SesionConcurrenteDetectada");
                }
                
                _activeConnections[userId] = Context.ConnectionId;
            }

            await base.OnConnectedAsync();
        }

        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            var userIdClaim = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (int.TryParse(userIdClaim, out int userId))
            {
                _activeConnections.TryRemove(userId, out _);
            }

            await base.OnDisconnectedAsync(exception);
        }

        public async Task EnviarCheckpoint(ProgresoPayload payload)
        {
            if (payload == null || string.IsNullOrEmpty(payload.Checkpoint))
            {
                _logger.LogWarning("Payload inválido recibido por SignalR.");
                return;
            }

            try
            {
                await _kafkaProducer.ProduceAsync(payload);
                await Clients.Caller.SendAsync("CheckpointRecibido", payload.Checkpoint);
                _logger.LogInformation("Checkpoint encolado vía SignalR para la asignación {Id}", payload.IdAsignacion);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al encolar el checkpoint desde SignalR.");
                await Clients.Caller.SendAsync("ErrorCheckpoint", payload.Checkpoint);
            }
        }
    }
}
