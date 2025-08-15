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
        public List<OrderItem> Items { get; private set; }
        public DateTime CreatedAt { get; private set; }
        public long Version { get; private set; }
        
        private readonly List<DomainEvent> _uncommittedEvents = new();

        public OrderAggregate()
        {
            ProductIds = new List<string>();
            Items = new List<OrderItem>();
            Status = Shared.Models.OrderStatus.Created;
        }

        public static OrderAggregate Create(string customerName, decimal totalAmount, string[] productIds, List<OrderItem> items)
        {
            var orderId = Guid.NewGuid().ToString();
            var aggregate = new OrderAggregate();
            
            aggregate.Id = orderId;
            aggregate.CustomerName = customerName;
            aggregate.TotalAmount = totalAmount;
            aggregate.ProductIds = productIds.ToList();
            aggregate.Items = items ?? new List<OrderItem>();
            aggregate.Status = Shared.Models.OrderStatus.Created;
            aggregate.CreatedAt = DateTime.UtcNow;
            
            aggregate.Apply(new OrderCreatedEvent(orderId, OrderMessageFactory.CreateFromAggregate(aggregate)));

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
            if (Status != Shared.Models.OrderStatus.Created && Status != Shared.Models.OrderStatus.Pending)
            {
                throw new InvalidOperationException($"Cannot start processing order in {Status} status");
            }

            Apply(new OrderProcessingStartedEvent(Id, OrderMessageFactory.CreateFromAggregate(this, Shared.Models.OrderStatus.Processing)));
        }

        public void Fulfill()
        {
            if (Status != Shared.Models.OrderStatus.Processing)
            {
                throw new InvalidOperationException($"Cannot fulfill order in {Status} status");
            }

            Apply(new OrderFulfilledEvent(Id, OrderMessageFactory.CreateFromAggregate(this, Shared.Models.OrderStatus.Fulfilled)));
        }

        public void Cancel(string reason)
        {
            if (Status == Shared.Models.OrderStatus.Fulfilled)
            {
                throw new InvalidOperationException("Cannot cancel a fulfilled order");
            }

            Apply(new OrderCancelledEvent(Id, OrderMessageFactory.CreateFromAggregate(this, Shared.Models.OrderStatus.Cancelled, reason)));
        }

        public void MarkAsPending(List<InventoryShortageItem> shortages)
        {
            if (Status != Shared.Models.OrderStatus.Created)
            {
                throw new InvalidOperationException($"Cannot mark order as pending from {Status} status");
            }

            Apply(new OrderPendingEvent(Id, OrderMessageFactory.CreateFromAggregate(this, Shared.Models.OrderStatus.Pending)));
        }

        public void RequestInventoryReservation()
        {
            if (Status != Shared.Models.OrderStatus.Created && Status != Shared.Models.OrderStatus.Pending)
            {
                throw new InvalidOperationException($"Cannot request inventory for order in {Status} status");
            }

            Apply(new InventoryReservationRequestedEvent(Id, OrderMessageFactory.CreateFromAggregate(this)));
        }

        public void ReserveInventory(List<InventoryReservationItem> reservations)
        {
            if (Status != Shared.Models.OrderStatus.Created && Status != Shared.Models.OrderStatus.Pending)
            {
                throw new InvalidOperationException($"Cannot reserve inventory for order in {Status} status");
            }

            Apply(new InventoryReservedEvent(Id, OrderMessageFactory.CreateFromAggregate(this), reservations));
        }

        public void MarkInventoryInsufficient(List<InventoryShortageItem> shortages)
        {
            if (Status != Shared.Models.OrderStatus.Created)
            {
                throw new InvalidOperationException($"Cannot mark inventory insufficient for order in {Status} status");
            }

            Apply(new InventoryInsufficientEvent(Id, OrderMessageFactory.CreateFromAggregate(this, Shared.Models.OrderStatus.Cancelled), shortages));
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
                        Items = orderData.Items ?? new List<OrderItem>();
                        Status = Shared.Models.OrderStatus.Created;
                        CreatedAt = DateTime.UtcNow;
                    }
                    break;
                case InventoryReservationRequestedEvent inventoryRequested:
                    // No state change, just tracking the event
                    break;
                case InventoryReservedEvent inventoryReserved:
                    // Inventory successfully reserved, ready for processing
                    break;
                case InventoryInsufficientEvent inventoryInsufficient:
                    Status = Shared.Models.OrderStatus.Cancelled;
                    break;
                case OrderPendingEvent pending:
                    Status = Shared.Models.OrderStatus.Pending;
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
                case InventoryReleasedEvent inventoryReleased:
                    // Inventory released, no status change needed as order is already cancelled/failed
                    break;
            }
            Version = @event.Version;
        }

        public void Apply(DomainEvent @event)
        {
            Version++;
            @event.Version = Version;
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