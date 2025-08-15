using System;

namespace Shared.Models
{
    public class OrderMessage : Message
    {
        public int OrderId { get; set; }
        public string CustomerName { get; set; } = string.Empty;
        public decimal TotalAmount { get; set; }
        public string[] ProductIds { get; set; } = Array.Empty<string>();
        public List<OrderItem> Items { get; set; } = new();
        public OrderStatus Status { get; set; } = OrderStatus.Created;
        public string? Reason { get; set; }
    }

    public enum OrderStatus
    {
        Created,
        Pending,
        Processing,
        Fulfilled,
        Cancelled
    }
}