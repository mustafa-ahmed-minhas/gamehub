using System.ComponentModel.DataAnnotations;

namespace GameHub.Models.Entities
{
    public class CourtSchedule
    {
        public int Id { get; set; }
        public int CourtId { get; set; }
        public DayOfWeek DayOfWeek { get; set; }
        public TimeOnly OpeningTime { get; set; }
        public TimeOnly ClosingTime { get; set; }
        public bool IsClosed { get; set; }
        public int SlotDurationMinutes { get; set; }
        public int BufferMinutes { get; set; }
        public bool IsActive { get; set; } = true;
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }

        public Court Court { get; set; } = null!;
    }
}
