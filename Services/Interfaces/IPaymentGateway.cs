using GameHub.Models.Entities;
using GameHub.Models.ViewModels;

namespace GameHub.Services.Interfaces
{
    public interface IPaymentGateway
    {
        Task<PaymentGatewayResult> ProcessAsync(OnlinePaymentViewModel model, decimal amount, string currency);

        Task<PaymentGatewayRefundResult> RefundAsync(string gatewayTransactionId, decimal amount, string currency, string reason);
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

    public class PaymentGatewayRefundResult
    {
        public bool Succeeded { get; set; }
        public string GatewayRefundId { get; set; } = string.Empty;
        public string GatewayRefundReference { get; set; } = string.Empty;
        public string CustomerMessage { get; set; } = string.Empty;
        public string? FailureReason { get; set; }
    }
}
