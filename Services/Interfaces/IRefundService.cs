using GameHub.Models.Entities;
using GameHub.Models.Enums;

namespace GameHub.Services.Interfaces
{
    public interface IRefundService
    {
        /// <summary>
        /// Initiates a refund for a rejected or cancelled booking.
        /// </summary>
        Task<PaymentRefund> InitiateRefundAsync(
            int bookingId,
            int paymentTransactionId,
            int? paymentId,
            int initiatedByUserId,
            RefundReason reason,
            string? reasonDetails,
            decimal amount,
            RefundMethod method = RefundMethod.GatewayRefund);

        /// <summary>
        /// Processes a pending refund through the payment gateway.
        /// </summary>
        Task<PaymentRefund> ProcessRefundAsync(int refundId, int processedByUserId);

        /// <summary>
        /// Completes a manual refund (for ManualRefund or WalletCredit methods).
        /// </summary>
        Task<PaymentRefund> CompleteManualRefundAsync(int refundId, int processedByUserId, string? gatewayRefundReference = null);

        /// <summary>
        /// Gets the remaining refundable amount for a booking/payment.
        /// </summary>
        Task<decimal> GetRefundableAmountAsync(int bookingId);

        /// <summary>
        /// Gets all refunds for a booking.
        /// </summary>
        Task<List<PaymentRefund>> GetRefundsForBookingAsync(int bookingId);
    }
}