using GameHub.Models.Enums;
using System.ComponentModel.DataAnnotations;

namespace GameHub.Models.Entities
{
    public class CustomerNotification
    {
        public int Id { get; set; }
        public int CustomerId { get; set; }
        public int? BookingId { get; set; }
        public CustomerNotificationType Type { get; set; }
        [Required, MaxLength(120)] public string Title { get; set; } = string.Empty;
        [Required, MaxLength(500)] public string Message { get; set; } = string.Empty;
        [MaxLength(300)] public string? ActionUrl { get; set; }
        public bool IsRead { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? ReadAt { get; set; }
        public DateTime? ExpiresAt { get; set; }

        public Customer Customer { get; set; } = null!;
        public Booking? Booking { get; set; }
    }
}
