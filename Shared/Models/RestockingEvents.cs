using System.Text.Json;

namespace Shared.Models
{
    public class LowStockWarningEvent : DomainEvent
    {
        public LowStockWarningEvent() { }
        
        public LowStockWarningEvent(string productId, LowStockWarningData warningData)
        {
            AggregateId = productId;
            EventType = nameof(LowStockWarningEvent);
            Data = JsonSerializer.Serialize(warningData);
        }
    }

    public class RestockRequestEvent : DomainEvent
    {
        public RestockRequestEvent() { }
        
        public RestockRequestEvent(string productId, RestockRequestData requestData)
        {
            AggregateId = productId;
            EventType = nameof(RestockRequestEvent);
            Data = JsonSerializer.Serialize(requestData);
        }
    }

    public class LowStockWarningData
    {
        public string ProductId { get; set; } = string.Empty;
        public string ProductName { get; set; } = string.Empty;
        public int CurrentStock { get; set; }
        public int ReorderLevel { get; set; }
        public int RecommendedRestockQuantity { get; set; }
        public DateTime WarningTimestamp { get; set; } = DateTime.UtcNow;
        public string Reason { get; set; } = string.Empty;
    }

    public class RestockRequestData
    {
        public string ProductId { get; set; } = string.Empty;
        public string ProductName { get; set; } = string.Empty;
        public int RequestedQuantity { get; set; }
        public int CurrentStock { get; set; }
        public int ReorderLevel { get; set; }
        public DateTime RequestTimestamp { get; set; } = DateTime.UtcNow;
        public string Priority { get; set; } = "Normal";
        public string RequestedBy { get; set; } = "System";
    }
}