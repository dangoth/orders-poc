using Microsoft.AspNetCore.Mvc;
using OrderService.Services;
using Shared.Models;

namespace OrderService.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class OrdersController : ControllerBase
    {
        private readonly IOrderService _orderService;
        private readonly IInventoryService _inventoryService;
        private readonly IOrderValidationService _validationService;
        private readonly ILogger<OrdersController> _logger;

        public OrdersController(IOrderService orderService, IInventoryService inventoryService, IOrderValidationService validationService, ILogger<OrdersController> logger)
        {
            _orderService = orderService;
            _inventoryService = inventoryService;
            _validationService = validationService;
            _logger = logger;
        }

        [HttpPost]
        public async Task<IActionResult> CreateOrder([FromBody] CreateOrderRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var validationResult = _validationService.ValidateCreateOrderRequest(request);
            if (!validationResult.IsValid)
            {
                return BadRequest(new { validationResult.Errors });
            }

            var mergedItems = request.Items
                .GroupBy(item => item.ProductId)
                .Select(group => new OrderItem
                {
                    ProductId = group.Key,
                    Quantity = group.Sum(item => item.Quantity),
                    UnitPrice = group.First().UnitPrice
                })
                .ToList();

            var orderMessage = new OrderMessage
            {
                CustomerName = request.CustomerName,
                Items = mergedItems,
                ProductIds = mergedItems.Select(item => item.ProductId).ToArray(),
                TotalAmount = mergedItems.Sum(item => item.TotalPrice),
                Status = OrderStatus.Created
            };

            try
            {
                var productIds = mergedItems.Select(item => item.ProductId).ToArray();
                var stockInfo = await _inventoryService.GetDetailedAvailabilityAsync(productIds);
                var warnings = new List<string>();

                foreach (var item in mergedItems)
                {
                    var stock = stockInfo.FirstOrDefault(s => s.ProductId == item.ProductId);
                    if (stock != null && item.Quantity > stock.QuantityAvailable)
                    {
                        if (stock.QuantityAvailable == 0)
                        {
                            warnings.Add($"Product '{stock.ProductName}' ({item.ProductId}) is out of stock. Requested: {item.Quantity}, Available: 0");
                        }
                        else
                        {
                            warnings.Add($"Product '{stock.ProductName}' ({item.ProductId}) has insufficient stock. Requested: {item.Quantity}, Available: {stock.QuantityAvailable}");
                        }
                    }
                }

                var orderId = await _orderService.CreateOrderAsync(orderMessage);
                
                var response = new { 
                    OrderId = orderId, 
                    Message = "Order created successfully", 
                    TotalAmount = orderMessage.TotalAmount,
                    Warnings = warnings.ToArray()
                };
                
                return Ok(response);
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { Error = ex.Message });
            }
        }

        [HttpPost("{orderId}/process")]
        public async Task<IActionResult> ProcessOrder(string orderId)
        {
            var result = await _orderService.ProcessOrderAsync(orderId);
            
            if (result.IsSuccessful)
            {
                return Ok(new { 
                    Message = result.Message, 
                    Status = result.Status.ToString(),
                    Success = true
                });
            }
            else
            {
                return Ok(new { 
                    Message = result.Message, 
                    Status = result.Status.ToString(),
                    Success = false,
                    Warning = "Order is pending due to insufficient inventory",
                    Shortages = result.Shortages?.Select(s => new {
                        s.ProductId,
                        s.QuantityRequested,
                        s.QuantityAvailable,
                        s.Shortage
                    })
                });
            }
        }

        [HttpPost("{orderId}/process-pending")]
        public async Task<IActionResult> ProcessPendingOrder(string orderId)
        {
            await _orderService.ProcessPendingOrderAsync(orderId);
            return Ok(new { Message = "Pending order processing attempted" });
        }

        [HttpPost("{orderId}/fulfill")]
        public async Task<IActionResult> FulfillOrder(string orderId)
        {
            await _orderService.FulfillOrderAsync(orderId);
            return Ok(new { Message = "Order fulfilled" });
        }

        [HttpPost("{orderId}/cancel")]
        public async Task<IActionResult> CancelOrder(string orderId, [FromBody] CancelOrderRequest request)
        {
            await _orderService.CancelOrderAsync(orderId, request.Reason);
            return Ok(new { Message = "Order cancelled" });
        }

        [HttpGet("{orderId}")]
        public async Task<IActionResult> GetOrder(string orderId)
        {
            var order = await _orderService.GetOrderAsync(orderId);
            return Ok(order);
        }

        [HttpGet("{orderId}/history")]
        public async Task<IActionResult> GetOrderHistory(string orderId)
        {
            try
            {
                var history = await _orderService.GetOrderHistoryAsync(orderId);
                return Ok(history);
            }
            catch (InvalidOperationException ex) when (ex.Message.Contains("not found"))
            {
                return NotFound(new { Error = $"Order with id {orderId} not found" });
            }
        }
    }

    public class CancelOrderRequest
    {
        public string Reason { get; set; } = string.Empty;
    }
}
