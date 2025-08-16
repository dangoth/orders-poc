using RabbitMQ.Client;
using Shared.RabbitMQ;

namespace OrderService.Services
{
    public interface IRabbitMQInitializationService
    {
        Task InitializeAsync();
    }

    public class RabbitMQInitializationService : IRabbitMQInitializationService
    {
        private readonly IRabbitMQPublisher _publisher;

        public RabbitMQInitializationService(IRabbitMQPublisher publisher)
        {
            _publisher = publisher;
        }

        public async Task InitializeAsync()
        {
            await _publisher.InitializeAsync(RabbitMQConstants.OrdersExchange, ExchangeType.Direct);
            await _publisher.InitializeAsync(RabbitMQConstants.RestockingExchange, ExchangeType.Direct);
        }
    }
}