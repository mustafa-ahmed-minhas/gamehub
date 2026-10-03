using System.ComponentModel.DataAnnotations;

namespace GameHub.Models.ViewModels
{
    public class SystemSettingsViewModel : IValidatableObject
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "Arena name is required.")]
        [MaxLength(120)]
        public string ArenaName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Phone is required.")]
        [MaxLength(30)]
        public string Phone { get; set; } = string.Empty;

        [Required(ErrorMessage = "Email is required.")]
        [EmailAddress(ErrorMessage = "Enter a valid email address.")]
        [MaxLength(150)]
        public string Email { get; set; } = string.Empty;

        [MaxLength(250)]
        public string? Address { get; set; }

        [MaxLength(300)]
        public string? Logo { get; set; }

        [Required(ErrorMessage = "Opening time is required.")]
        public TimeOnly? OpeningTime { get; set; }

        [Required(ErrorMessage = "Closing time is required.")]
        public TimeOnly? ClosingTime { get; set; }

        [Range(1, 1440, ErrorMessage = "Slot duration must be positive.")]
        public int SlotDurationMinutes { get; set; }

        [Range(1, 1440, ErrorMessage = "Buffer minutes must be positive.")]
        public int BufferMinutes { get; set; }

        [Range(0, 365, ErrorMessage = "Advance booking days must be 0 or more.")]
        public int AdvanceBookingDays { get; set; }

        public bool AllowWalkIn { get; set; }

        public bool AllowOnlineBooking { get; set; }

        [Required(ErrorMessage = "Currency is required.")]
        [MaxLength(20)]
        public string Currency { get; set; } = string.Empty;

        [MaxLength(8)]
        public string? CurrencySymbol { get; set; }

        [Range(0, 100, ErrorMessage = "Tax must be between 0 and 100.")]
        public decimal TaxPercentage { get; set; }

        [Required(ErrorMessage = "Booking prefix is required.")]
        [MaxLength(20)]
        public string BookingPrefix { get; set; } = string.Empty;

        [Required(ErrorMessage = "Invoice prefix is required.")]
        [MaxLength(20)]
        public string InvoicePrefix { get; set; } = string.Empty;

        [Required(ErrorMessage = "Receipt prefix is required.")]
        [MaxLength(20)]
        public string ReceiptPrefix { get; set; } = string.Empty;

        [Range(5, 1440, ErrorMessage = "Session timeout must be at least 5 minutes.")]
        public int SessionTimeoutMinutes { get; set; }

        [Required(ErrorMessage = "Password minimum length is required.")]
        [Range(6, 64, ErrorMessage = "Password minimum length must be between 6 and 64.")]
        public int PasswordMinLength { get; set; }

        public bool RequireStrongPassword { get; set; }

        [MaxLength(30)]
        public string? Version { get; set; }

        public DateTime BuildDate { get; set; }

        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            if (OpeningTime.HasValue && ClosingTime.HasValue && OpeningTime.Value >= ClosingTime.Value)
            {
                yield return new ValidationResult("Closing time must be after opening time.", new[] { nameof(ClosingTime) });
            }
        }
    }
}
