using System.Text.Json;

namespace Shared.Models
{
    public abstract class DomainEvent
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string AggregateId { get; set; } = string.Empty;
        public string EventType { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
        public long Version { get; set; }
        public string Data { get; set; } = string.Empty;
        public string? CorrelationId { get; set; }
        public string? CausationId { get; set; }
        
        public string Serialize()
        {
            return JsonSerializer.Serialize(this);
        }
    }

    public class OrderCreatedEvent : DomainEvent
    {
        public OrderCreatedEvent(string orderId, OrderMessage order)
        {
            AggregateId = orderId;
            EventType = nameof(OrderCreatedEvent);
            Data = JsonSerializer.Serialize(order);
        }
    }

    public class OrderProcessingStartedEvent : DomainEvent
    {
        public OrderProcessingStartedEvent(string orderId, OrderMessage order)
        {
            AggregateId = orderId;
            EventType = nameof(OrderProcessingStartedEvent);
            Data = JsonSerializer.Serialize(order);
        }
    }

    public class OrderFulfilledEvent : DomainEvent
    {
        public OrderFulfilledEvent(string orderId, OrderMessage order)
        {
            AggregateId = orderId;
            EventType = nameof(OrderFulfilledEvent);
            Data = JsonSerializer.Serialize(order);
        }
    }

    public class OrderCancelledEvent : DomainEvent
    {
        public OrderCancelledEvent(string orderId, OrderMessage order)
        {
            AggregateId = orderId;
            EventType = nameof(OrderCancelledEvent);
            Data = JsonSerializer.Serialize(order);
        }
    }
} 