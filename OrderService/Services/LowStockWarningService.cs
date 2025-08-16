using Microsoft.EntityFrameworkCore;
using OrderService.Models;
using OrderService.Persistence;
using Shared.Models;
using Shared.Services;
using Shared.RabbitMQ;

namespace OrderService.Services
{
    public interface ILowStockWarningService
    {
        Task CheckAndWarnLowStockAsync(IEnumerable<string> productIds);
    }

    public class LowStockWarningService : ILowStockWarningService
    {
        private readonly OrderDbContext _dbContext;
        private readonly IEventPublishingHelper _eventPublisher;
        private readonly ILogger<LowStockWarningService> _logger;
        private const int LOW_STOCK_THRESHOLD = 5;

        public LowStockWarningService(
            OrderDbContext dbContext, 
            IEventPublishingHelper eventPublisher,
            ILogger<LowStockWarningService> logger)
        {
            _dbContext = dbContext;
            _eventPublisher = eventPublisher;
            _logger = logger;
        }

        public async Task CheckAndWarnLowStockAsync(IEnumerable<string> productIds)
        {
            var inventoryItems = await _dbContext.InventoryItems
                .Include(i => i.Product)
                .Where(i => productIds.Contains(i.ProductId))
                .ToListAsync();

            foreach (var item in inventoryItems)
            {
                if (item.QuantityAvailable <= LOW_STOCK_THRESHOLD)
                {
                    await PublishLowStockWarningAsync(item);
                    
                    if (item.QuantityAvailable <= item.ReorderLevel)
                    {
                        await PublishRestockRequestAsync(item);
                    }
                }
            }
        }

        private async Task PublishLowStockWarningAsync(InventoryItem item)
        {
            var warningData = new LowStockWarningData
            {
                ProductId = item.ProductId,
                ProductName = item.Product?.Name ?? "Unknown Product",
                CurrentStock = item.QuantityAvailable,
                ReorderLevel = item.ReorderLevel,
                RecommendedRestockQuantity = CalculateRestockQuantity(item),
                Reason = $"Stock level ({item.QuantityAvailable}) is at or below threshold ({LOW_STOCK_THRESHOLD})"
            };

            var warningEvent = new LowStockWarningEvent(item.ProductId, warningData);
            await _eventPublisher.PublishLowStockWarningAsync(warningEvent);

            _logger.LogWarning("Low stock warning published for product {ProductId}: {CurrentStock} units remaining", 
                item.ProductId, item.QuantityAvailable);
        }

        private async Task PublishRestockRequestAsync(InventoryItem item)
        {
            var requestData = new RestockRequestData
            {
                ProductId = item.ProductId,
                ProductName = item.Product?.Name ?? "Unknown Product",
                RequestedQuantity = CalculateRestockQuantity(item),
                CurrentStock = item.QuantityAvailable,
                ReorderLevel = item.ReorderLevel,
                Priority = item.QuantityAvailable == 0 ? "Critical" : "High"
            };

            var restockEvent = new RestockRequestEvent(item.ProductId, requestData);
            await _eventPublisher.PublishToRestockingExchangeAsync(restockEvent);

            _logger.LogWarning("Restock request published for product {ProductId}: requesting {RequestedQuantity} units", 
                item.ProductId, requestData.RequestedQuantity);
        }

        private static int CalculateRestockQuantity(InventoryItem item)
        {
            var targetStock = Math.Max(item.ReorderLevel * 3, 50);
            return targetStock - item.QuantityOnHand;
        }
    }
}