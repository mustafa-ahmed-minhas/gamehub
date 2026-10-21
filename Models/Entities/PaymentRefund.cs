using GameHub.Models.Enums;
using System.ComponentModel.DataAnnotations;

namespace GameHub.Models.Entities
{
    public class PaymentRefund
    {
        public int Id { get; set; }

        [Required, MaxLength(30)]
        public string RefundNumber { get; set; } = string.Empty;

        public int PaymentTransactionId { get; set; }
        public int? PaymentId { get; set; }
        public int BookingId { get; set; }
        public int CustomerId { get; set; }

        public decimal Amount { get; set; }
        public RefundReason Reason { get; set; }
        [MaxLength(1500)] public string? ReasonDetails { get; set; }

        public RefundMethod Method { get; set; }
        public RefundStatus Status { get; set; } = RefundStatus.Pending;

        [MaxLength(100)] public string? GatewayRefundId { get; set; }
        [MaxLength(100)] public string? GatewayRefundReference { get; set; }
        [MaxLength(500)] public string? FailureReason { get; set; }

        public int InitiatedByUserId { get; set; }
        public int? ProcessedByUserId { get; set; }

        public DateTime InitiatedAt { get; set; }
        public DateTime? ProcessedAt { get; set; }
        public DateTime? CompletedAt { get; set; }
        public DateTime? FailedAt { get; set; }

        [MaxLength(128)] public string? IpAddress { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }

        public PaymentTransaction PaymentTransaction { get; set; } = null!;
        public Payment? Payment { get; set; }
        public Booking Booking { get; set; } = null!;
        public Customer Customer { get; set; } = null!;
        public User InitiatedByUser { get; set; } = null!;
        public User? ProcessedByUser { get; set; }
    }

    public enum RefundReason
    {
        [Display(Name = "Booking Rejected")] BookingRejected,
        [Display(Name = "Customer Cancellation")] CustomerCancellation,
        [Display(Name = "Court Unavailable")] CourtUnavailable,
        [Display(Name = "Duplicate Payment")] DuplicatePayment,
        [Display(Name = "Overpayment")] Overpayment,
        [Display(Name = "Service Not Rendered")] ServiceNotRendered,
        [Display(Name = "Other")] Other
    }

    public enum RefundMethod
    {
        [Display(Name = "Gateway Refund")] GatewayRefund,
        [Display(Name = "Manual Refund")] ManualRefund,
        [Display(Name = "Wallet Credit")] WalletCredit
    }

    public enum RefundStatus
    {
        [Display(Name = "Pending")] Pending,
        [Display(Name = "Processing")] Processing,
        [Display(Name = "Completed")] Completed,
        [Display(Name = "Failed")] Failed,
        [Display(Name = "Cancelled")] Cancelled
    }
}