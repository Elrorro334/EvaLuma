using Microsoft.AspNetCore.SignalR;
using System.Threading.Tasks;
using Rodnix.EvaLuma.DTOs;
using Rodnix.EvaLuma.Services;
using Microsoft.Extensions.Logging;

namespace Rodnix.EvaLuma.Hubs
{
    public class MotorHub : Hub
    {
        private readonly IKafkaProducerService _kafkaProducer;
        private readonly ILogger<MotorHub> _logger;

        public MotorHub(IKafkaProducerService kafkaProducer, ILogger<MotorHub> logger)
        {
            _kafkaProducer = kafkaProducer;
            _logger = logger;
        }

        // El cliente invoca este método "EnviarCheckpoint"
        public async Task EnviarCheckpoint(ProgresoPayload payload)
        {
            if (payload == null || string.IsNullOrEmpty(payload.Checkpoint))
            {
                _logger.LogWarning("Payload inválido recibido por SignalR.");
                return;
            }

            try
            {
                // En lugar de guardar sincrónicamente en la BD, delegamos a Kafka
                await _kafkaProducer.ProduceAsync(payload);
                
                // Opcional: Podríamos confirmar al cliente que se recibió el checkpoint
                await Clients.Caller.SendAsync("CheckpointRecibido", payload.Checkpoint);
                
                _logger.LogInformation("Checkpoint encolado vía SignalR para la asignación {Id}", payload.IdAsignacion);
            }
            catch (System.Exception ex)
            {
                _logger.LogError(ex, "Error al encolar el checkpoint desde SignalR.");
                await Clients.Caller.SendAsync("ErrorCheckpoint", payload.Checkpoint);
            }
        }
    }
}
