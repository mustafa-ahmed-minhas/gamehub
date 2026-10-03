using System.ComponentModel.DataAnnotations;

namespace GameHub.Models.Entities
{
    public class BookingInvoice
    {
        public int Id { get; set; }
        [Required, MaxLength(30)] public string InvoiceNumber { get; set; } = string.Empty;
        public int BookingId { get; set; }
        public int CustomerId { get; set; }
        public DateTime InvoiceDate { get; set; }
        public decimal Subtotal { get; set; }
        public decimal DiscountAmount { get; set; }
        public decimal TaxAmount { get; set; }
        public decimal TotalAmount { get; set; }
        public decimal PaidAmount { get; set; }
        public decimal BalanceAmount { get; set; }
        [Required, MaxLength(20)] public string Currency { get; set; } = string.Empty;
        [MaxLength(8)] public string CurrencySymbol { get; set; } = string.Empty;
        [Required, MaxLength(120)] public string ArenaName { get; set; } = string.Empty;
        [MaxLength(30)] public string? ArenaPhone { get; set; }
        [MaxLength(150)] public string? ArenaEmail { get; set; }
        [MaxLength(250)] public string? ArenaAddress { get; set; }
        [Required, MaxLength(120)] public string CustomerName { get; set; } = string.Empty;
        [MaxLength(150)] public string CustomerEmail { get; set; } = string.Empty;
        [MaxLength(20)] public string CustomerPhone { get; set; } = string.Empty;
        [Required, MaxLength(30)] public string BookingNumber { get; set; } = string.Empty;
        [Required, MaxLength(100)] public string CourtName { get; set; } = string.Empty;
        [Required, MaxLength(80)] public string SportName { get; set; } = string.Empty;
        public DateOnly BookingDate { get; set; }
        public TimeOnly StartTime { get; set; }
        public TimeOnly EndTime { get; set; }
        public bool IsPaid { get; set; }
        public DateTime CreatedAt { get; set; }

        public Booking Booking { get; set; } = null!;
        public Customer Customer { get; set; } = null!;
    }
}
