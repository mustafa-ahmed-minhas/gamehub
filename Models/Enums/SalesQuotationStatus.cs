using System.ComponentModel.DataAnnotations;

namespace GameHub.Models.Enums
{
    public enum SalesQuotationStatus
    {
        Draft,
        [Display(Name = "Submitted For Approval")] SubmittedForApproval,
        Approved,
        [Display(Name = "Accepted By Customer")] AcceptedByCustomer,
        [Display(Name = "Converted To Sales Order")] ConvertedToSalesOrder,
        Rejected,
        [Display(Name = "Revised Draft")] RevisedDraft,
        Expired,
        Cancelled,
        // Legacy aliases keep existing customer-facing quotation actions compatible.
        [Display(Name = "Submitted For Approval")] ReadyForReview = SubmittedForApproval,
        [Display(Name = "Approved")] Sent = Approved,
        [Display(Name = "Approved")] Viewed = Approved,
        [Display(Name = "Accepted By Customer")] Accepted = AcceptedByCustomer
    }
}
