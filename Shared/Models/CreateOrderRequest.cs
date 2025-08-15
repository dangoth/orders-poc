using System.ComponentModel.DataAnnotations;

namespace Shared.Models
{
    public class CreateOrderRequest
    {
        [Required]
        [StringLength(100, MinimumLength = 1)]
        public string CustomerName { get; set; } = string.Empty;
        
        [Required]
        [MinLength(1, ErrorMessage = "At least one product must be selected")]
        public List<OrderItem> Items { get; set; } = new();
        
        public decimal CalculateTotalAmount()
        {
            return Items.Sum(item => item.TotalPrice);
        }
        
        public string[] GetProductIds()
        {
            return Items.Select(item => item.ProductId).ToArray();
        }
        
        public Dictionary<string, int> GetProductQuantities()
        {
            return Items.ToDictionary(item => item.ProductId, item => item.Quantity);
        }
    }
}