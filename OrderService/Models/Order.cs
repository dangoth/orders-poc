using System;
using System.Collections.Generic;

namespace OrderService.Models
{
    public class Order
    {
        public int Id { get; set; }
        public string CustomerName { get; set; }
        public decimal TotalAmount { get; set; }
        public OrderStatus Status { get; set; }
        public List<OrderProduct> Products { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class OrderProduct
    {
        public int Id { get; set; }
        public string ProductId { get; set; }
        public int OrderId { get; set; }
        public Order Order { get; set; }
    }

    public enum OrderStatus
    {
        Created,
        Processing,
        Fulfilled,
        Cancelled
    }
}