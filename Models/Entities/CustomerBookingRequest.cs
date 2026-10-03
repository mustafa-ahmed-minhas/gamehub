using GameHub.Models.Enums;
using System.ComponentModel.DataAnnotations;

namespace GameHub.Models.Entities
{
    public class CustomerBookingRequest
    {
        public int Id { get; set; }
        public int BookingId { get; set; }
        public int CustomerId { get; set; }
        public CustomerBookingRequestType RequestType { get; set; }
        public CustomerBookingRequestStatus Status { get; set; }
        public DateOnly? RequestedDate { get; set; }
        public TimeOnly? RequestedStartTime { get; set; }
        public TimeOnly? RequestedEndTime { get; set; }
        [Required, MaxLength(500)] public string Reason { get; set; } = string.Empty;
        [MaxLength(500)] public string? AdminResponse { get; set; }
        public DateTime RequestedAt { get; set; }
        public DateTime? ReviewedAt { get; set; }
        public int? ReviewedByUserId { get; set; }

        public Booking Booking { get; set; } = null!;
        public Customer Customer { get; set; } = null!;
    }
}
