using System.ComponentModel.DataAnnotations;
using GameHub.Models.Enums;

namespace GameHub.Models.Entities
{
    /// <summary>
    /// A single clearance checklist line for a termination request (Screen 6).
    /// Each item belongs to a category (IT Assets, Department Handover,
    /// Finance/Dues, Administration, HR) and records the responsible
    /// person/department, outstanding items or dues, remarks, a status, and the
    /// completion date.
    /// </summary>
    public class ClearanceItem
    {
        public int Id { get; set; }

        public int TerminationRequestId { get; set; }

        public TerminationRequest? TerminationRequest { get; set; }

        public ClearanceCategory Category { get; set; }

        [MaxLength(120)]
        public string? ResponsiblePerson { get; set; }

        [MaxLength(100)]
        public string? ResponsibleDepartment { get; set; }

        /// <summary>Mandatory items block final completion until they are cleared.</summary>
        public bool IsMandatory { get; set; } = true;

        public ClearanceStatus Status { get; set; } = ClearanceStatus.Pending;

        [MaxLength(2000)]
        public string? OutstandingItems { get; set; }

        [MaxLength(2000)]
        public string? Remarks { get; set; }

        public DateOnly? CompletedDate { get; set; }

        public bool IsActive { get; set; } = true;

        public DateTime CreatedAt { get; set; }

        public DateTime? UpdatedAt { get; set; }

        public int? CreatedByUserId { get; set; }

        public User? CreatedByUser { get; set; }
    }
}
