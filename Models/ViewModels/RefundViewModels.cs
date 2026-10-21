using GameHub.Helpers;
using GameHub.Models.Entities;
using GameHub.Models.Enums;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace GameHub.Models.ViewModels
{
    public class RefundListItemViewModel
    {
        public int Id { get; set; }
        public string RefundNumber { get; set; } = string.Empty;
        public string BookingNumber { get; set; } = string.Empty;
        public string CustomerName { get; set; } = string.Empty;
        public string CustomerCode { get; set; } = string.Empty;
        public string PaymentTransactionNumber { get; set; } = string.Empty;
        public RefundReason Reason { get; set; }
        public string ReasonDetails { get; set; } = string.Empty;
        public RefundMethod Method { get; set; }
        public RefundStatus Status { get; set; }
        public decimal Amount { get; set; }
        public string CurrencySymbol { get; set; } = "Rs";
        public string GatewayRefundId { get; set; } = string.Empty;
        public string GatewayRefundReference { get; set; } = string.Empty;
        public string FailureReason { get; set; } = string.Empty;
        public string InitiatedByName { get; set; } = string.Empty;
        public string ProcessedByName { get; set; } = string.Empty;
        public DateTime InitiatedAt { get; set; }
        public DateTime? ProcessedAt { get; set; }
        public DateTime? CompletedAt { get; set; }
        public DateTime? FailedAt { get; set; }
    }

    public class RefundsViewModel
    {
        public IReadOnlyList<RefundListItemViewModel> Items { get; set; } = Array.Empty<RefundListItemViewModel>();
        public string? Search { get; set; }
        public RefundStatus? Status { get; set; }
        public RefundMethod? Method { get; set; }
        public RefundReason? Reason { get; set; }
        public DateOnly? DateFrom { get; set; }
        public DateOnly? DateTo { get; set; }
        public string SortBy { get; set; } = "newest";
        public int CurrentPage { get; set; }
        public int PageSize { get; set; }
        public int TotalRecords { get; set; }
        public int TotalPages { get; set; }
        public string CurrencySymbol { get; set; } = "Rs";
        public bool CanManage { get; set; }
        public List<SelectListItem> StatusOptions { get; set; } = new();
        public List<SelectListItem> MethodOptions { get; set; } = new();
        public List<SelectListItem> ReasonOptions { get; set; } = new();

        // Pre-computed properties for simplified view
        public int ActiveFilterCount { get; set; }
        public int StartRecord { get; set; }
        public int EndRecord { get; set; }
        public int PageStart { get; set; }
        public int PageEnd { get; set; }
        public List<SelectListItem> SortOptions { get; set; } = new();
        public List<SelectListItem> PageSizeOptions { get; set; } = new();
        public List<PageLink> PaginationLinks { get; set; } = new();
        public Dictionary<string, string> CurrentQueryValues { get; set; } = new();
    }

    public class RefundDetailsViewModel
    {
        public PaymentRefund Refund { get; set; } = null!;
        public string CurrencySymbol { get; set; } = "Rs";
        public bool CanManage { get; set; }
        public Booking Booking { get; set; } = null!;
        public PaymentTransaction? PaymentTransaction { get; set; }
        public Payment? Payment { get; set; }
        public List<DocumentApproval> ApprovalHistory { get; set; } = new();
        public List<DocumentActivity> ActivityHistory { get; set; } = new();
        public string InitiatedByName => Refund?.InitiatedByUser?.FirstName + " " + Refund?.InitiatedByUser?.LastName ?? "—";
        public string ProcessedByName => Refund?.ProcessedByUser?.FirstName + " " + Refund?.ProcessedByUser?.LastName ?? "—";
        public string ReasonDisplayName => Refund?.Reason.GetDisplayName() ?? "—";
        public string MethodDisplayName => Refund?.Method.GetDisplayName() ?? "—";
        public string StatusDisplayName => Refund?.Status.GetDisplayName() ?? "—";
        
        // Pre-computed flags for simplified view
        public bool ShowRetryButton => Refund?.Status == RefundStatus.Pending && Refund?.Method == RefundMethod.GatewayRefund && CanManage;
        public bool ShowCompleteManualButton => Refund?.Status == RefundStatus.Pending && Refund?.Method == RefundMethod.ManualRefund && CanManage;
        public bool HasPayment => Payment != null;
        public bool HasInvoice => Payment?.Invoice != null;
        public bool HasApprovals => ApprovalHistory?.Any() == true;
        public bool HasActivities => ActivityHistory?.Any() == true;
        public bool HasPaymentTransaction => PaymentTransaction != null;
    }
}