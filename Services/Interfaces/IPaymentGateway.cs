using GameHub.Models.ViewModels;

namespace GameHub.Services.Interfaces
{
    public interface IPaymentGateway
    {
        Task<PaymentGatewayResult> ProcessAsync(OnlinePaymentViewModel model, decimal amount, string currency);
    }

    public class PaymentGatewayResult
    {
        public bool Succeeded { get; set; }
        public bool Cancelled { get; set; }
        public string GatewayTransactionId { get; set; } = string.Empty;
        public string GatewayReference { get; set; } = string.Empty;
        public string CustomerMessage { get; set; } = string.Empty;
        public string? FailureReason { get; set; }
        public string? CardBrand { get; set; }
        public string? CardLastFour { get; set; }
    }
}
