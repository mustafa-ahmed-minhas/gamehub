using System.ComponentModel.DataAnnotations;
using GameHub.Helpers;

namespace GameHub.Models.ViewModels
{
    public class OnlineBookingReviewViewModel
    {
        [Required]
        public int CourtId { get; set; }

        [Required]
        public DateOnly BookingDate { get; set; }

        [Required]
        public string StartTime { get; set; } = string.Empty;

        [Required]
        public int DurationMinutes { get; set; }

        [Range(1, 500)]
        public int PlayerCount { get; set; } = 1;

        [MaxLength(500)]
        public string? CustomerNotes { get; set; }

        [MaxLength(500)]
        public string? SpecialRequest { get; set; }

        [MustBeTrue(ErrorMessage = "Please accept the booking policy.")]
        public bool AcceptBookingPolicy { get; set; }

        [MustBeTrue(ErrorMessage = "Please accept the cancellation policy.")]
        public bool AcceptCancellationPolicy { get; set; }
    }
}
