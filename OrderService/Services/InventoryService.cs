using Microsoft.EntityFrameworkCore;
using OrderService.Models;
using OrderService.Persistence;
using Shared.Models;

namespace OrderService.Services
{
    public interface IInventoryService
    {
        Task<InventoryCheckResult> CheckAndReserveInventoryAsync(string orderId, IEnumerable<OrderItem> items);
        Task ReleaseInventoryAsync(string orderId, string reason);
        Task FulfillInventoryAsync(string orderId);
        Task<bool> IsInventoryAvailableAsync(IEnumerable<string> productIds);
        Task<List<Product>> GetProductsAsync(IEnumerable<string> productIds);
        Task SeedInventoryDataAsync();
    }

    public class InventoryService : IInventoryService
    {
        private readonly OrderDbContext _dbContext;
        private readonly ILogger<InventoryService> _logger;
        private readonly ILowStockWarningService _lowStockWarningService;

        public InventoryService(OrderDbContext dbContext, ILogger<InventoryService> logger, ILowStockWarningService lowStockWarningService)
        {
            _dbContext = dbContext;
            _logger = logger;
            _lowStockWarningService = lowStockWarningService;
        }

        public async Task<InventoryCheckResult> CheckAndReserveInventoryAsync(string orderId, IEnumerable<OrderItem> items)
        {
            using var transaction = await _dbContext.Database.BeginTransactionAsync();
            
            try
            {
                var result = new InventoryCheckResult { OrderId = orderId };
                var itemsList = items.ToList();
                var productIds = itemsList.Select(i => i.ProductId).ToArray();
                
                var inventoryItems = await _dbContext.InventoryItems
                    .Include(i => i.Product)
                    .Where(i => productIds.Contains(i.ProductId))
                    .ToListAsync();

                var missingProducts = productIds.Except(inventoryItems.Select(i => i.ProductId)).ToList();
                if (missingProducts.Any())
                {
                    result.IsSuccessful = false;
                    result.Shortages.AddRange(missingProducts.Select(p => new InventoryShortageItem
                    {
                        ProductId = p,
                        QuantityRequested = itemsList.First(i => i.ProductId == p).Quantity,
                        QuantityAvailable = 0
                    }));
                }

                foreach (var orderItem in itemsList)
                {
                    var inventoryItem = inventoryItems.FirstOrDefault(i => i.ProductId == orderItem.ProductId);
                    if (inventoryItem == null) continue; // Already handled in missing products
                    
                    var quantityRequested = orderItem.Quantity;
                    
                    if (inventoryItem.QuantityAvailable >= quantityRequested)
                    {
                        inventoryItem.QuantityAvailable -= quantityRequested;
                        inventoryItem.QuantityReserved += quantityRequested;
                        inventoryItem.LastUpdated = DateTime.UtcNow;

                        var reservation = new InventoryReservation
                        {
                            OrderId = orderId,
                            ProductId = inventoryItem.ProductId,
                            QuantityReserved = quantityRequested,
                            Status = InventoryReservationStatus.Active
                        };

                        _dbContext.InventoryReservations.Add(reservation);

                        result.Reservations.Add(new InventoryReservationItem
                        {
                            ProductId = inventoryItem.ProductId,
                            QuantityRequested = quantityRequested,
                            QuantityReserved = quantityRequested,
                            ReservationId = reservation.Id
                        });
                    }
                    else
                    {
                        result.IsSuccessful = false;
                        result.Shortages.Add(new InventoryShortageItem
                        {
                            ProductId = inventoryItem.ProductId,
                            QuantityRequested = quantityRequested,
                            QuantityAvailable = inventoryItem.QuantityAvailable
                        });
                    }
                }

                if (result.IsSuccessful)
                {
                    await _dbContext.SaveChangesAsync();
                    await transaction.CommitAsync();
                    _logger.LogInformation("Inventory reserved successfully for order {OrderId}", orderId);
                    
                    await _lowStockWarningService.CheckAndWarnLowStockAsync(productIds);
                }
                else
                {
                    await transaction.RollbackAsync();
                    _logger.LogWarning("Insufficient inventory for order {OrderId}. Shortages: {Shortages}", 
                        orderId, string.Join(", ", result.Shortages.Select(s => $"{s.ProductId}: {s.Shortage}")));
                }

                return result;
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Error checking and reserving inventory for order {OrderId}", orderId);
                throw;
            }
        }

        public async Task ReleaseInventoryAsync(string orderId, string reason)
        {
            using var transaction = await _dbContext.Database.BeginTransactionAsync();
            
            try
            {
                var reservations = await _dbContext.InventoryReservations
                    .Where(r => r.OrderId == orderId && r.Status == InventoryReservationStatus.Active)
                    .ToListAsync();

                foreach (var reservation in reservations)
                {
                    var inventoryItem = await _dbContext.InventoryItems
                        .FirstOrDefaultAsync(i => i.ProductId == reservation.ProductId);

                    if (inventoryItem != null)
                    {
                        inventoryItem.QuantityAvailable += reservation.QuantityReserved;
                        inventoryItem.QuantityReserved -= reservation.QuantityReserved;
                        inventoryItem.LastUpdated = DateTime.UtcNow;

                        reservation.Status = InventoryReservationStatus.Released;
                        reservation.ReleasedAt = DateTime.UtcNow;
                        reservation.Reason = reason;
                    }
                }

                await _dbContext.SaveChangesAsync();
                await transaction.CommitAsync();
                
                _logger.LogInformation("Inventory released for order {OrderId}. Reason: {Reason}", orderId, reason);
                
                var affectedProductIds = reservations.Select(r => r.ProductId).Distinct();
                await _lowStockWarningService.CheckAndWarnLowStockAsync(affectedProductIds);
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Error releasing inventory for order {OrderId}", orderId);
                throw;
            }
        }

        public async Task FulfillInventoryAsync(string orderId)
        {
            var reservations = await _dbContext.InventoryReservations
                .Where(r => r.OrderId == orderId && r.Status == InventoryReservationStatus.Active)
                .ToListAsync();

            foreach (var reservation in reservations)
            {
                var inventoryItem = await _dbContext.InventoryItems
                    .FirstOrDefaultAsync(i => i.ProductId == reservation.ProductId);

                if (inventoryItem != null)
                {
                    inventoryItem.QuantityReserved -= reservation.QuantityReserved;
                    inventoryItem.LastUpdated = DateTime.UtcNow;

                    reservation.Status = InventoryReservationStatus.Fulfilled;
                    reservation.ReleasedAt = DateTime.UtcNow;
                }
            }

            await _dbContext.SaveChangesAsync();
            _logger.LogInformation("Inventory fulfilled for order {OrderId}", orderId);
        }

        public async Task<bool> IsInventoryAvailableAsync(IEnumerable<string> productIds)
        {
            var productIdsList = productIds.ToList();
            var inventoryItems = await _dbContext.InventoryItems
                .Where(i => productIdsList.Contains(i.ProductId))
                .ToListAsync();

            return productIdsList.All(productId => 
                inventoryItems.Any(i => i.ProductId == productId && i.QuantityAvailable > 0));
        }

        public async Task<List<Product>> GetProductsAsync(IEnumerable<string> productIds)
        {
            var productIdsList = productIds.ToList();
            return await _dbContext.Products
                .Where(p => productIdsList.Contains(p.ProductId))
                .ToListAsync();
        }

        public async Task SeedInventoryDataAsync()
        {
            if (await _dbContext.Products.AnyAsync())
            {
                return;
            }

            var products = new List<Product>
            {
                new() { ProductId = "LAPTOP001", Name = "Gaming Laptop", Description = "High-performance gaming laptop", Price = 1299.99m },
                new() { ProductId = "MOUSE001", Name = "Wireless Mouse", Description = "Ergonomic wireless mouse", Price = 29.99m },
                new() { ProductId = "KEYBOARD001", Name = "Mechanical Keyboard", Description = "RGB mechanical keyboard", Price = 89.99m },
                new() { ProductId = "MONITOR001", Name = "4K Monitor", Description = "27-inch 4K monitor", Price = 399.99m },
                new() { ProductId = "HEADSET001", Name = "Gaming Headset", Description = "Surround sound gaming headset", Price = 79.99m }
            };

            _dbContext.Products.AddRange(products);

            var inventoryItems = products.Select(p => new InventoryItem
            {
                ProductId = p.ProductId,
                QuantityAvailable = Random.Shared.Next(5, 50),
                QuantityReserved = 0,
                ReorderLevel = 10
            }).ToList();

            _dbContext.InventoryItems.AddRange(inventoryItems);
            await _dbContext.SaveChangesAsync();

            _logger.LogInformation("Inventory data seeded successfully");
        }
    }

    public class InventoryCheckResult
    {
        public string OrderId { get; set; } = string.Empty;
        public bool IsSuccessful { get; set; } = true;
        public List<InventoryReservationItem> Reservations { get; set; } = new();
        public List<InventoryShortageItem> Shortages { get; set; } = new();
    }
}