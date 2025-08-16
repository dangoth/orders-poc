using OrderService.Models;
using OrderService.Services;
using Shared.Models;

namespace OrderService.Repositories
{
    public interface IOrderRepository
    {
        Task<OrderAggregate> GetByIdAsync(string orderId);
        Task SaveAsync(OrderAggregate order);
        Task<bool> ExistsAsync(string orderId);
        Task<IEnumerable<DomainEvent>> GetEventsAsync(string orderId);
    }

    public class OrderRepository : IOrderRepository
    {
        private readonly IEventStoreService _eventStore;
        private readonly ILogger<OrderRepository> _logger;

        public OrderRepository(IEventStoreService eventStore, ILogger<OrderRepository> logger)
        {
            _eventStore = eventStore;
            _logger = logger;
        }

        public async Task<OrderAggregate> GetByIdAsync(string orderId)
        {
            var events = await _eventStore.GetEventsAsync(orderId);
            
            if (!events.Any())
            {
                throw new InvalidOperationException($"Order with id {orderId} not found");
            }

            return OrderAggregate.FromEvents(events);
        }

        public async Task SaveAsync(OrderAggregate order)
        {
            var uncommittedEvents = order.GetUncommittedEvents().ToList();
            
            if (!uncommittedEvents.Any())
            {
                return; // No changes to save
            }

            var currentVersion = await _eventStore.GetCurrentVersionAsync(order.Id);
            var expectedVersion = currentVersion;

            await _eventStore.SaveEventsAsync(order.Id, uncommittedEvents, expectedVersion);
            
            order.MarkEventsAsCommitted();
            
            _logger.LogInformation("Saved {EventCount} events for order {OrderId}", uncommittedEvents.Count, order.Id);
        }

        public async Task<bool> ExistsAsync(string orderId)
        {
            var currentVersion = await _eventStore.GetCurrentVersionAsync(orderId);
            return currentVersion > 0;
        }

        public async Task<IEnumerable<DomainEvent>> GetEventsAsync(string orderId)
        {
            return await _eventStore.GetEventsAsync(orderId);
        }
    }
} 