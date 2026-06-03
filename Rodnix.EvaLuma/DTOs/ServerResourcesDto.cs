using System.ComponentModel.DataAnnotations;

namespace Rodnix.EvaLuma.DTOs
{
    public class ServerResourcesDto
    {
        [Required(ErrorMessage = "El tiempo del servidor es requerido")]
        public DateTime ServerTime { get; set; }

        [Required]
        public string MachineName { get; set; } = null!;

        [Required]
        public string OS { get; set; } = null!;

        [Required]
        public int ProcessorCount { get; set; }

        [Required]
        public string MemoryUsage { get; set; } = null!;

        [Required]
        public int ProcessId { get; set; }

        [Required]
        public double TotalCpuTime { get; set; }
    }

    public class QueueStatusDto
    {
        [Required]
        public DateTime LastUpdated { get; set; }

        [Required]
        public string Status { get; set; } = null!;

        [Required]
        public List<QueueDetailDto> Queues { get; set; } = new List<QueueDetailDto>();
    }

    public class QueueDetailDto
    {
        [Required]
        public string QueueName { get; set; } = null!;

        [Required]
        public int MessageCount { get; set; }

        [Required]
        public int ActiveConsumers { get; set; }

        [Required]
        public string Status { get; set; } = null!;
    }
}