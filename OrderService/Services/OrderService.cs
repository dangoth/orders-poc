using RabbitMQ.Client;
using Shared.Models;
using Shared.RabbitMQ;
using System.Text.Json;
using OrderService.Models;
using OrderService.Repositories;

namespace OrderService.Services
{
    public interface IOrderService
    {
        Task InitializeAsync();
        Task<string> CreateOrderAsync(OrderMessage order);
        Task ProcessOrderAsync(string orderId);
        Task FulfillOrderAsync(string orderId);
        Task CancelOrderAsync(string orderId, string reason);
        Task<OrderAggregate> GetOrderAsync(string orderId);
    }

    public class OrderService : IOrderService
    {
        private readonly IRabbitMQPublisher _publisher;
        private readonly ILogger<OrderService> _logger;
        private readonly IOrderRepository _orderRepository;

        public OrderService(IRabbitMQPublisher publisher, ILogger<OrderService> logger, IOrderRepository orderRepository)
        {
            _publisher = publisher;
            _logger = logger;
            _orderRepository = orderRepository;
        }

        public async Task InitializeAsync()
        {
            await _publisher.InitializeAsync(
                RabbitMQConstants.OrdersExchange,
                ExchangeType.Direct);
        }

        public async Task<string> CreateOrderAsync(OrderMessage order)
        {
            var aggregate = OrderAggregate.Create(order.CustomerName, order.TotalAmount, order.ProductIds);
            
            // Get the events before saving (which clears them)
            var events = aggregate.GetUncommittedEvents().ToList();
            _logger.LogInformation("Uncommitted events count: {Count}", events.Count);
            
            if (!events.Any())
            {
                throw new InvalidOperationException("No uncommitted events found after creating aggregate");
            }
            
            await _orderRepository.SaveAsync(aggregate);

            // Publish the created event to RabbitMQ
            var createdEvent = events.First();
            var message = JsonSerializer.Serialize(createdEvent);
            await _publisher.PublishAsync(
                RabbitMQConstants.OrdersExchange,
                RabbitMQConstants.OrdersRoutingKey,
                message);
            
            _logger.LogInformation("Order created with event sourcing: {OrderId}", aggregate.Id);
            return aggregate.Id;
        }

        public async Task ProcessOrderAsync(string orderId)
        {
            var aggregate = await _orderRepository.GetByIdAsync(orderId);
            aggregate.StartProcessing();
            
            // Get the events before saving (which clears them)
            var events = aggregate.GetUncommittedEvents().ToList();
            
            await _orderRepository.SaveAsync(aggregate);

            // Publish the processing event
            var processingEvent = events.First();
            var message = JsonSerializer.Serialize(processingEvent);
            await _publisher.PublishAsync(
                RabbitMQConstants.OrdersExchange,
                RabbitMQConstants.OrdersRoutingKey,
                message);

            _logger.LogInformation("Order processing started: {OrderId}", orderId);
        }

        public async Task FulfillOrderAsync(string orderId)
        {
            var aggregate = await _orderRepository.GetByIdAsync(orderId);
            aggregate.Fulfill();
            
            // Get the events before saving (which clears them)
            var events = aggregate.GetUncommittedEvents().ToList();
            
            await _orderRepository.SaveAsync(aggregate);

            // Publish the fulfilled event
            var fulfilledEvent = events.First();
            var message = JsonSerializer.Serialize(fulfilledEvent);
            await _publisher.PublishAsync(
                RabbitMQConstants.OrdersExchange,
                RabbitMQConstants.OrdersRoutingKey,
                message);

            _logger.LogInformation("Order fulfilled: {OrderId}", orderId);
        }

        public async Task CancelOrderAsync(string orderId, string reason)
        {
            var aggregate = await _orderRepository.GetByIdAsync(orderId);
            aggregate.Cancel(reason);
            
            // Get the events before saving (which clears them)
            var events = aggregate.GetUncommittedEvents().ToList();
            
            await _orderRepository.SaveAsync(aggregate);

            // Publish the cancelled event
            var cancelledEvent = events.First();
            var message = JsonSerializer.Serialize(cancelledEvent);
            await _publisher.PublishAsync(
                RabbitMQConstants.OrdersExchange,
                RabbitMQConstants.OrdersRoutingKey,
                message);

            _logger.LogInformation("Order cancelled: {OrderId}, Reason: {Reason}", orderId, reason);
        }

        public async Task<OrderAggregate> GetOrderAsync(string orderId)
        {
            return await _orderRepository.GetByIdAsync(orderId);
        }
    }
}
