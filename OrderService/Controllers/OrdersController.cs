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
        private readonly ILogger<OrdersController> _logger;

        public OrdersController(IOrderService orderService, ILogger<OrdersController> logger)
        {
            _orderService = orderService;
            _logger = logger;
        }

        [HttpPost]
        public async Task<IActionResult> CreateOrder([FromBody] CreateOrderRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            if (request.Items == null || !request.Items.Any())
            {
                return BadRequest(new { Error = "At least one product must be selected" });
            }
            if (request.Items.Any(item => item.Quantity <= 0))
            {
                return BadRequest(new { Error = "All product quantities must be greater than 0" });
            }

            if (request.Items.Any(item => string.IsNullOrWhiteSpace(item.ProductId)))
            {
                return BadRequest(new { Error = "All products must have valid product IDs" });
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
                var orderId = await _orderService.CreateOrderAsync(orderMessage);
                return Ok(new { OrderId = orderId, Message = "Order created successfully", TotalAmount = orderMessage.TotalAmount });
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
