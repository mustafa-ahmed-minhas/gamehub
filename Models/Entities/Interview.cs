using System.ComponentModel.DataAnnotations;
using GameHub.Models.Enums;

namespace GameHub.Models.Entities
{
    /// <summary>
    /// A scheduled interview for a job application. Records the panel, timing,
    /// meeting location/link, notes, and the resulting status and outcome.
    /// </summary>
    public class Interview
    {
        public int Id { get; set; }

        public int JobApplicationId { get; set; }

        public JobApplication? JobApplication { get; set; }

        public DateOnly InterviewDate { get; set; }

        public TimeOnly InterviewTime { get; set; }

        public InterviewType InterviewType { get; set; }

        [Required, MaxLength(150)]
        public string Interviewer { get; set; } = string.Empty;

        // Meeting location for in-person interviews, or the meeting link for online ones.
        [MaxLength(250)]
        public string? Location { get; set; }

        [MaxLength(2000)]
        public string? Notes { get; set; }

        public InterviewStatus Status { get; set; } = InterviewStatus.Scheduled;

        public InterviewOutcome Outcome { get; set; } = InterviewOutcome.Pending;

        [MaxLength(2000)]
        public string? Feedback { get; set; }

        public bool IsActive { get; set; } = true;

        public DateTime CreatedAt { get; set; }

        public DateTime? UpdatedAt { get; set; }

        public int? CreatedByUserId { get; set; }

        public User? CreatedByUser { get; set; }
    }
}
