using System.ComponentModel.DataAnnotations;

namespace GameHub.Models.Enums
{
    public enum InvoiceSourceType { Direct, [Display(Name = "Sales Order")] SalesOrder }
    public enum InvoiceStatus
    {
        Draft,
        [Display(Name = "Submitted For Approval")] SubmittedForApproval,
        Rejected,
        Approved,
        Issued,
        [Display(Name = "Partially Paid")] PartiallyPaid,
        Paid,
        Overdue,
        Cancelled,
        Void
    }
}
