using Shared.Models;
using Shared.RabbitMQ;
using System.Text.Json;

namespace RestockingService.Services
{
    public interface IRestockingService
    {
        Task InitializeAsync();
        Task ProcessRestockingMessageAsync(string message);
    }

    public class RestockingService : IRestockingService
    {
        private readonly IRabbitMQConsumer _consumer;
        private readonly ILogger<RestockingService> _logger;

        public RestockingService(IRabbitMQConsumer consumer, ILogger<RestockingService> logger)
        {
            _consumer = consumer;
            _logger = logger;
        }

        public async Task InitializeAsync()
        {
            await _consumer.InitializeAsync(
                RabbitMQConstants.RestockingQueue,
                RabbitMQConstants.RestockingExchange,
                RabbitMQConstants.RestockingRoutingKey);
            
            await _consumer.StartConsumingAsync(
                RabbitMQConstants.RestockingQueue,
                ProcessRestockingMessageAsync);
        }

        public async Task ProcessRestockingMessageAsync(string message)
        {
            try
            {
                var domainEvent = TryDeserializeDomainEvent(message);
                if (domainEvent != null)
                {
                    await ProcessRestockingEventAsync(domainEvent);
                }
                else
                {
                    _logger.LogWarning("Unable to deserialize restocking message: {Message}", message);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing restocking message: {Message}", message);
                throw;
            }
        }

        private async Task ProcessRestockingEventAsync(DomainEvent domainEvent)
        {
            switch (domainEvent.EventType)
            {
                case nameof(LowStockWarningEvent):
                    var warningData = JsonSerializer.Deserialize<LowStockWarningData>(domainEvent.Data);
                    if (warningData != null)
                    {
                        _logger.LogWarning("ALERT: Low stock detected for {ProductName} (ID: {ProductId}). Current stock: {CurrentStock}, Reorder level: {ReorderLevel}", 
                            warningData.ProductName, warningData.ProductId, warningData.CurrentStock, warningData.ReorderLevel);
                        
                        await SimulateNotificationAsync("Low Stock Alert", $"Product {warningData.ProductName} is running low with only {warningData.CurrentStock} units remaining.");
                    }
                    break;

                case nameof(RestockRequestEvent):
                    var requestData = JsonSerializer.Deserialize<RestockRequestData>(domainEvent.Data);
                    if (requestData != null)
                    {
                        _logger.LogInformation("RESTOCK REQUEST: Product {ProductName} (ID: {ProductId}) needs {RequestedQuantity} units. Priority: {Priority}", 
                            requestData.ProductName, requestData.ProductId, requestData.RequestedQuantity, requestData.Priority);
                        
                        await SimulateRestockOrderAsync(requestData);
                    }
                    break;

                default:
                    _logger.LogInformation("Unhandled restocking event type: {EventType}", domainEvent.EventType);
                    break;
            }
        }

        private async Task SimulateNotificationAsync(string subject, string message)
        {
            await Task.Delay(100);
            _logger.LogInformation("NOTIFICATION SENT - Subject: {Subject}, Message: {Message}", subject, message);
        }

        private async Task SimulateRestockOrderAsync(RestockRequestData requestData)
        {
            await Task.Delay(500);
            _logger.LogInformation("RESTOCK ORDER PLACED - Product: {ProductName}, Quantity: {Quantity}, Priority: {Priority}, Estimated delivery: {DeliveryDate}", 
                requestData.ProductName, requestData.RequestedQuantity, requestData.Priority, DateTime.UtcNow.AddDays(3).ToString("yyyy-MM-dd"));
        }

        private DomainEvent? TryDeserializeDomainEvent(string message)
        {
            try
            {
                using var document = JsonDocument.Parse(message);
                if (!document.RootElement.TryGetProperty("EventType", out var eventTypeElement))
                {
                    return null;
                }

                var eventType = eventTypeElement.GetString();
                if (string.IsNullOrEmpty(eventType))
                {
                    return null;
                }

                return eventType switch
                {
                    nameof(LowStockWarningEvent) => JsonSerializer.Deserialize<LowStockWarningEvent>(message),
                    nameof(RestockRequestEvent) => JsonSerializer.Deserialize<RestockRequestEvent>(message),
                    _ => null
                };
            }
            catch (JsonException)
            {
                return null;
            }
        }
    }
}