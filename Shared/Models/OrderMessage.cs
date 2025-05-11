using System;

namespace Shared.Models
{
    public class OrderMessage : Message
    {
        public int OrderId { get; set; }
        public string CustomerName { get; set; } = string.Empty;
        public decimal TotalAmount { get; set; }
        public string[] ProductIds { get; set; } = Array.Empty<string>();
        public OrderStatus Status { get; set; } = OrderStatus.Created;
    }

    public enum OrderStatus
    {
        Created,
        Processing,
        Fulfilled,
        Cancelled
    }
}