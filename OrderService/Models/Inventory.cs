using System.ComponentModel.DataAnnotations;

namespace OrderService.Models
{
    public class Product
    {
        [Key]
        public string ProductId { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
        
        public virtual ICollection<InventoryItem> InventoryItems { get; set; } = new List<InventoryItem>();
    }

    public class InventoryItem
    {
        [Key]
        public Guid Id { get; set; } = Guid.NewGuid();
        public string ProductId { get; set; } = string.Empty;
        public int QuantityAvailable { get; set; }
        public int QuantityReserved { get; set; }
        public int ReorderLevel { get; set; } = 10;
        public DateTime LastUpdated { get; set; } = DateTime.UtcNow;
        
        public virtual Product Product { get; set; } = null!;
        
        public int QuantityOnHand => QuantityAvailable + QuantityReserved;
    }

    public class InventoryReservation
    {
        [Key]
        public Guid Id { get; set; } = Guid.NewGuid();
        public string OrderId { get; set; } = string.Empty;
        public string ProductId { get; set; } = string.Empty;
        public int QuantityReserved { get; set; }
        public DateTime ReservedAt { get; set; } = DateTime.UtcNow;
        public DateTime? ReleasedAt { get; set; }
        public InventoryReservationStatus Status { get; set; } = InventoryReservationStatus.Active;
        public string? Reason { get; set; }
        
        public virtual Product Product { get; set; } = null!;
    }

    public enum InventoryReservationStatus
    {
        Active,
        Released,
        Fulfilled,
        Expired
    }
}