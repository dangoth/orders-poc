using RabbitMQ.Client;
using Shared.Models;
using Shared.RabbitMQ;
using System.Text.Json;

namespace OrderService.Services
{
    public interface IOrderService
    {
        Task InitializeAsync();
        Task CreateOrderAsync(OrderMessage order);
    }

    public class OrderService : IOrderService
    {
        private readonly IRabbitMQPublisher _publisher;
        private readonly ILogger<OrderService> _logger;

        public OrderService(IRabbitMQPublisher publisher, ILogger<OrderService> logger)
        {
            _publisher = publisher;
            _logger = logger;
        }

        public async Task InitializeAsync()
        {
            await _publisher.InitializeAsync(
                RabbitMQConstants.OrdersExchange,
                ExchangeType.Direct);
        }

        public async Task CreateOrderAsync(OrderMessage order)
        {
            var message = JsonSerializer.Serialize(order);
            await _publisher.PublishAsync(
                RabbitMQConstants.OrdersExchange,
                RabbitMQConstants.OrdersRoutingKey,
                message);
            
            _logger.LogInformation("Order created and published: {OrderId}", order.OrderId);
        }
    }
}
