using OrderService.Models;
using OrderService.Repositories;
using Shared.Models;
using Shared.Services;

namespace OrderService.Services
{
    public interface IOrderService
    {
        Task InitializeAsync();
        Task<string> CreateOrderAsync(OrderMessage order);
        Task<OrderProcessingResult> ProcessOrderAsync(string orderId);
        Task ProcessPendingOrderAsync(string orderId);
        Task FulfillOrderAsync(string orderId);
        Task CancelOrderAsync(string orderId, string reason);
        Task<OrderAggregate> GetOrderAsync(string orderId);
        Task<OrderHistoryResponse> GetOrderHistoryAsync(string orderId);
    }

    public class OrderService : IOrderService
    {
        private readonly IEventPublishingHelper _eventPublisher;
        private readonly ILogger<OrderService> _logger;
        private readonly IOrderRepository _orderRepository;
        private readonly IInventoryService _inventoryService;

        public OrderService(IEventPublishingHelper eventPublisher, ILogger<OrderService> logger, IOrderRepository orderRepository, IInventoryService inventoryService)
        {
            _eventPublisher = eventPublisher;
            _logger = logger;
            _orderRepository = orderRepository;
            _inventoryService = inventoryService;
        }

        #region Private Helper Methods

        private async Task SaveAndPublishEventsAsync(OrderAggregate aggregate, string logMessage)
        {
            var events = aggregate.GetUncommittedEvents().ToList();
            if (!events.Any()) return;

            await _orderRepository.SaveAsync(aggregate);

            foreach (var @event in events)
            {
                await PublishEventAsync(@event);
            }

            _logger.LogInformation(logMessage, aggregate.Id);
        }

        private async Task PublishEventAsync(DomainEvent domainEvent)
        {
            await _eventPublisher.PublishToOrdersExchangeAsync(domainEvent);
        }

        private async Task<(bool Success, List<InventoryShortageItem> Shortages)> TryReserveInventoryAsync(OrderAggregate aggregate)
        {
            var inventoryResult = await _inventoryService.CheckAndReserveInventoryAsync(
                aggregate.Id, 
                aggregate.Items);

            if (inventoryResult.IsSuccessful)
            {
                aggregate.ReserveInventory(inventoryResult.Reservations);
                await SaveAndPublishEventsAsync(aggregate, "Inventory reserved for order: {0}");
                return (true, new List<InventoryShortageItem>());
            }
            else
            {
                aggregate.MarkAsPending(inventoryResult.Shortages);
                await SaveAndPublishEventsAsync(aggregate, "Order marked as pending due to insufficient inventory: {0}");
                
                _logger.LogWarning("Insufficient inventory for order {OrderId}. Order marked as pending. Shortages: {Shortages}", 
                    aggregate.Id, string.Join(", ", inventoryResult.Shortages.Select(s => $"{s.ProductId}: {s.Shortage}")));
                return (false, inventoryResult.Shortages);
            }
        }

        #endregion

        public async Task InitializeAsync()
        {
            await Task.CompletedTask;
        }

        public async Task<string> CreateOrderAsync(OrderMessage order)
        {
            if (order.Items == null || !order.Items.Any())
            {
                throw new ArgumentException("Order must contain at least one item");
            }

            if (order.Items.Any(item => item.Quantity <= 0))
            {
                throw new ArgumentException("All order items must have positive quantities");
            }

            var productIds = order.Items.Select(item => item.ProductId).Distinct().ToArray();
            var existingProducts = await _inventoryService.GetProductsAsync(productIds);
            var existingProductIds = existingProducts.Select(p => p.ProductId).ToHashSet();
            
            var missingProducts = productIds.Where(id => !existingProductIds.Contains(id)).ToList();
            if (missingProducts.Any())
            {
                throw new ArgumentException($"The following products do not exist: {string.Join(", ", missingProducts)}");
            }

            var aggregate = OrderAggregate.Create(order.CustomerName, order.TotalAmount, order.ProductIds, order.Items);
            
            var events = aggregate.GetUncommittedEvents().ToList();
            if (!events.Any())
            {
                throw new InvalidOperationException("No uncommitted events found after creating aggregate");
            }
            
            await SaveAndPublishEventsAsync(aggregate, "Order created: {0}");
            return aggregate.Id;
        }

        public async Task<OrderProcessingResult> ProcessOrderAsync(string orderId)
        {
            var aggregate = await _orderRepository.GetByIdAsync(orderId);
            
            aggregate.RequestInventoryReservation();
            await SaveAndPublishEventsAsync(aggregate, "Inventory reservation requested for order: {0}");

            var (inventoryReserved, shortages) = await TryReserveInventoryAsync(aggregate);
            
            if (inventoryReserved)
            {
                aggregate.StartProcessing();
                await SaveAndPublishEventsAsync(aggregate, "Order processing started: {0}");
                return new OrderProcessingResult 
                { 
                    IsSuccessful = true, 
                    Status = aggregate.Status,
                    Message = "Order processing started successfully"
                };
            }
            else
            {
                return new OrderProcessingResult 
                { 
                    IsSuccessful = false, 
                    Status = aggregate.Status,
                    Message = "Order marked as pending due to insufficient inventory",
                    Shortages = shortages
                };
            }
        }

        public async Task ProcessPendingOrderAsync(string orderId)
        {
            var aggregate = await _orderRepository.GetByIdAsync(orderId);
            
            if (aggregate.Status != OrderStatus.Pending)
            {
                throw new InvalidOperationException($"Cannot process pending order in {aggregate.Status} status");
            }

            aggregate.RequestInventoryReservation();
            await SaveAndPublishEventsAsync(aggregate, "Inventory reservation re-requested for pending order: {0}");

            var (inventoryReserved, shortages) = await TryReserveInventoryAsync(aggregate);
            
            if (inventoryReserved)
            {
                aggregate.StartProcessing();
                await SaveAndPublishEventsAsync(aggregate, "Pending order processing started: {0}");
            }
        }

        public async Task FulfillOrderAsync(string orderId)
        {
            var aggregate = await _orderRepository.GetByIdAsync(orderId);
            aggregate.Fulfill();
            
            await SaveAndPublishEventsAsync(aggregate, "Order fulfilled: {0}");
            await _inventoryService.FulfillInventoryAsync(orderId);
        }

        public async Task CancelOrderAsync(string orderId, string reason)
        {
            var aggregate = await _orderRepository.GetByIdAsync(orderId);
            aggregate.Cancel(reason);
            
            await SaveAndPublishEventsAsync(aggregate, "Order cancelled: {0}");
            await _inventoryService.ReleaseInventoryAsync(orderId, reason);
            
            _logger.LogInformation("Order cancelled: {OrderId}, Reason: {Reason}", orderId, reason);
        }

        public async Task<OrderAggregate> GetOrderAsync(string orderId)
        {
            return await _orderRepository.GetByIdAsync(orderId);
        }

        public async Task<OrderHistoryResponse> GetOrderHistoryAsync(string orderId)
        {
            var aggregate = await _orderRepository.GetByIdAsync(orderId);
            var events = await _orderRepository.GetEventsAsync(orderId);

            var historyEvents = events.Select(CreateHistoryEvent).ToList();

            return new OrderHistoryResponse
            {
                OrderId = aggregate.Id,
                CustomerName = aggregate.CustomerName,
                TotalAmount = aggregate.TotalAmount,
                CurrentStatus = aggregate.Status,
                CreatedAt = aggregate.CreatedAt,
                Events = historyEvents
            };
        }

        private OrderHistoryEvent CreateHistoryEvent(DomainEvent domainEvent)
        {
            var historyEvent = new OrderHistoryEvent
            {
                EventType = domainEvent.EventType,
                Timestamp = domainEvent.Timestamp,
                Version = domainEvent.Version
            };

            switch (domainEvent)
            {
                case OrderCreatedEvent created:
                    historyEvent.Description = "Order was created";
                    historyEvent.StatusAfterEvent = OrderStatus.Created;
                    break;

                case InventoryReservationRequestedEvent inventoryRequested:
                    historyEvent.Description = "Inventory reservation was requested";
                    break;

                case InventoryReservedEvent inventoryReserved:
                    historyEvent.Description = "Inventory was successfully reserved";
                    var reservationData = System.Text.Json.JsonSerializer.Deserialize<Shared.Models.InventoryReservationData>(domainEvent.Data);
                    if (reservationData?.Reservations != null)
                    {
                        historyEvent.AdditionalData["reservations"] = reservationData.Reservations.Select(r => new
                        {
                            r.ProductId,
                            r.QuantityRequested,
                            r.QuantityReserved
                        }).ToList();
                    }
                    break;

                case InventoryInsufficientEvent inventoryInsufficient:
                    historyEvent.Description = "Insufficient inventory - order cancelled";
                    historyEvent.StatusAfterEvent = OrderStatus.Cancelled;
                    var shortageData = System.Text.Json.JsonSerializer.Deserialize<Shared.Models.InventoryShortageData>(domainEvent.Data);
                    if (shortageData?.Shortages != null)
                    {
                        historyEvent.AdditionalData["shortages"] = shortageData.Shortages.Select(s => new
                        {
                            s.ProductId,
                            s.QuantityRequested,
                            s.QuantityAvailable,
                            s.Shortage
                        }).ToList();
                    }
                    break;

                case OrderPendingEvent pending:
                    historyEvent.Description = "Order marked as pending due to inventory shortage";
                    historyEvent.StatusAfterEvent = OrderStatus.Pending;
                    break;

                case OrderProcessingStartedEvent processing:
                    historyEvent.Description = "Order processing started";
                    historyEvent.StatusAfterEvent = OrderStatus.Processing;
                    break;

                case OrderFulfilledEvent fulfilled:
                    historyEvent.Description = "Order was fulfilled";
                    historyEvent.StatusAfterEvent = OrderStatus.Fulfilled;
                    break;

                case OrderCancelledEvent cancelled:
                    historyEvent.Description = "Order was cancelled";
                    historyEvent.StatusAfterEvent = OrderStatus.Cancelled;
                    var orderData = System.Text.Json.JsonSerializer.Deserialize<OrderMessage>(domainEvent.Data);
                    if (!string.IsNullOrEmpty(orderData?.Reason))
                    {
                        historyEvent.AdditionalData["cancellationReason"] = orderData.Reason;
                    }
                    break;

                case InventoryReleasedEvent inventoryReleased:
                    historyEvent.Description = "Inventory was released";
                    var releaseData = System.Text.Json.JsonSerializer.Deserialize<Shared.Models.InventoryReleaseData>(domainEvent.Data);
                    if (!string.IsNullOrEmpty(releaseData?.Reason))
                    {
                        historyEvent.AdditionalData["releaseReason"] = releaseData.Reason;
                    }
                    break;

                default:
                    historyEvent.Description = $"Unknown event: {domainEvent.EventType}";
                    break;
            }

            return historyEvent;
        }
    }
}
