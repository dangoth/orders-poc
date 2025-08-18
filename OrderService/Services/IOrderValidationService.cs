using Shared.Models;

namespace OrderService.Services
{
    public interface IOrderValidationService
    {
        ValidationResult ValidateCreateOrderRequest(CreateOrderRequest request);
        ValidationResult ValidateProductIds(string[] productIds);
    }

    public class ValidationResult
    {
        public bool IsValid { get; set; } = true;
        public List<string> Errors { get; set; } = new();
        
        public void AddError(string error)
        {
            IsValid = false;
            Errors.Add(error);
        }
    }
}