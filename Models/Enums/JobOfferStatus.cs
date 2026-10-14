using System.ComponentModel.DataAnnotations;

namespace GameHub.Models.Enums
{
    /// <summary>
    /// Lifecycle of a job offer (Screen 3 — Offer &amp; Hiring).
    /// Draft and PendingAcceptance are in-flight; Accepted / Rejected /
    /// Withdrawn / Expired are terminal; Hired means the offer was converted
    /// into an employee record.
    /// </summary>
    public enum JobOfferStatus
    {
        Draft,

        [Display(Name = "Pending Acceptance")]
        PendingAcceptance,

        Accepted,

        Rejected,

        Withdrawn,

        Expired,

        Hired
    }
}
