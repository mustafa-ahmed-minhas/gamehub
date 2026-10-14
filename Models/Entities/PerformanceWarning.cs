using System.ComponentModel.DataAnnotations;
using GameHub.Models.Enums;

namespace GameHub.Models.Entities
{
    /// <summary>
    /// A formal warning issued within a performance case (Screen 5). Records the
    /// reason, the issue date, the employee's response or HR remarks, and the
    /// current status. Warnings are preserved as part of the case history.
    /// </summary>
    public class PerformanceWarning
    {
        public int Id { get; set; }

        public int PerformanceCaseId { get; set; }

        public PerformanceCase? PerformanceCase { get; set; }

        [Required, MaxLength(3000)]
        public string Reason { get; set; } = string.Empty;

        public DateOnly IssueDate { get; set; }

        [MaxLength(3000)]
        public string? ResponseRemarks { get; set; }

        public WarningStatus Status { get; set; } = WarningStatus.Issued;

        public bool IsActive { get; set; } = true;

        public DateTime CreatedAt { get; set; }

        public DateTime? UpdatedAt { get; set; }

        public int? CreatedByUserId { get; set; }

        public User? CreatedByUser { get; set; }
    }
}
