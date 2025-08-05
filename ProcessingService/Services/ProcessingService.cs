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
            try
            {
                // Try to deserialize as a domain event first
                var domainEvent = JsonSerializer.Deserialize<Shared.Models.DomainEvent>(message);
                if (domainEvent != null)
                {
                    await ProcessDomainEventAsync(domainEvent);
                    return;
                }

                // Fallback to old format
                var order = JsonSerializer.Deserialize<OrderMessage>(message);
                if (order == null)
                {
                    throw new InvalidOperationException("Failed to deserialize message");
                }

                order.Status = OrderStatus.Processing;
                await Task.Delay(1000);
                order.Status = OrderStatus.Fulfilled;

                var processedMessage = JsonSerializer.Serialize(order);
                await _publisher.PublishAsync(
                    RabbitMQConstants.ProcessedOrdersExchange,
                    RabbitMQConstants.ProcessedOrdersRoutingKey,
                    processedMessage);

                _logger.LogInformation("Order processed (legacy format): {OrderId}", order.OrderId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing order message: {Message}", message);
                throw;
            }
        }

        private async Task ProcessDomainEventAsync(Shared.Models.DomainEvent domainEvent)
        {
            _logger.LogInformation("Processing domain event: {EventType} for aggregate {AggregateId}", 
                domainEvent.EventType, domainEvent.AggregateId);

            switch (domainEvent.EventType)
            {
                case nameof(Shared.Models.OrderCreatedEvent):
                    // Order was created, start processing
                    await Task.Delay(1000); // Simulate processing time
                    
                    // Publish processing started event
                    var processingEvent = new Shared.Models.OrderProcessingStartedEvent(
                        domainEvent.AggregateId, 
                        JsonSerializer.Deserialize<OrderMessage>(domainEvent.Data) ?? new OrderMessage());
                    
                    var processingMessage = JsonSerializer.Serialize(processingEvent);
                    await _publisher.PublishAsync(
                        RabbitMQConstants.ProcessedOrdersExchange,
                        RabbitMQConstants.ProcessedOrdersRoutingKey,
                        processingMessage);
                    break;

                case nameof(Shared.Models.OrderProcessingStartedEvent):
                    // Order processing started, simulate fulfillment
                    await Task.Delay(2000); // Simulate fulfillment time
                    
                    // Publish fulfilled event
                    var fulfilledEvent = new Shared.Models.OrderFulfilledEvent(
                        domainEvent.AggregateId,
                        JsonSerializer.Deserialize<OrderMessage>(domainEvent.Data) ?? new OrderMessage());
                    
                    var fulfilledMessage = JsonSerializer.Serialize(fulfilledEvent);
                    await _publisher.PublishAsync(
                        RabbitMQConstants.ProcessedOrdersExchange,
                        RabbitMQConstants.ProcessedOrdersRoutingKey,
                        fulfilledMessage);
                    break;

                default:
                    _logger.LogInformation("Unhandled event type: {EventType}", domainEvent.EventType);
                    break;
            }
        }
    }
}