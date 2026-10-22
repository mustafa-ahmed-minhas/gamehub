using GameHub.Models.Enums;
using Microsoft.AspNetCore.Mvc.Rendering;
using System.ComponentModel.DataAnnotations;

namespace GameHub.Models.ViewModels
{
    public class BookingFormViewModel : IValidatableObject
    {
        public int? Id { get; set; }
        public string? BookingNumber { get; set; }
        [Required] [Range(1, int.MaxValue, ErrorMessage = "Select a customer.")] public int CustomerId { get; set; }
        [Required] [Range(1, int.MaxValue, ErrorMessage = "Select a sport.")] public int SportId { get; set; }
        [Required] [Range(1, int.MaxValue, ErrorMessage = "Select a facility.")] public int FacilityId { get; set; }
        [Required] [Range(1, int.MaxValue, ErrorMessage = "Select a court.")] public int CourtId { get; set; }
        public int? CustomerMembershipId { get; set; }
        public DateOnly BookingDate { get; set; } = DateOnly.FromDateTime(DateTime.UtcNow);
        public TimeOnly StartTime { get; set; }
        public TimeOnly EndTime { get; set; }
        [Range(1, 600, ErrorMessage = "Duration must be greater than zero.")] public int DurationMinutes { get; set; }
        [Range(1, 100, ErrorMessage = "Player count must be at least 1.")] public int PlayerCount { get; set; } = 1;
        public BookingSource Source { get; set; } = BookingSource.Reception;
        public BookingStatus Status { get; set; } = BookingStatus.Pending;
        public bool IsWalkIn { get; set; }
        [MaxLength(1000)] public string? CustomerNotes { get; set; }
        [MaxLength(1500)] public string? InternalNotes { get; set; }
        [MaxLength(500)] public string? SpecialRequest { get; set; }
        [MaxLength(50)] public string? ReferenceNumber { get; set; }
        [Range(0, 999999999, ErrorMessage = "Manual discount cannot be negative.")] public decimal ManualDiscountAmount { get; set; }
        [Range(0, 999999999, ErrorMessage = "Paid amount cannot be negative.")] public decimal PaidAmount { get; set; }
        public string CurrencySymbol { get; set; } = "Rs";
        public string ArenaName { get; set; } = "GameHub Arena";
        public bool CanManualDiscount { get; set; }

        /// <summary>
        /// True once a Sales Order / Sales Invoice / Payment exists for this booking. The UI locks
        /// the financial inputs and the controller rejects conflicting financial edits.
        /// </summary>
        public bool HasFinancialRecords { get; set; }
        public bool IsEditMode => Id.HasValue;
        public List<SelectListItem> Customers { get; set; } = new();
        public List<SelectListItem> Sports { get; set; } = new();
        public List<SelectListItem> Facilities { get; set; } = new();
        public List<SelectListItem> Courts { get; set; } = new();
        public List<SelectListItem> StatusOptions { get; set; } = new();
        public List<SelectListItem> SourceOptions { get; set; } = new();
        public BookingPriceSummaryViewModel PriceSummary { get; set; } = new();

        /// <summary>Resolved display name for the selected customer (server side, never trusted from the client).</summary>
        public string? CustomerDisplayName { get; set; }

        /// <summary>Oldest selectable booking date (today).</summary>
        public DateOnly MinBookingDate { get; set; } = DateOnly.FromDateTime(DateTime.UtcNow);

        /// <summary>Last selectable booking date derived from the advance booking window.</summary>
        public DateOnly MaxBookingDate { get; set; } = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(14);

        /// <summary>Slot length the server derives for this court (minutes).</summary>
        public int SlotDurationMinutes { get; set; } = 60;

        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            if (EndTime <= StartTime)
                yield return new ValidationResult("Select an available slot so the booking has a valid time range.", new[] { nameof(StartTime) });
            if (ManualDiscountAmount < 0)
                yield return new ValidationResult("Manual discount cannot be negative.", new[] { nameof(ManualDiscountAmount) });
            if (PaidAmount < 0)
                yield return new ValidationResult("Paid amount cannot be negative.", new[] { nameof(PaidAmount) });
        }
    }
}
