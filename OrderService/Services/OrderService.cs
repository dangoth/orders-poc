using RabbitMQ.Client;
using Shared.Models;
using Shared.RabbitMQ;
using System.Text.Json;
using OrderService.Persistence;
using OrderService.Models;
using Microsoft.EntityFrameworkCore;

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
        private readonly OrderDbContext _dbContext;

        public OrderService(IRabbitMQPublisher publisher, ILogger<OrderService> logger, OrderDbContext dbContext)
        {
            _publisher = publisher;
            _logger = logger;
            _dbContext = dbContext;
        }

        public async Task InitializeAsync()
        {
            await _publisher.InitializeAsync(
                RabbitMQConstants.OrdersExchange,
                ExchangeType.Direct);
        }

        public async Task CreateOrderAsync(OrderMessage order)
        {
            var dbOrder = new Order
            {
                CustomerName = order.CustomerName,
                TotalAmount = order.TotalAmount,
                Status = (Models.OrderStatus)order.Status,
                CreatedAt = DateTime.UtcNow,
                Products = order.ProductIds.Select(productId => new OrderProduct
                {
                    ProductId = productId
                }).ToList()
            };

            _dbContext.Orders.Add(dbOrder);
            await _dbContext.SaveChangesAsync();

            var message = JsonSerializer.Serialize(order);
            await _publisher.PublishAsync(
                RabbitMQConstants.OrdersExchange,
                RabbitMQConstants.OrdersRoutingKey,
                message);
            
            _logger.LogInformation("Order created, saved to database, and published: {OrderId}", order.OrderId);
        }
    }
}
