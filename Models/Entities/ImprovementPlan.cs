using System.ComponentModel.DataAnnotations;
using GameHub.Models.Enums;

namespace GameHub.Models.Entities
{
    /// <summary>
    /// A measurable performance improvement plan attached to a case (Screen 5).
    /// Holds the objectives, the measurable expectations, the start / review /
    /// end dates, and the tracked progress, review notes, outcome, and closure.
    /// </summary>
    public class ImprovementPlan
    {
        public int Id { get; set; }

        public int PerformanceCaseId { get; set; }

        public PerformanceCase? PerformanceCase { get; set; }

        [Required, MaxLength(3000)]
        public string Objectives { get; set; } = string.Empty;

        [Required, MaxLength(3000)]
        public string MeasurableExpectations { get; set; } = string.Empty;

        public DateOnly StartDate { get; set; }

        public DateOnly ReviewDate { get; set; }

        public DateOnly EndDate { get; set; }

        // Tracked completion, 0-100.
        [Range(0, 100)]
        public int ProgressPercent { get; set; }

        [MaxLength(3000)]
        public string? ReviewNotes { get; set; }

        [MaxLength(2000)]
        public string? Outcome { get; set; }

        public ImprovementPlanStatus Status { get; set; } = ImprovementPlanStatus.Active;

        public bool IsActive { get; set; } = true;

        public DateTime CreatedAt { get; set; }

        public DateTime? UpdatedAt { get; set; }

        public int? CreatedByUserId { get; set; }

        public User? CreatedByUser { get; set; }
    }
}
