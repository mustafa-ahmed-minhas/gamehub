using GameHub.Models.Enums;
using System.ComponentModel.DataAnnotations;

namespace GameHub.Models.Entities
{
    public class PaymentTransaction
    {
        public int Id { get; set; }
        [Required, MaxLength(30)] public string TransactionNumber { get; set; } = string.Empty;
        [Required, MaxLength(80)] public string CheckoutToken { get; set; } = string.Empty;
        public int CustomerId { get; set; }
        public int? BookingId { get; set; }
        public PaymentGatewayType Gateway { get; set; }
        public OnlinePaymentMethod PaymentMethod { get; set; }
        public PaymentTransactionStatus Status { get; set; }
        [Required, MaxLength(100)] public string GatewayTransactionId { get; set; } = string.Empty;
        [MaxLength(100)] public string? GatewayReference { get; set; }
        public decimal Amount { get; set; }
        [Required, MaxLength(20)] public string Currency { get; set; } = string.Empty;
        [MaxLength(8)] public string CurrencySymbol { get; set; } = string.Empty;
        [MaxLength(300)] public string? FailureReason { get; set; }
        [MaxLength(300)] public string? CustomerSafeMessage { get; set; }
        [MaxLength(120)] public string? IdempotencyKey { get; set; }
        public DateTime InitiatedAt { get; set; }
        public DateTime? ProcessingAt { get; set; }
        public DateTime? CompletedAt { get; set; }
        public DateTime? FailedAt { get; set; }
        public DateTime? CancelledAt { get; set; }
        public DateTime? RefundedAt { get; set; }
        public string? SafeMetadataJson { get; set; }
        [MaxLength(128)] public string? ClientIpHash { get; set; }
        [MaxLength(250)] public string? UserAgentSummary { get; set; }
        [MaxLength(30)] public string? CardBrand { get; set; }
        [MaxLength(4)] public string? CardLastFour { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }

        public Customer Customer { get; set; } = null!;
        public Booking? Booking { get; set; }
    }
}
