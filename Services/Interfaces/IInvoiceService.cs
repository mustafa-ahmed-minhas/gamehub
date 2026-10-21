using GameHub.Data;
using GameHub.Models.Checkout;
using GameHub.Models.Entities;
using GameHub.Models.Enums;
using GameHub.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace GameHub.Services.Interfaces
{
    public interface IInvoiceService
    {
        Task<BookingInvoice> EnsureBookingInvoiceAsync(int bookingId);

        Task<PaymentReceipt> EnsurePaymentReceiptAsync(int paymentTransactionId);

        /// <summary>
        /// Creates the invoice for a membership term. <paramref name="billedFee"/> is the amount the
        /// customer was actually charged, so a renewal invoices the renewal fee and an upgrade
        /// invoices the price difference rather than a full new fee.
        /// </summary>
        Task<MembershipInvoice> EnsureMembershipInvoiceAsync(
            int customerMembershipId,
            MembershipOperation operation,
            decimal billedFee,
            string? previousPlanName = null,
            string? previousPlanCode = null);

        /// <summary>
        /// Creates Sales Order, Sales Invoice, and Payment records for an approved website booking.
        /// Links the existing PaymentTransaction to the new Sales records.
        /// </summary>
        Task<(SalesOrder SalesOrder, SalesInvoice Invoice, Payment Payment)?> CreateSalesRecordsForApprovedBookingAsync(
            int bookingId,
            int approvedByUserId,
            string? approvalComments = null);
    }
}
