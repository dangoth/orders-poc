using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using Shared.Models;
using Shared.RabbitMQ;
using System.Text.Json;

namespace ProcessingService.Services
{
    public interface IProcessingService
    {
        Task InitializeAsync();
        Task ProcessOrderAsync(string message);
    }

    public class ProcessingService : IProcessingService
    {
        private readonly IRabbitMQPublisher _publisher;
        private readonly IRabbitMQConsumer _consumer;
        private readonly ILogger<ProcessingService> _logger;

        public ProcessingService(
            IRabbitMQPublisher publisher,
            IRabbitMQConsumer consumer,
            ILogger<ProcessingService> logger)
        {
            _publisher = publisher;
            _consumer = consumer;
            _logger = logger;
        }

        public async Task InitializeAsync()
        {
            await _publisher.InitializeAsync(
                RabbitMQConstants.ProcessedOrdersExchange,
                ExchangeType.Direct);

            await _consumer.InitializeAsync(
                RabbitMQConstants.OrdersQueue,
                RabbitMQConstants.OrdersExchange,
                RabbitMQConstants.OrdersRoutingKey);
            await _consumer.StartConsumingAsync(
                RabbitMQConstants.OrdersQueue,
                ProcessOrderAsync);
        }

        public async Task ProcessOrderAsync(string message)
        {
            var order = JsonSerializer.Deserialize<OrderMessage>(message);
            if (order == null)
            {
                throw new InvalidOperationException("Failed to deserialize order message");
            }

            order.Status = OrderStatus.Processing;
            await Task.Delay(1000);
            order.Status = OrderStatus.Fulfilled;

            var processedMessage = JsonSerializer.Serialize(order);
            await _publisher.PublishAsync(
                RabbitMQConstants.ProcessedOrdersExchange,
                RabbitMQConstants.ProcessedOrdersRoutingKey,
                processedMessage);

            _logger.LogInformation("Order processed: {OrderId}", order.OrderId);
        }
    }
}