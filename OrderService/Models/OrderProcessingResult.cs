using Shared.Models;

namespace OrderService.Models
{
    public class OrderProcessingResult
    {
        public bool IsSuccessful { get; set; }
        public OrderStatus Status { get; set; }
        public string Message { get; set; } = string.Empty;
        public List<InventoryShortageItem>? Shortages { get; set; }
    }
}