using System.ComponentModel.DataAnnotations;

namespace GameHub.Models.Enums
{
    public enum DocumentType { SalesQuotation, SalesOrder, SalesInvoice }
    public enum ApprovalStatus { Draft, PendingApproval, Approved, Rejected, Cancelled }
    public enum ApprovalAction
    {
        Created,
        Submitted,
        Approved,
        Rejected,
        ReturnedForRevision,
        Resubmitted,
        CustomerAccepted,
        Converted,
        Confirmed,
        Fulfilled,
        Cancelled,
        PaymentRecorded
    }
}
