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
        Task ProcessPendingOrderAsync(string orderId);
        Task FulfillOrderAsync(string orderId);
        Task CancelOrderAsync(string orderId, string reason);
        Task<OrderAggregate> GetOrderAsync(string orderId);
    }

    public class OrderService : IOrderService
    {
        private readonly IRabbitMQPublisher _publisher;
        private readonly ILogger<OrderService> _logger;
        private readonly IOrderRepository _orderRepository;
        private readonly IInventoryService _inventoryService;

        public OrderService(IRabbitMQPublisher publisher, ILogger<OrderService> logger, IOrderRepository orderRepository, IInventoryService inventoryService)
        {
            _publisher = publisher;
            _logger = logger;
            _orderRepository = orderRepository;
            _inventoryService = inventoryService;
        }

        #region Private Helper Methods

        private async Task SaveAndPublishEventsAsync(OrderAggregate aggregate, string logMessage)
        {
            var events = aggregate.GetUncommittedEvents().ToList();
            if (!events.Any()) return;

            await _orderRepository.SaveAsync(aggregate);

            foreach (var @event in events)
            {
                await PublishEventAsync(@event);
            }

            _logger.LogInformation(logMessage, aggregate.Id);
        }

        private async Task PublishEventAsync(DomainEvent domainEvent)
        {
            var message = JsonSerializer.Serialize(domainEvent);
            await _publisher.PublishAsync(
                RabbitMQConstants.OrdersExchange,
                RabbitMQConstants.OrdersRoutingKey,
                message);
        }

        private async Task<bool> TryReserveInventoryAsync(OrderAggregate aggregate)
        {
            var inventoryResult = await _inventoryService.CheckAndReserveInventoryAsync(
                aggregate.Id, 
                aggregate.Items);

            if (inventoryResult.IsSuccessful)
            {
                aggregate.ReserveInventory(inventoryResult.Reservations);
                await SaveAndPublishEventsAsync(aggregate, "Inventory reserved for order: {0}");
                return true;
            }
            else
            {
                aggregate.MarkAsPending(inventoryResult.Shortages);
                await SaveAndPublishEventsAsync(aggregate, "Order marked as pending due to insufficient inventory: {0}");
                
                _logger.LogWarning("Insufficient inventory for order {OrderId}. Order marked as pending. Shortages: {Shortages}", 
                    aggregate.Id, string.Join(", ", inventoryResult.Shortages.Select(s => $"{s.ProductId}: {s.Shortage}")));
                return false;
            }
        }

        #endregion

        public async Task InitializeAsync()
        {
            await _publisher.InitializeAsync(
                RabbitMQConstants.OrdersExchange,
                ExchangeType.Direct);
        }

        public async Task<string> CreateOrderAsync(OrderMessage order)
        {
            if (order.Items == null || !order.Items.Any())
            {
                throw new ArgumentException("Order must contain at least one item");
            }

            if (order.Items.Any(item => item.Quantity <= 0))
            {
                throw new ArgumentException("All order items must have positive quantities");
            }

            var productIds = order.Items.Select(item => item.ProductId).Distinct().ToArray();
            var existingProducts = await _inventoryService.GetProductsAsync(productIds);
            var existingProductIds = existingProducts.Select(p => p.ProductId).ToHashSet();
            
            var missingProducts = productIds.Where(id => !existingProductIds.Contains(id)).ToList();
            if (missingProducts.Any())
            {
                throw new ArgumentException($"The following products do not exist: {string.Join(", ", missingProducts)}");
            }

            var aggregate = OrderAggregate.Create(order.CustomerName, order.TotalAmount, order.ProductIds, order.Items);
            
            var events = aggregate.GetUncommittedEvents().ToList();
            if (!events.Any())
            {
                throw new InvalidOperationException("No uncommitted events found after creating aggregate");
            }
            
            await SaveAndPublishEventsAsync(aggregate, "Order created: {0}");
            return aggregate.Id;
        }

        public async Task ProcessOrderAsync(string orderId)
        {
            var aggregate = await _orderRepository.GetByIdAsync(orderId);
            
            aggregate.RequestInventoryReservation();
            await SaveAndPublishEventsAsync(aggregate, "Inventory reservation requested for order: {0}");

            var inventoryReserved = await TryReserveInventoryAsync(aggregate);
            
            if (inventoryReserved)
            {
                aggregate.StartProcessing();
                await SaveAndPublishEventsAsync(aggregate, "Order processing started: {0}");
            }
        }

        public async Task ProcessPendingOrderAsync(string orderId)
        {
            var aggregate = await _orderRepository.GetByIdAsync(orderId);
            
            if (aggregate.Status != Shared.Models.OrderStatus.Pending)
            {
                throw new InvalidOperationException($"Cannot process pending order in {aggregate.Status} status");
            }

            aggregate.RequestInventoryReservation();
            await SaveAndPublishEventsAsync(aggregate, "Inventory reservation re-requested for pending order: {0}");

            var inventoryReserved = await TryReserveInventoryAsync(aggregate);
            
            if (inventoryReserved)
            {
                aggregate.StartProcessing();
                await SaveAndPublishEventsAsync(aggregate, "Pending order processing started: {0}");
            }
        }

        public async Task FulfillOrderAsync(string orderId)
        {
            var aggregate = await _orderRepository.GetByIdAsync(orderId);
            aggregate.Fulfill();
            
            await SaveAndPublishEventsAsync(aggregate, "Order fulfilled: {0}");
            await _inventoryService.FulfillInventoryAsync(orderId);
        }

        public async Task CancelOrderAsync(string orderId, string reason)
        {
            var aggregate = await _orderRepository.GetByIdAsync(orderId);
            aggregate.Cancel(reason);
            
            await SaveAndPublishEventsAsync(aggregate, "Order cancelled: {0}");
            await _inventoryService.ReleaseInventoryAsync(orderId, reason);
            
            _logger.LogInformation("Order cancelled: {OrderId}, Reason: {Reason}", orderId, reason);
        }

        public async Task<OrderAggregate> GetOrderAsync(string orderId)
        {
            return await _orderRepository.GetByIdAsync(orderId);
        }
    }
}
