using RabbitMQ.Client;
using Shared.Models;
using Shared.RabbitMQ;
using Shared.Services;
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
        private readonly IEventPublishingHelper _eventPublisher;
        private readonly IRabbitMQConsumer _consumer;
        private readonly ILogger<ProcessingService> _logger;

        public ProcessingService(
            IEventPublishingHelper eventPublisher,
            IRabbitMQConsumer consumer,
            ILogger<ProcessingService> logger)
        {
            _eventPublisher = eventPublisher;
            _consumer = consumer;
            _logger = logger;
        }

        public async Task InitializeAsync()
        {
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
            try
            {
                var domainEvent = TryDeserializeDomainEvent(message);
                if (domainEvent != null)
                {
                    await ProcessDomainEventAsync(domainEvent);
                    return;
                }

                var order = JsonSerializer.Deserialize<OrderMessage>(message);
                if (order == null)
                {
                    throw new InvalidOperationException("Failed to deserialize message");
                }

                order.Status = OrderStatus.Processing;
                await Task.Delay(1000);
                order.Status = OrderStatus.Fulfilled;

                var fulfilledEvent = new OrderFulfilledEvent(order.OrderId.ToString(), order);
                await _eventPublisher.PublishToProcessedOrdersExchangeAsync(fulfilledEvent);

                _logger.LogInformation("Order processed (legacy format): {OrderId}", order.OrderId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing order message: {Message}", message);
                throw;
            }
        }

        private async Task ProcessDomainEventAsync(DomainEvent domainEvent)
        {
            _logger.LogInformation("Processing domain event: {EventType} for aggregate {AggregateId}", 
                domainEvent.EventType, domainEvent.AggregateId);

            switch (domainEvent.EventType)
            {
                case nameof(OrderCreatedEvent):
                    _logger.LogInformation("Order created: {OrderId}. Waiting for inventory check...", domainEvent.AggregateId);
                    break;

                case nameof(OrderPendingEvent):
                    _logger.LogInformation("Order marked as pending due to insufficient inventory: {OrderId}", domainEvent.AggregateId);
                    break;

                case nameof(InventoryReservationRequestedEvent):
                    _logger.LogInformation("Inventory reservation requested for order: {OrderId}", domainEvent.AggregateId);
                    break;

                case nameof(InventoryReservedEvent):
                    _logger.LogInformation("Inventory reserved successfully for order: {OrderId}", domainEvent.AggregateId);
                    break;

                case nameof(InventoryInsufficientEvent):
                    _logger.LogWarning("Insufficient inventory for order: {OrderId}. Order cancelled.", domainEvent.AggregateId);
                    break;

                case nameof(OrderProcessingStartedEvent):
                    await Task.Delay(2000);
                    
                    var fulfilledEvent = new OrderFulfilledEvent(
                        domainEvent.AggregateId,
                        JsonSerializer.Deserialize<OrderMessage>(domainEvent.Data) ?? new OrderMessage());
                    
                    await _eventPublisher.PublishToProcessedOrdersExchangeAsync(fulfilledEvent);
                    break;

                case nameof(OrderFulfilledEvent):
                    _logger.LogInformation("Order fulfilled: {OrderId}", domainEvent.AggregateId);
                    break;

                case nameof(OrderCancelledEvent):
                    _logger.LogInformation("Order cancelled: {OrderId}", domainEvent.AggregateId);
                    break;

                case nameof(InventoryReleasedEvent):
                    _logger.LogInformation("Inventory released for order: {OrderId}", domainEvent.AggregateId);
                    break;

                case nameof(LowStockWarningEvent):
                    var warningData = JsonSerializer.Deserialize<LowStockWarningData>(domainEvent.Data);
                    _logger.LogWarning("Low stock warning for product {ProductId}: {CurrentStock} units remaining", 
                        warningData?.ProductId, warningData?.CurrentStock);
                    break;

                case nameof(RestockRequestEvent):
                    var requestData = JsonSerializer.Deserialize<RestockRequestData>(domainEvent.Data);
                    _logger.LogInformation("Restock request for product {ProductId}: {RequestedQuantity} units requested (Priority: {Priority})", 
                        requestData?.ProductId, requestData?.RequestedQuantity, requestData?.Priority);
                    break;

                default:
                    _logger.LogInformation("Unhandled event type: {EventType}", domainEvent.EventType);
                    break;
            }
        }


        private DomainEvent? TryDeserializeDomainEvent(string message)
        {
            try
            {
                using var document = JsonDocument.Parse(message);
                if (!document.RootElement.TryGetProperty("EventType", out var eventTypeElement))
                {
                    return null;
                }

                var eventType = eventTypeElement.GetString();
                if (string.IsNullOrEmpty(eventType))
                {
                    return null;
                }

                return eventType switch
                {
                    nameof(OrderCreatedEvent) => JsonSerializer.Deserialize<OrderCreatedEvent>(message),
                    nameof(OrderPendingEvent) => JsonSerializer.Deserialize<OrderPendingEvent>(message),
                    nameof(OrderProcessingStartedEvent) => JsonSerializer.Deserialize<OrderProcessingStartedEvent>(message),
                    nameof(OrderFulfilledEvent) => JsonSerializer.Deserialize<OrderFulfilledEvent>(message),
                    nameof(OrderCancelledEvent) => JsonSerializer.Deserialize<OrderCancelledEvent>(message),
                    nameof(InventoryReservationRequestedEvent) => JsonSerializer.Deserialize<InventoryReservationRequestedEvent>(message),
                    nameof(InventoryReservedEvent) => JsonSerializer.Deserialize<InventoryReservedEvent>(message),
                    nameof(InventoryInsufficientEvent) => JsonSerializer.Deserialize<InventoryInsufficientEvent>(message),
                    nameof(InventoryReleasedEvent) => JsonSerializer.Deserialize<InventoryReleasedEvent>(message),
                    nameof(LowStockWarningEvent) => JsonSerializer.Deserialize<LowStockWarningEvent>(message),
                    nameof(RestockRequestEvent) => JsonSerializer.Deserialize<RestockRequestEvent>(message),
                    _ => null
                };
            }
            catch (JsonException)
            {
                return null;
            }
        }

        private OrderMessage DeserializeOrderMessage(string data)
        {
            try
            {
                return JsonSerializer.Deserialize<OrderMessage>(data) ?? new OrderMessage();
            }
            catch (JsonException ex)
            {
                _logger.LogWarning(ex, "Failed to deserialize order message. Using default instance.");
                return new OrderMessage();
            }
        }
    }
}