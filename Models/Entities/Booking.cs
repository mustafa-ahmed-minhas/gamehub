using GameHub.Models.Enums;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace GameHub.Models.Entities
{
    public class Booking
    {
        public int Id { get; set; }

        [Required, MaxLength(30)]
        public string BookingNumber { get; set; } = string.Empty;

        public int CustomerId { get; set; }
        public int SportId { get; set; }
        public int FacilityId { get; set; }
        public int CourtId { get; set; }
        public int? CustomerMembershipId { get; set; }

        public DateOnly BookingDate { get; set; }
        public TimeOnly StartTime { get; set; }
        public TimeOnly EndTime { get; set; }
        public int DurationMinutes { get; set; }
        public int PlayerCount { get; set; }

        public BookingStatus Status { get; set; }
        public PaymentStatus PaymentStatus { get; set; }
        public BookingSource Source { get; set; }

        public decimal BaseAmount { get; set; }
        public decimal MembershipDiscountAmount { get; set; }
        public decimal ManualDiscountAmount { get; set; }
        public decimal TaxAmount { get; set; }
        public decimal TotalAmount { get; set; }
        public decimal PaidAmount { get; set; }
        public decimal BalanceAmount { get; set; }

        public int? PaymentTransactionId { get; set; }

        [MaxLength(80)]
        public string? WebsiteCheckoutToken { get; set; }

        public DateTime? PaidAt { get; set; }

        [MaxLength(100)]
        public string? PaymentReference { get; set; }

        public bool CustomerReminderEnabled { get; set; }

        public DateTime? CustomerCancelledAt { get; set; }

        public DateTime? CustomerRescheduleRequestedAt { get; set; }

        [MaxLength(1000)]
        public string? CustomerNotes { get; set; }

        [MaxLength(1500)]
        public string? InternalNotes { get; set; }

        [MaxLength(500)]
        public string? CancellationReason { get; set; }

        [MaxLength(500)]
        public string? RescheduleReason { get; set; }

        [MaxLength(500)]
        public string? SpecialRequest { get; set; }

        [MaxLength(50)]
        public string? ReferenceNumber { get; set; }

        public bool IsWalkIn { get; set; }
        public bool IsPeakRate { get; set; }
        public bool IsMemberBooking { get; set; }
        public bool MembershipHoursApplied { get; set; }
        public bool IsActive { get; set; } = true;

        public int CreatedByUserId { get; set; }
        public int? UpdatedByUserId { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public DateTime? ConfirmedAt { get; set; }
        public DateTime? CancelledAt { get; set; }
        public DateTime? CompletedAt { get; set; }
        public DateTime? CheckedInAt { get; set; }
        public DateTime? CheckedOutAt { get; set; }

        public Customer Customer { get; set; } = null!;
        public Sport Sport { get; set; } = null!;
        public Facility Facility { get; set; } = null!;
        public Court Court { get; set; } = null!;
        public CustomerMembership? CustomerMembership { get; set; }
        public PaymentTransaction? PaymentTransaction { get; set; }

        [NotMapped]
        public string BookingTimeRange => $"{StartTime:HH\\:mm} - {EndTime:HH\\:mm}";

        [NotMapped]
        public bool IsPast => BookingDate.ToDateTime(EndTime) < DateTime.Now;

        [NotMapped]
        public bool IsUpcoming => BookingDate.ToDateTime(StartTime) >= DateTime.Now;
    }
}
