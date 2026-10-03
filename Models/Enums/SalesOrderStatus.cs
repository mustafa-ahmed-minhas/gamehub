using System.ComponentModel.DataAnnotations;

namespace GameHub.Models.Enums
{
    public enum SalesOrderStatus
    {
        Draft,
        [Display(Name = "Submitted For Approval")] SubmittedForApproval,
        Approved,
        Rejected,
        [Display(Name = "Revised Draft")] RevisedDraft,
        Confirmed,
        [Display(Name = "In Progress")] InProgress,
        Fulfilled,
        [Display(Name = "Ready For Invoice")] ReadyForInvoice,
        Closed,
        Cancelled
    }
}
