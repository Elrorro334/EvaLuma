using Rodnix.EvaLuma.DTOs;
using Confluent.Kafka;
// using RabbitMQ.Client; 
// using Microsoft.EntityFrameworkCore; 

namespace Rodnix.EvaLuma.Services
{
    public class QueueMonitorService : IQueueMonitorService
    {
        
        // private readonly EvalumaDbContext _context;
        // public QueueMonitorService(EvalumaDbContext context) { _context = context; }

        public async Task<QueueStatusDto> GetCurrentQueueStatusAsync()
        {
            var adminConfig = new AdminClientConfig { BootstrapServers = "localhost:9092" };
            int pendingMessages = 0;
            int activeWorkers = 0;

            try
            {
                using var adminClient = new AdminClientBuilder(adminConfig).Build();
                var metadata = adminClient.GetMetadata("evaluma-payloads", TimeSpan.FromSeconds(5));
                
                // Si el tópico existe, asumimos que está activo
                if (metadata.Topics.Count > 0)
                {
                    activeWorkers = 1; // Nuestro Worker de BackgroundService
                    // Confluent.Kafka AdminClient no expone "Lag" directo sin consultar consumer groups
                    // Simularemos el conteo para esta demo o dejarlo en N/A si no se puede
                    pendingMessages = 0; 
                }
            }
            catch(Exception)
            {
                // Si no podemos contactar Kafka
            }

            var currentProcess = System.Diagnostics.Process.GetCurrentProcess();
            var ramUsageMb = currentProcess.WorkingSet64 / (1024 * 1024);

            var responseDto = new QueueStatusDto
            {
                LastUpdated = DateTime.UtcNow,
                Status = ramUsageMb > 800 ? "Warning - RAM Alta" : "Healthy",
                Queues = new List<QueueDetailDto>
                {
                    new QueueDetailDto
                    {
                        QueueName = "evaluma-payloads (Kafka)",
                        MessageCount = pendingMessages, // Simplificado, Kafka lag requiere lógica de offsets
                        ActiveConsumers = activeWorkers,
                        Status = activeWorkers > 0 ? "Active" : "Disconnected"
                    },
                    new QueueDetailDto
                    {
                        QueueName = "Telemetría API",
                        MessageCount = (int)ramUsageMb, // Sobrecargamos esto para mandar MB
                        ActiveConsumers = 1,
                        Status = "RAM MB"
                    }
                }
            };

            await Task.CompletedTask;
            return responseDto;
        }
    }
}