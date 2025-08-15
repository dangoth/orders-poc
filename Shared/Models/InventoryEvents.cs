using System.Text.Json;

namespace Shared.Models
{
    // Inventory-related domain events
    public class InventoryReservationRequestedEvent : DomainEvent
    {
        public InventoryReservationRequestedEvent() { }
        
        public InventoryReservationRequestedEvent(string orderId, OrderMessage order)
        {
            AggregateId = orderId;
            EventType = nameof(InventoryReservationRequestedEvent);
            Data = JsonSerializer.Serialize(order);
        }
    }

    public class InventoryReservedEvent : DomainEvent
    {
        public InventoryReservedEvent() { }
        
        public InventoryReservedEvent(string orderId, OrderMessage order, List<InventoryReservationItem> reservations)
        {
            AggregateId = orderId;
            EventType = nameof(InventoryReservedEvent);
            Data = JsonSerializer.Serialize(new InventoryReservationData 
            { 
                Order = order, 
                Reservations = reservations 
            });
        }
    }

    public class InventoryInsufficientEvent : DomainEvent
    {
        public InventoryInsufficientEvent() { }
        
        public InventoryInsufficientEvent(string orderId, OrderMessage order, List<InventoryShortageItem> shortages)
        {
            AggregateId = orderId;
            EventType = nameof(InventoryInsufficientEvent);
            Data = JsonSerializer.Serialize(new InventoryShortageData 
            { 
                Order = order, 
                Shortages = shortages 
            });
        }
    }

    public class InventoryReleasedEvent : DomainEvent
    {
        public InventoryReleasedEvent() { }
        
        public InventoryReleasedEvent(string orderId, OrderMessage order, string reason)
        {
            AggregateId = orderId;
            EventType = nameof(InventoryReleasedEvent);
            Data = JsonSerializer.Serialize(new InventoryReleaseData 
            { 
                Order = order, 
                Reason = reason 
            });
        }
    }

    // Supporting data classes
    public class InventoryReservationData
    {
        public OrderMessage Order { get; set; } = new();
        public List<InventoryReservationItem> Reservations { get; set; } = new();
    }

    public class InventoryReservationItem
    {
        public string ProductId { get; set; } = string.Empty;
        public int QuantityRequested { get; set; }
        public int QuantityReserved { get; set; }
        public Guid ReservationId { get; set; }
    }

    public class InventoryShortageData
    {
        public OrderMessage Order { get; set; } = new();
        public List<InventoryShortageItem> Shortages { get; set; } = new();
    }

    public class InventoryShortageItem
    {
        public string ProductId { get; set; } = string.Empty;
        public int QuantityRequested { get; set; }
        public int QuantityAvailable { get; set; }
        public int Shortage => QuantityRequested - QuantityAvailable;
    }

    public class InventoryReleaseData
    {
        public OrderMessage Order { get; set; } = new();
        public string Reason { get; set; } = string.Empty;
    }
}