using Microsoft.AspNetCore.Mvc;
using OrderService.Services;

namespace OrderService.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class InventoryController : ControllerBase
    {
        private readonly IInventoryService _inventoryService;
        private readonly IOrderValidationService _validationService;
        private readonly ILogger<InventoryController> _logger;

        public InventoryController(IInventoryService inventoryService, IOrderValidationService validationService, ILogger<InventoryController> logger)
        {
            _inventoryService = inventoryService;
            _validationService = validationService;
            _logger = logger;
        }

        [HttpGet("products")]
        public async Task<IActionResult> GetProducts([FromQuery] string[] productIds)
        {
            var validationResult = _validationService.ValidateProductIds(productIds);
            if (!validationResult.IsValid)
            {
                return BadRequest(new { Errors = validationResult.Errors });
            }

            var products = await _inventoryService.GetProductsAsync(productIds);
            return Ok(products);
        }

        [HttpGet("availability")]
        public async Task<IActionResult> CheckAvailability([FromQuery] string[] productIds)
        {
            var validationResult = _validationService.ValidateProductIds(productIds);
            if (!validationResult.IsValid)
            {
                return BadRequest(new { validationResult.Errors });
            }

            var detailedAvailability = await _inventoryService.GetDetailedAvailabilityAsync(productIds);
            return Ok(new { Products = detailedAvailability });
        }

        [HttpPost("seed")]
        public async Task<IActionResult> SeedInventoryData()
        {
            await _inventoryService.SeedInventoryDataAsync();
            return Ok(new { Message = "Inventory data seeded successfully" });
        }
    }
}