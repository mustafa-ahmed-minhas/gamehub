using System.ComponentModel.DataAnnotations;
using GameHub.Models.Enums;

namespace GameHub.Models.Entities
{
    /// <summary>
    /// A recorded performance meeting for a case (Screen 5). Captures the
    /// discussion, the agreed outcome, and any follow-up date. Meetings are
    /// preserved as part of the case history.
    /// </summary>
    public class PerformanceMeeting
    {
        public int Id { get; set; }

        public int PerformanceCaseId { get; set; }

        public PerformanceCase? PerformanceCase { get; set; }

        public DateOnly MeetingDate { get; set; }

        public TimeOnly MeetingTime { get; set; }

        [Required, MaxLength(3000)]
        public string DiscussionNotes { get; set; } = string.Empty;

        [MaxLength(2000)]
        public string? Outcome { get; set; }

        public DateOnly? FollowUpDate { get; set; }

        public PerformanceMeetingStatus Status { get; set; } = PerformanceMeetingStatus.Scheduled;

        public bool IsActive { get; set; } = true;

        public DateTime CreatedAt { get; set; }

        public DateTime? UpdatedAt { get; set; }

        public int? CreatedByUserId { get; set; }

        public User? CreatedByUser { get; set; }
    }
}
