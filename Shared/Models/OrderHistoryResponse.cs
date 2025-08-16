namespace Shared.Models
{
    public class OrderHistoryResponse
    {
        public string OrderId { get; set; } = string.Empty;
        public string CustomerName { get; set; } = string.Empty;
        public decimal TotalAmount { get; set; }
        public OrderStatus CurrentStatus { get; set; }
        public DateTime CreatedAt { get; set; }
        public List<OrderHistoryEvent> Events { get; set; } = new();
    }

    public class OrderHistoryEvent
    {
        public string EventType { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; }
        public long Version { get; set; }
        public OrderStatus? StatusAfterEvent { get; set; }
        public Dictionary<string, object> AdditionalData { get; set; } = new();
    }
}