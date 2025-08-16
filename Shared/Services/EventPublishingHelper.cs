using Shared.Models;
using Shared.RabbitMQ;
using System.Text.Json;

namespace Shared.Services
{
    public interface IEventPublishingHelper
    {
        Task PublishDomainEventAsync(DomainEvent domainEvent, string exchange, string routingKey);
        Task PublishToOrdersExchangeAsync(DomainEvent domainEvent);
        Task PublishToProcessedOrdersExchangeAsync(DomainEvent domainEvent);
        Task PublishToRestockingExchangeAsync(DomainEvent domainEvent);
        Task PublishLowStockWarningAsync(DomainEvent domainEvent);
    }

    public class EventPublishingHelper : IEventPublishingHelper
    {
        private readonly IRabbitMQPublisher _publisher;

        public EventPublishingHelper(IRabbitMQPublisher publisher)
        {
            _publisher = publisher;
        }

        public async Task PublishDomainEventAsync(DomainEvent domainEvent, string exchange, string routingKey)
        {
            var message = JsonSerializer.Serialize(domainEvent);
            await _publisher.PublishAsync(exchange, routingKey, message);
        }

        public async Task PublishToOrdersExchangeAsync(DomainEvent domainEvent)
        {
            await PublishDomainEventAsync(domainEvent, RabbitMQConstants.OrdersExchange, RabbitMQConstants.OrdersRoutingKey);
        }

        public async Task PublishToProcessedOrdersExchangeAsync(DomainEvent domainEvent)
        {
            await PublishDomainEventAsync(domainEvent, RabbitMQConstants.ProcessedOrdersExchange, RabbitMQConstants.ProcessedOrdersRoutingKey);
        }

        public async Task PublishToRestockingExchangeAsync(DomainEvent domainEvent)
        {
            await PublishDomainEventAsync(domainEvent, RabbitMQConstants.RestockingExchange, RabbitMQConstants.RestockingRoutingKey);
        }

        public async Task PublishLowStockWarningAsync(DomainEvent domainEvent)
        {
            await PublishDomainEventAsync(domainEvent, RabbitMQConstants.RestockingExchange, RabbitMQConstants.LowStockWarningRoutingKey);
        }
    }
}