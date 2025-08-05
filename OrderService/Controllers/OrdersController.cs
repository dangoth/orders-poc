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
        public async Task<IActionResult> CreateOrder([FromBody] OrderMessage order)
        {
            var orderId = await _orderService.CreateOrderAsync(order);
            return Ok(new { OrderId = orderId, Message = "Order created successfully" });
        }

        [HttpPost("{orderId}/process")]
        public async Task<IActionResult> ProcessOrder(string orderId)
        {
            await _orderService.ProcessOrderAsync(orderId);
            return Ok(new { Message = "Order processing started" });
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
    }

    public class CancelOrderRequest
    {
        public string Reason { get; set; } = string.Empty;
    }
}
