using Confluent.Kafka;
using System.Text.Json;
using Rodnix.EvaLuma.DTOs;
using System.Threading.Tasks;
using System;

namespace Rodnix.EvaLuma.Services
{
    public interface IKafkaProducerService
    {
        Task ProduceAsync(ProgresoPayload payload);
    }

    public class KafkaProducerService : IKafkaProducerService, IDisposable
    {
        private readonly IProducer<Null, string> _producer;
        private readonly string _topic = "evaluma-payloads";

        public KafkaProducerService()
        {
            var config = new ProducerConfig
            {
                BootstrapServers = "localhost:9092",
                Acks = Acks.All // Garantizar inmutabilidad y resiliencia en Kafka
            };
            _producer = new ProducerBuilder<Null, string>(config).Build();
        }

        public async Task ProduceAsync(ProgresoPayload payload)
        {
            var message = JsonSerializer.Serialize(payload);
            await _producer.ProduceAsync(_topic, new Message<Null, string> { Value = message });
        }

        public void Dispose()
        {
            _producer?.Flush(TimeSpan.FromSeconds(10));
            _producer?.Dispose();
        }
    }
}
