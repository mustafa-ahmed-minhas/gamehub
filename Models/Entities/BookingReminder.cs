using GameHub.Models.Enums;

namespace GameHub.Models.Entities
{
    public class BookingReminder
    {
        public int Id { get; set; }
        public int BookingId { get; set; }
        public int CustomerId { get; set; }
        public BookingReminderType ReminderType { get; set; }
        public DateTime ScheduledFor { get; set; }
        public DateTime? DisplayedAt { get; set; }
        public bool IsProcessed { get; set; }
        public bool IsActive { get; set; } = true;
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }

        public Booking Booking { get; set; } = null!;
        public Customer Customer { get; set; } = null!;
    }
}
