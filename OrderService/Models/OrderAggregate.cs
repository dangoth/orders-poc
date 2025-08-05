using Shared.Models;
using System.Text.Json;

namespace OrderService.Models
{
    public class OrderAggregate
    {
        public string Id { get; private set; } = string.Empty;
        public string CustomerName { get; private set; } = string.Empty;
        public decimal TotalAmount { get; private set; }
        public Shared.Models.OrderStatus Status { get; private set; }
        public List<string> ProductIds { get; private set; }
        public DateTime CreatedAt { get; private set; }
        public long Version { get; private set; }
        
        private readonly List<DomainEvent> _uncommittedEvents = new();

        public OrderAggregate()
        {
            ProductIds = new List<string>();
            Status = Shared.Models.OrderStatus.Created;
        }

        public static OrderAggregate Create(string customerName, decimal totalAmount, string[] productIds)
        {
            var orderId = Guid.NewGuid().ToString();
            var aggregate = new OrderAggregate();
            
            aggregate.Apply(new OrderCreatedEvent(orderId, new OrderMessage
            {
                OrderId = int.Parse(orderId.Substring(0, 8), System.Globalization.NumberStyles.HexNumber),
                CustomerName = customerName,
                TotalAmount = totalAmount,
                ProductIds = productIds,
                Status = Shared.Models.OrderStatus.Created
            }));

            return aggregate;
        }

        public static OrderAggregate FromEvents(IEnumerable<DomainEvent> events)
        {
            var aggregate = new OrderAggregate();

            foreach (var @event in events.OrderBy(e => e.Version))
            {
                aggregate.ApplyHistoricalEvent(@event);
            }
            return aggregate;
        }

        public void StartProcessing()
        {
            if (Status != Shared.Models.OrderStatus.Created)
            {
                throw new InvalidOperationException($"Cannot start processing order in {Status} status");
            }

            Apply(new OrderProcessingStartedEvent(Id, new OrderMessage
            {
                OrderId = int.Parse(Id.Substring(0, 8), System.Globalization.NumberStyles.HexNumber),
                CustomerName = CustomerName,
                TotalAmount = TotalAmount,
                ProductIds = ProductIds.ToArray(),
                Status = Shared.Models.OrderStatus.Processing
            }));
        }

        public void Fulfill()
        {
            if (Status != Shared.Models.OrderStatus.Processing)
            {
                throw new InvalidOperationException($"Cannot fulfill order in {Status} status");
            }

            Apply(new OrderFulfilledEvent(Id, new OrderMessage
            {
                OrderId = int.Parse(Id.Substring(0, 8), System.Globalization.NumberStyles.HexNumber),
                CustomerName = CustomerName,
                TotalAmount = TotalAmount,
                ProductIds = ProductIds.ToArray(),
                Status = Shared.Models.OrderStatus.Fulfilled
            }));
        }

        public void Cancel(string reason)
        {
            if (Status == Shared.Models.OrderStatus.Fulfilled)
            {
                throw new InvalidOperationException("Cannot cancel a fulfilled order");
            }

            Apply(new OrderCancelledEvent(Id, new OrderMessage
            {
                OrderId = int.Parse(Id.Substring(0, 8), System.Globalization.NumberStyles.HexNumber),
                CustomerName = CustomerName,
                TotalAmount = TotalAmount,
                ProductIds = ProductIds.ToArray(),
                Status = Shared.Models.OrderStatus.Cancelled,
                Reason = reason
            }));
        }

        private void ApplyHistoricalEvent(DomainEvent @event)
        {
            switch (@event)
            {
                case OrderCreatedEvent created:
                    Id = created.AggregateId;
                    var orderData = JsonSerializer.Deserialize<OrderMessage>(created.Data);
                    if (orderData != null)
                    {
                        CustomerName = orderData.CustomerName;
                        TotalAmount = orderData.TotalAmount;
                        ProductIds = orderData.ProductIds.ToList();
                        Status = Shared.Models.OrderStatus.Created;
                        CreatedAt = DateTime.UtcNow;
                    }
                    break;
                case OrderProcessingStartedEvent processing:
                    Status = Shared.Models.OrderStatus.Processing;
                    break;
                case OrderFulfilledEvent fulfilled:
                    Status = Shared.Models.OrderStatus.Fulfilled;
                    break;
                case OrderCancelledEvent cancelled:
                    Status = Shared.Models.OrderStatus.Cancelled;
                    break;
            }
            Version++;
        }

        public void Apply(DomainEvent @event)
        {
            ApplyHistoricalEvent(@event);
            _uncommittedEvents.Add(@event);
        }

        public IEnumerable<DomainEvent> GetUncommittedEvents()
        {
            return _uncommittedEvents.ToList();
        }

        public void MarkEventsAsCommitted()
        {
            _uncommittedEvents.Clear();
        }
    }
} 