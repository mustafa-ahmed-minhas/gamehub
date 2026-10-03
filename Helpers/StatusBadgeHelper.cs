using GameHub.Models.Enums;

namespace GameHub.Helpers
{
    /// <summary>
    /// Maps Sales Cycle workflow states onto the shared master status badge modifiers
    /// (membership-status-badge) so Quotations, Sales Orders, Invoices and Payments all
    /// use the exact same visual language as the Customer module.
    /// Presentation only: no business rules, calculations or persistence are involved.
    /// </summary>
    public static class StatusBadgeHelper
    {
        public const string BadgeClass = "membership-status-badge";

        public const string Active = "active";
        public const string Confirmed = "confirmed";
        public const string Pending = "pending";
        public const string Frozen = "frozen";
        public const string Suspended = "suspended";
        public const string Cancelled = "cancelled";
        public const string Inactive = "inactive";
        public const string Expired = "expired";

        public static string Modifier(this SalesQuotationStatus status) => status switch
        {
            SalesQuotationStatus.Draft => Pending,
            SalesQuotationStatus.SubmittedForApproval => Pending,
            SalesQuotationStatus.Approved => Active,
            SalesQuotationStatus.AcceptedByCustomer => Confirmed,
            SalesQuotationStatus.ConvertedToSalesOrder => Suspended,
            SalesQuotationStatus.Rejected => Cancelled,
            SalesQuotationStatus.RevisedDraft => Frozen,
            SalesQuotationStatus.Expired => Expired,
            SalesQuotationStatus.Cancelled => Cancelled,
            _ => Pending
        };

        public static string Modifier(this SalesOrderStatus status) => status switch
        {
            SalesOrderStatus.Draft => Pending,
            SalesOrderStatus.SubmittedForApproval => Pending,
            SalesOrderStatus.Approved => Active,
            SalesOrderStatus.Confirmed => Confirmed,
            SalesOrderStatus.InProgress => Active,
            SalesOrderStatus.Fulfilled => Confirmed,
            SalesOrderStatus.ReadyForInvoice => Suspended,
            SalesOrderStatus.Closed => Inactive,
            SalesOrderStatus.Rejected => Cancelled,
            SalesOrderStatus.RevisedDraft => Frozen,
            SalesOrderStatus.Cancelled => Cancelled,
            _ => Pending
        };

        public static string Modifier(this InvoiceStatus status) => status switch
        {
            InvoiceStatus.Draft => Pending,
            InvoiceStatus.SubmittedForApproval => Pending,
            InvoiceStatus.Approved => Active,
            InvoiceStatus.Issued => Confirmed,
            InvoiceStatus.PartiallyPaid => Frozen,
            InvoiceStatus.Paid => Confirmed,
            InvoiceStatus.Overdue => Expired,
            InvoiceStatus.Rejected => Cancelled,
            InvoiceStatus.Cancelled => Cancelled,
            InvoiceStatus.Void => Inactive,
            _ => Pending
        };

        public static string Modifier(this PaymentStatus status) => status switch
        {
            PaymentStatus.Unpaid => Pending,
            PaymentStatus.PartiallyPaid => Frozen,
            PaymentStatus.Paid => Confirmed,
            PaymentStatus.Refunded => Suspended,
            PaymentStatus.Waived => Inactive,
            _ => Pending
        };

        /// <summary>Full badge class list, e.g. "membership-status-badge active".</summary>
        public static string Badge(this SalesQuotationStatus status) => $"{BadgeClass} {status.Modifier()}";

        public static string Badge(this SalesOrderStatus status) => $"{BadgeClass} {status.Modifier()}";

        public static string Badge(this InvoiceStatus status) => $"{BadgeClass} {status.Modifier()}";

        public static string Badge(this PaymentStatus status) => $"{BadgeClass} {status.Modifier()}";

        /// <summary>Maps a workflow state name to a badge modifier, for string based states.</summary>
        public static string Modifier(string? state) => state?.Trim().ToLowerInvariant() switch
        {
            "draft" => Pending,
            "readyforreview" or "submittedforapproval" or "pendingapproval" => Pending,
            "sent" or "viewed" or "approved" or "inprogress" or "active" => Active,
            "accepted" or "acceptedbycustomer" or "confirmed" or "issued" or "paid" or "fulfilled" => Confirmed,
            "valid" or "created" or "not created" => Confirmed,
            "reviseddraft" or "partiallypaid" or "expiring" or "expiring soon" => Frozen,
            "convertedtosalesorder" or "readyforinvoice" or "ready for invoice" or "refunded" => Suspended,
            "rejected" or "cancelled" => Cancelled,
            "closed" or "void" or "waived" or "no order" => Inactive,
            "overdue" or "expired" => Expired,
            _ => Pending
        };

        public static string Badge(string? state) => $"{BadgeClass} {Modifier(state)}";
    }
}
