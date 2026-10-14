using System.ComponentModel.DataAnnotations;

namespace GameHub.Models.Enums
{
    /// <summary>
    /// Screen 6 — Termination &amp; Clearance enums. Grouped in one file (as with
    /// the other HR screens) because they are only ever used together by the
    /// termination request, letter, and clearance workflow.
    /// </summary>
    public enum TerminationType
    {
        Resignation,
        Dismissal,
        Redundancy,

        [Display(Name = "Contract End")]
        ContractEnd,

        Retirement,

        [Display(Name = "Mutual Agreement")]
        MutualAgreement,

        Other
    }

    /// <summary>
    /// Lifecycle of a termination request:
    /// Draft -> PendingApproval -> Approved / Rejected -> InProgress -> Completed.
    /// </summary>
    public enum TerminationStatus
    {
        Draft,

        [Display(Name = "Pending Approval")]
        PendingApproval,

        Approved,

        Rejected,

        [Display(Name = "In Progress")]
        InProgress,

        Completed
    }

    /// <summary>Offboarding clearance areas tracked for every termination.</summary>
    public enum ClearanceCategory
    {
        [Display(Name = "IT Assets")]
        ITAssets,

        [Display(Name = "Department Handover")]
        DepartmentHandover,

        [Display(Name = "Finance / Dues")]
        Finance,

        Administration,

        [Display(Name = "Human Resources")]
        HR
    }

    /// <summary>
    /// Progress of a single clearance item:
    /// Pending -> InProgress -> Cleared, with Outstanding / Rejected where a
    /// department cannot clear the item.
    /// </summary>
    public enum ClearanceStatus
    {
        Pending,

        [Display(Name = "In Progress")]
        InProgress,

        Cleared,

        Outstanding,

        Rejected
    }
}
