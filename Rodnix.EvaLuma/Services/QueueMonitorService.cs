using Rodnix.EvaLuma.DTOs;
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
          
            var random = new Random();
            var pendingMessages = random.Next(0, 50);
            var activeWorkers = random.Next(1, 5);

            var responseDto = new QueueStatusDto
            {
                LastUpdated = DateTime.UtcNow,
                Status = pendingMessages > 40 ? "Warning - Alta Carga" : "Healthy",
                Queues = new List<QueueDetailDto>
                {
                    new QueueDetailDto
                    {
                        QueueName = "evaluaciones-pendientes",
                        MessageCount = pendingMessages,
                        ActiveConsumers = activeWorkers,
                        Status = pendingMessages == 0 ? "Idle" : "Active"
                    },
                    new QueueDetailDto
                    {
                        QueueName = "auditoria-append-only",
                        MessageCount = random.Next(0, 5), 
                        ActiveConsumers = 4,
                        Status = "Active"
                    }
                }
            };

          
            await Task.Delay(100);

            return responseDto;
        }
    }
}