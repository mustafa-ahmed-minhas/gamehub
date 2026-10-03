using System.ComponentModel.DataAnnotations;

namespace GameHub.Models.Entities
{
    public class CourtPricing
    {
        public int Id { get; set; }
        public int CourtId { get; set; }

        [Required, MaxLength(100)]
        public string Name { get; set; } = string.Empty;

        public DayOfWeek? DayOfWeek { get; set; }
        public TimeOnly StartTime { get; set; }
        public TimeOnly EndTime { get; set; }
        public int DurationMinutes { get; set; }
        public decimal Price { get; set; }
        public bool IsPeakRate { get; set; }
        public bool IsActive { get; set; } = true;
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }

        public Court Court { get; set; } = null!;
    }
}
