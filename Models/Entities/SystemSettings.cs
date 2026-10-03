using System.ComponentModel.DataAnnotations;

namespace GameHub.Models.Entities
{
    public class SystemSettings
    {
        public int Id { get; set; }

        [Required]
        [MaxLength(120)]
        public string ArenaName { get; set; } = string.Empty;

        [Required]
        [MaxLength(30)]
        public string Phone { get; set; } = string.Empty;

        [Required]
        [EmailAddress]
        [MaxLength(150)]
        public string Email { get; set; } = string.Empty;

        [MaxLength(250)]
        public string? Address { get; set; }

        [MaxLength(300)]
        public string? Logo { get; set; }

        public TimeOnly OpeningTime { get; set; }

        public TimeOnly ClosingTime { get; set; }

        public int SlotDurationMinutes { get; set; }

        public int BufferMinutes { get; set; }

        public int AdvanceBookingDays { get; set; }

        public bool AllowWalkIn { get; set; }

        public bool AllowOnlineBooking { get; set; }

        [Required]
        [MaxLength(20)]
        public string Currency { get; set; } = string.Empty;

        [MaxLength(8)]
        public string? CurrencySymbol { get; set; }

        public decimal TaxPercentage { get; set; }

        [Required]
        [MaxLength(20)]
        public string BookingPrefix { get; set; } = string.Empty;

        [Required]
        [MaxLength(20)]
        public string InvoicePrefix { get; set; } = string.Empty;

        [Required]
        [MaxLength(20)]
        public string ReceiptPrefix { get; set; } = string.Empty;

        [Required]
        [MaxLength(20)]
        public string LeadPrefix { get; set; } = "LEAD";

        [Required]
        [MaxLength(20)]
        public string OpportunityPrefix { get; set; } = "OPP";

        [Required]
        [MaxLength(20)]
        public string QuotationPrefix { get; set; } = "SQ";

        [Required]
        [MaxLength(20)]
        public string SalesOrderPrefix { get; set; } = "SO";

        public int DefaultQuotationValidityDays { get; set; } = 7;

        public int SessionTimeoutMinutes { get; set; }

        public int PasswordMinLength { get; set; }

        public bool RequireStrongPassword { get; set; }

        public bool EnableCustomerBookingReminders { get; set; } = true;

        public int FirstReminderHoursBefore { get; set; } = 24;

        public int SecondReminderHoursBefore { get; set; } = 3;

        public bool AllowCustomerCancellation { get; set; } = true;

        public int CancellationCutoffHours { get; set; } = 6;

        public bool AllowCustomerRescheduling { get; set; } = true;

        public int RescheduleCutoffHours { get; set; } = 6;

        [MaxLength(30)]
        public string? Version { get; set; }

        public DateTime BuildDate { get; set; }
    }
}
