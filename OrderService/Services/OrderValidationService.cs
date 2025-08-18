using Shared.Models;

namespace OrderService.Services
{
    public class OrderValidationService : IOrderValidationService
    {
        public ValidationResult ValidateCreateOrderRequest(CreateOrderRequest request)
        {
            var result = new ValidationResult();

            if (request.Items == null || !request.Items.Any())
            {
                result.AddError("At least one product must be selected");
                return result;
            }

            if (request.Items.Any(item => item.Quantity <= 0))
            {
                result.AddError("All product quantities must be greater than 0");
            }

            if (request.Items.Any(item => string.IsNullOrWhiteSpace(item.ProductId)))
            {
                result.AddError("All products must have valid product IDs");
            }

            if (string.IsNullOrWhiteSpace(request.CustomerName))
            {
                result.AddError("Customer name is required");
            }

            return result;
        }

        public ValidationResult ValidateProductIds(string[] productIds)
        {
            var result = new ValidationResult();

            if (!productIds.Any())
            {
                result.AddError("At least one product ID must be provided");
            }

            if (productIds.Any(string.IsNullOrWhiteSpace))
            {
                result.AddError("All product IDs must be valid");
            }

            return result;
        }
    }
}