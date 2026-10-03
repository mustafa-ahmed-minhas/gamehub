using GameHub.Models.Enums;
using GameHub.Models.ViewModels;
using GameHub.Services.Interfaces;

namespace GameHub.Services.Implementations
{
    public class DemoPaymentGateway : IPaymentGateway
    {
        private readonly IWebHostEnvironment _environment;

        public DemoPaymentGateway(IWebHostEnvironment environment)
        {
            _environment = environment;
        }

        public Task<PaymentGatewayResult> ProcessAsync(OnlinePaymentViewModel model, decimal amount, string currency)
        {
            if (!_environment.IsDevelopment())
            {
                return Task.FromResult(Failed("Production payment provider is not configured."));
            }

            var gatewayId = "DEMO-" + Guid.NewGuid().ToString("N")[..12].ToUpperInvariant();
            if (model.PaymentMethod == OnlinePaymentMethod.Card)
            {
                var digits = OnlyDigits(model.CardNumber);
                if (digits == "4242424242424242") return Task.FromResult(Success(gatewayId, "Visa", "4242"));
                if (digits == "4000000000000069") return Task.FromResult(Cancelled(gatewayId));
                return Task.FromResult(Failed("The demo card payment was declined.", gatewayId, digits.Length >= 4 ? digits[^4..] : null));
            }

            if (model.PaymentMethod == OnlinePaymentMethod.MobileWallet)
            {
                var digits = OnlyDigits(model.WalletNumber);
                if (digits.EndsWith("00")) return Task.FromResult(Success(gatewayId, "Wallet", digits[^Math.Min(4, digits.Length)..]));
                if (digits.EndsWith("02")) return Task.FromResult(Cancelled(gatewayId));
                return Task.FromResult(Failed("The demo wallet payment was declined.", gatewayId));
            }

            return model.SimulateBankTransferSuccess
                ? Task.FromResult(Success(gatewayId, "Bank", null))
                : Task.FromResult(Cancelled(gatewayId));
        }

        private static PaymentGatewayResult Success(string id, string? brand, string? lastFour) => new()
        {
            Succeeded = true,
            GatewayTransactionId = id,
            GatewayReference = "REF-" + id,
            CustomerMessage = "Payment successful.",
            CardBrand = brand,
            CardLastFour = lastFour
        };

        private static PaymentGatewayResult Failed(string message, string? id = null, string? lastFour = null) => new()
        {
            GatewayTransactionId = id ?? "DEMO-" + Guid.NewGuid().ToString("N")[..12].ToUpperInvariant(),
            GatewayReference = "REF-FAILED",
            CustomerMessage = "Payment was not completed.",
            FailureReason = message,
            CardLastFour = lastFour
        };

        private static PaymentGatewayResult Cancelled(string id) => new()
        {
            Cancelled = true,
            GatewayTransactionId = id,
            GatewayReference = "REF-CANCELLED",
            CustomerMessage = "Payment was cancelled.",
            FailureReason = "Customer cancelled the demo payment."
        };

        private static string OnlyDigits(string? value) => string.IsNullOrWhiteSpace(value) ? string.Empty : new string(value.Where(char.IsDigit).ToArray());
    }
}
