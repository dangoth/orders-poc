using Microsoft.AspNetCore.Mvc;
using OrderService.Services;

namespace OrderService.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class InventoryController : ControllerBase
    {
        private readonly IInventoryService _inventoryService;
        private readonly ILogger<InventoryController> _logger;

        public InventoryController(IInventoryService inventoryService, ILogger<InventoryController> logger)
        {
            _inventoryService = inventoryService;
            _logger = logger;
        }

        [HttpGet("products")]
        public async Task<IActionResult> GetProducts([FromQuery] string[] productIds)
        {
            if (!productIds.Any())
            {
                return BadRequest("At least one product ID must be provided");
            }

            var products = await _inventoryService.GetProductsAsync(productIds);
            return Ok(products);
        }

        [HttpGet("availability")]
        public async Task<IActionResult> CheckAvailability([FromQuery] string[] productIds)
        {
            if (!productIds.Any())
            {
                return BadRequest("At least one product ID must be provided");
            }

            var isAvailable = await _inventoryService.IsInventoryAvailableAsync(productIds);
            return Ok(new { ProductIds = productIds, IsAvailable = isAvailable });
        }

        [HttpPost("seed")]
        public async Task<IActionResult> SeedInventoryData()
        {
            await _inventoryService.SeedInventoryDataAsync();
            return Ok(new { Message = "Inventory data seeded successfully" });
        }
    }
}