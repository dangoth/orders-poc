using Microsoft.EntityFrameworkCore;
using OrderService.Persistence;
using OrderService.Models;
using Shared.Models;
using System.Text.Json;

namespace OrderService.Services
{
    public interface IEventStoreService
    {
        Task SaveEventsAsync(string aggregateId, IEnumerable<DomainEvent> events, long expectedVersion);
        Task<IEnumerable<DomainEvent>> GetEventsAsync(string aggregateId, long fromVersion = 0);
        Task<long> GetCurrentVersionAsync(string aggregateId);
    }

    public class EventStoreService : IEventStoreService
    {
        private readonly OrderDbContext _dbContext;
        private readonly ILogger<EventStoreService> _logger;

        public EventStoreService(OrderDbContext dbContext, ILogger<EventStoreService> logger)
        {
            _dbContext = dbContext;
            _logger = logger;
        }

        public async Task SaveEventsAsync(string aggregateId, IEnumerable<DomainEvent> events, long expectedVersion)
        {
            using var transaction = await _dbContext.Database.BeginTransactionAsync();
            
            try
            {
                var eventStream = await _dbContext.EventStreams
                    .FirstOrDefaultAsync(es => es.AggregateId == aggregateId);

                if (eventStream == null)
                {
                    eventStream = new EventStream
                    {
                        AggregateId = aggregateId,
                        CurrentVersion = 0,
                        LastModified = DateTime.UtcNow
                    };
                    _dbContext.EventStreams.Add(eventStream);
                }

                if (eventStream.CurrentVersion != expectedVersion)
                {
                    throw new InvalidOperationException($"Concurrency conflict: expected version {expectedVersion}, but current version is {eventStream.CurrentVersion}");
                }

                var eventEntities = events.Select((e, index) => new EventStore
                {
                    Id = e.Id,
                    AggregateId = e.AggregateId,
                    EventType = e.EventType,
                    Timestamp = e.Timestamp,
                    Version = expectedVersion + index + 1,
                    Data = e.Data,
                    CorrelationId = e.CorrelationId,
                    CausationId = e.CausationId
                }).ToList();

                _dbContext.Events.AddRange(eventEntities);
                eventStream.CurrentVersion = expectedVersion + eventEntities.Count;
                eventStream.LastModified = DateTime.UtcNow;

                await _dbContext.SaveChangesAsync();
                await transaction.CommitAsync();

                _logger.LogInformation("Saved {EventCount} events for aggregate {AggregateId}", eventEntities.Count, aggregateId);
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        public async Task<IEnumerable<DomainEvent>> GetEventsAsync(string aggregateId, long fromVersion = 0)
        {
            var events = await _dbContext.Events
                .Where(e => e.AggregateId == aggregateId && e.Version > fromVersion)
                .OrderBy(e => e.Version)
                .ToListAsync();

            return events.Select(ReconstructEvent);
        }

        private DomainEvent ReconstructEvent(EventStore eventStore) =>
            eventStore.EventType switch
            {
                nameof(OrderCreatedEvent) => CreateEvent(
                    new OrderCreatedEvent(eventStore.AggregateId,
                        JsonSerializer.Deserialize<OrderMessage>(eventStore.Data) ?? new OrderMessage()),
                    eventStore),

                nameof(OrderProcessingStartedEvent) => CreateEvent(
                    new OrderProcessingStartedEvent(eventStore.AggregateId,
                        JsonSerializer.Deserialize<OrderMessage>(eventStore.Data) ?? new OrderMessage()),
                    eventStore),

                nameof(OrderFulfilledEvent) => CreateEvent(
                    new OrderFulfilledEvent(eventStore.AggregateId,
                        JsonSerializer.Deserialize<OrderMessage>(eventStore.Data) ?? new OrderMessage()),
                    eventStore),

                nameof(OrderCancelledEvent) => CreateEvent(
                    new OrderCancelledEvent(eventStore.AggregateId,
                        JsonSerializer.Deserialize<OrderMessage>(eventStore.Data) ?? new OrderMessage()),
                    eventStore),

                nameof(InventoryReservationRequestedEvent) => CreateEvent(
                    new InventoryReservationRequestedEvent(eventStore.AggregateId,
                        JsonSerializer.Deserialize<OrderMessage>(eventStore.Data) ?? new OrderMessage()),
                    eventStore),

                nameof(InventoryReservedEvent) => CreateEvent(
                    new InventoryReservedEvent(eventStore.AggregateId,
                        JsonSerializer.Deserialize<InventoryReservationData>(eventStore.Data)?.Order ?? new OrderMessage(),
                        JsonSerializer.Deserialize<InventoryReservationData>(eventStore.Data)?.Reservations ?? new List<InventoryReservationItem>()),
                    eventStore),

                nameof(InventoryInsufficientEvent) => CreateEvent(
                    new InventoryInsufficientEvent(eventStore.AggregateId,
                        JsonSerializer.Deserialize<InventoryShortageData>(eventStore.Data)?.Order ?? new OrderMessage(),
                        JsonSerializer.Deserialize<InventoryShortageData>(eventStore.Data)?.Shortages ?? new List<InventoryShortageItem>()),
                    eventStore),

                nameof(InventoryReleasedEvent) => CreateEvent(
                    new InventoryReleasedEvent(eventStore.AggregateId,
                        JsonSerializer.Deserialize<InventoryReleaseData>(eventStore.Data)?.Order ?? new OrderMessage(),
                        JsonSerializer.Deserialize<InventoryReleaseData>(eventStore.Data)?.Reason ?? "Unknown"),
                    eventStore),

                _ => throw new InvalidOperationException($"Unknown event type: {eventStore.EventType}")
        };

        private DomainEvent CreateEvent(DomainEvent domainEvent, EventStore store)
        {
            domainEvent.Timestamp = store.Timestamp;
            domainEvent.Version = store.Version;
            domainEvent.CorrelationId = store.CorrelationId;
            domainEvent.CausationId = store.CausationId;
            return domainEvent;
        }

        public async Task<long> GetCurrentVersionAsync(string aggregateId)
        {
            var eventStream = await _dbContext.EventStreams
                .FirstOrDefaultAsync(es => es.AggregateId == aggregateId);

            return eventStream?.CurrentVersion ?? 0;
        }
    }
} 