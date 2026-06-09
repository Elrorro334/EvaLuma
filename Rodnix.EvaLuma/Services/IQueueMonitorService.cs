using Rodnix.EvaLuma.DTOs;

namespace Rodnix.EvaLuma.Services
{
    public interface IQueueMonitorService
    {
        Task<QueueStatusDto> GetCurrentQueueStatusAsync();
    }
}