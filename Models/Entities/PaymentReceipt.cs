using GameHub.Models.Enums;
using System.ComponentModel.DataAnnotations;

namespace GameHub.Models.Entities
{
    public class PaymentReceipt
    {
        public int Id { get; set; }
        [Required, MaxLength(30)] public string ReceiptNumber { get; set; } = string.Empty;
        public int PaymentTransactionId { get; set; }
        public int BookingId { get; set; }
        public int CustomerId { get; set; }
        public DateTime ReceiptDate { get; set; }
        public decimal AmountPaid { get; set; }
        public OnlinePaymentMethod PaymentMethod { get; set; }
        [Required, MaxLength(100)] public string PaymentReference { get; set; } = string.Empty;
        [Required, MaxLength(20)] public string Currency { get; set; } = string.Empty;
        [MaxLength(8)] public string CurrencySymbol { get; set; } = string.Empty;
        [Required, MaxLength(120)] public string CustomerName { get; set; } = string.Empty;
        [Required, MaxLength(30)] public string BookingNumber { get; set; } = string.Empty;
        [Required, MaxLength(120)] public string ArenaName { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }

        public PaymentTransaction PaymentTransaction { get; set; } = null!;
        public Booking Booking { get; set; } = null!;
        public Customer Customer { get; set; } = null!;
    }
}
