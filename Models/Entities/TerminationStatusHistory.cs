using System.ComponentModel.DataAnnotations;
using GameHub.Models.Enums;

namespace GameHub.Models.Entities
{
    /// <summary>
    /// An immutable audit line recording a termination request status change
    /// (creation, submission, approval / rejection, clearance start, and
    /// completion). Together these rows form the approval and audit history.
    /// </summary>
    public class TerminationStatusHistory
    {
        public int Id { get; set; }

        public int TerminationRequestId { get; set; }

        public TerminationRequest? TerminationRequest { get; set; }

        public TerminationStatus? FromStatus { get; set; }

        public TerminationStatus ToStatus { get; set; }

        [MaxLength(2000)]
        public string? Note { get; set; }

        public int? ChangedByUserId { get; set; }

        public User? ChangedByUser { get; set; }

        public DateTime ChangedAt { get; set; }
    }
}
