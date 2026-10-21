using GameHub.Data;
using GameHub.Models.Entities;
using GameHub.Models.Enums;
using GameHub.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace GameHub.Services.Implementations
{
    public class RefundService : IRefundService
    {
        private readonly ApplicationDbContext _dbContext;
        private readonly IPaymentGateway _paymentGateway;

        public RefundService(ApplicationDbContext dbContext, IPaymentGateway paymentGateway)
        {
            _dbContext = dbContext;
            _paymentGateway = paymentGateway;
        }

        public async Task<PaymentRefund> InitiateRefundAsync(
            int bookingId,
            int paymentTransactionId,
            int? paymentId,
            int initiatedByUserId,
            RefundReason reason,
            string? reasonDetails,
            decimal amount,
            RefundMethod method = RefundMethod.GatewayRefund)
        {
            var booking = await _dbContext.Bookings
                .Include(x => x.Customer)
                .Include(x => x.PaymentTransaction)
                .FirstOrDefaultAsync(x => x.Id == bookingId);

            if (booking == null)
                throw new InvalidOperationException("Booking not found.");

            var paymentTransaction = await _dbContext.PaymentTransactions
                .FirstOrDefaultAsync(x => x.Id == paymentTransactionId);

            if (paymentTransaction == null)
                throw new InvalidOperationException("Payment transaction not found.");

            if (paymentTransaction.Status != PaymentTransactionStatus.Succeeded)
                throw new InvalidOperationException("Only succeeded payments can be refunded.");

            // Validate refundable amount
            var refundableAmount = await GetRefundableAmountAsync(bookingId);
            if (amount <= 0 || amount > refundableAmount)
                throw new InvalidOperationException($"Refund amount must be between 0 and {refundableAmount:N2}.");

            // Check for duplicate pending/processing refunds
            var existingPending = await _dbContext.PaymentRefunds
                .AnyAsync(x => x.BookingId == bookingId && (x.Status == RefundStatus.Pending || x.Status == RefundStatus.Processing));
            if (existingPending)
                throw new InvalidOperationException("A refund is already pending or processing for this booking.");

            var settings = await GetSettingsAsync();
            var now = DateTime.UtcNow;
            var refundNumber = await GenerateRefundNumberAsync(settings);

            var refund = new PaymentRefund
            {
                RefundNumber = refundNumber,
                PaymentTransactionId = paymentTransactionId,
                PaymentId = paymentId,
                BookingId = bookingId,
                CustomerId = booking.CustomerId,
                Amount = amount,
                Reason = reason,
                ReasonDetails = reasonDetails,
                Method = method,
                Status = method == RefundMethod.GatewayRefund ? RefundStatus.Pending : RefundStatus.Processing,
                InitiatedByUserId = initiatedByUserId,
                InitiatedAt = now,
                IpAddress = string.Empty,
                CreatedAt = now
            };

            _dbContext.PaymentRefunds.Add(refund);

            var currency = settings.Currency;
            var currencySymbol = settings.CurrencySymbol ?? "Rs";

            // Record audit trail
            _dbContext.DocumentApprovals.Add(new DocumentApproval
            {
                DocumentType = DocumentType.Booking,
                DocumentId = booking.Id,
                DocumentNumber = booking.BookingNumber,
                RevisionNumber = 1,
                Action = ApprovalAction.RefundInitiated,
                PreviousStatus = booking.Status.ToString(),
                NewStatus = "RefundInitiated",
                SubmittedByUserId = initiatedByUserId,
                ActionByUserId = initiatedByUserId,
                ActionAt = now,
                Comments = $"Refund initiated: {reason.ToString()}. {reasonDetails ?? string.Empty}".Trim(),
                FinancialTotal = amount,
                Currency = currency,
                IsFinal = false,
                IpAddress = string.Empty,
                CreatedAt = now
            });

            _dbContext.DocumentActivities.Add(new DocumentActivity
            {
                DocumentType = DocumentType.Booking,
                DocumentId = booking.Id,
                ActivityType = ApprovalAction.RefundInitiated,
                Description = $"Refund {refundNumber} initiated for booking {booking.BookingNumber}. Amount: {currencySymbol} {amount:N2}. Reason: {reason.ToString()}.",
                PerformedByUserId = initiatedByUserId,
                PerformedAt = now
            });

            // Notify customer
            _dbContext.CustomerNotifications.Add(new CustomerNotification
            {
                CustomerId = booking.CustomerId,
                BookingId = booking.Id,
                Type = CustomerNotificationType.RefundInitiated,
                Title = "Refund Initiated",
                Message = $"A refund of {currencySymbol} {amount:N2} has been initiated for your booking {booking.BookingNumber}. Reason: {reason.ToString()}.",
                ActionUrl = $"/MyBookings/Details/{booking.Id}",
                CreatedAt = now
            });

            await _dbContext.SaveChangesAsync();

            // If gateway refund, process immediately
            if (method == RefundMethod.GatewayRefund)
            {
                await ProcessRefundAsync(refund.Id, initiatedByUserId);
            }

            return refund;
        }

        public async Task<PaymentRefund> ProcessRefundAsync(int refundId, int processedByUserId)
        {
            var refund = await _dbContext.PaymentRefunds
                .Include(x => x.PaymentTransaction)
                .Include(x => x.Booking).ThenInclude(x => x.Customer)
                .FirstOrDefaultAsync(x => x.Id == refundId);

            if (refund == null)
                throw new InvalidOperationException("Refund not found.");

            if (refund.Status != RefundStatus.Pending && refund.Status != RefundStatus.Processing)
                throw new InvalidOperationException($"Refund is not in a processable state: {refund.Status}.");

            var now = DateTime.UtcNow;
            var settings = await GetSettingsAsync();
            var currency = settings.Currency;
            var currencySymbol = settings.CurrencySymbol ?? "Rs";

            refund.Status = RefundStatus.Processing;
            refund.ProcessedByUserId = processedByUserId;
            refund.ProcessedAt = now;
            refund.UpdatedAt = now;

            await _dbContext.SaveChangesAsync();

            if (refund.Method == RefundMethod.GatewayRefund)
            {
                var gatewayResult = await _paymentGateway.RefundAsync(
                    refund.PaymentTransaction.GatewayTransactionId,
                    refund.Amount,
                    refund.PaymentTransaction.Currency,
                    $"Refund {refund.RefundNumber}: {refund.Reason.ToString()}. {refund.ReasonDetails ?? string.Empty}".Trim()
                );

                refund.GatewayRefundId = gatewayResult.GatewayRefundId;
                refund.GatewayRefundReference = gatewayResult.GatewayRefundReference;
                refund.UpdatedAt = now;

                if (!gatewayResult.Succeeded)
                {
                    refund.Status = RefundStatus.Failed;
                    refund.FailureReason = gatewayResult.FailureReason;
                    refund.FailedAt = now;

                    _dbContext.DocumentApprovals.Add(new DocumentApproval
                    {
                        DocumentType = DocumentType.Booking,
                        DocumentId = refund.BookingId,
                        DocumentNumber = refund.Booking.BookingNumber,
                        RevisionNumber = 1,
                        Action = ApprovalAction.RefundFailed,
                        PreviousStatus = "RefundProcessing",
                        NewStatus = "RefundFailed",
                        SubmittedByUserId = processedByUserId,
                        ActionByUserId = processedByUserId,
                        ActionAt = now,
                        Comments = $"Gateway refund failed: {gatewayResult.FailureReason}",
                        FinancialTotal = refund.Amount,
                        Currency = currency,
                        IsFinal = false,
                        IpAddress = string.Empty,
                        CreatedAt = now
                    });

                    _dbContext.CustomerNotifications.Add(new CustomerNotification
                    {
                        CustomerId = refund.CustomerId,
                        BookingId = refund.BookingId,
                        Type = CustomerNotificationType.RefundFailed,
                        Title = "Refund Failed",
                        Message = $"The refund for booking {refund.Booking.BookingNumber} could not be processed automatically. Reason: {gatewayResult.FailureReason}. Please contact the arena.",
                        ActionUrl = $"/MyBookings/Details/{refund.BookingId}",
                        CreatedAt = now
                    });

                    await _dbContext.SaveChangesAsync();
                    return refund;
                }
            }

            // Gateway refund succeeded or manual/wallet method
            refund.Status = RefundStatus.Completed;
            refund.CompletedAt = now;
            refund.UpdatedAt = now;

            // Update PaymentTransaction status
            // Only set to Refunded for full refunds; for partial refunds, keep as Succeeded
            var isFullRefund = refund.Amount >= refund.Booking.PaidAmount - 0.01m; // Allow small rounding difference
            if (isFullRefund)
            {
                refund.PaymentTransaction.Status = PaymentTransactionStatus.Refunded;
                refund.PaymentTransaction.RefundedAt = now;
            }
            refund.PaymentTransaction.UpdatedAt = now;

            // Restore membership hours for full refunds
            if (isFullRefund)
            {
                await RestoreMembershipHoursAsync(refund.BookingId);
            }

            // Record audit trail
            _dbContext.DocumentApprovals.Add(new DocumentApproval
            {
                DocumentType = DocumentType.Booking,
                DocumentId = refund.BookingId,
                DocumentNumber = refund.Booking.BookingNumber,
                RevisionNumber = 1,
                Action = ApprovalAction.RefundCompleted,
                PreviousStatus = "RefundProcessing",
                NewStatus = "RefundCompleted",
                SubmittedByUserId = processedByUserId,
                ActionByUserId = processedByUserId,
                ActionAt = now,
                Comments = $"Refund {refund.RefundNumber} completed via {refund.Method.ToString()}.",
                FinancialTotal = refund.Amount,
                Currency = currency,
                IsFinal = true,
                IpAddress = string.Empty,
                CreatedAt = now
            });

            _dbContext.DocumentActivities.Add(new DocumentActivity
            {
                DocumentType = DocumentType.Booking,
                DocumentId = refund.BookingId,
                ActivityType = ApprovalAction.RefundCompleted,
                Description = $"Refund {refund.RefundNumber} completed. Amount: {currencySymbol} {refund.Amount:N2}.",
                PerformedByUserId = processedByUserId,
                PerformedAt = now
            });

            _dbContext.CustomerNotifications.Add(new CustomerNotification
            {
                CustomerId = refund.CustomerId,
                BookingId = refund.BookingId,
                Type = CustomerNotificationType.RefundCompleted,
                Title = "Refund Completed",
                Message = $"Your refund of {currencySymbol} {refund.Amount:N2} for booking {refund.Booking.BookingNumber} has been completed.",
                ActionUrl = $"/MyBookings/Details/{refund.BookingId}",
                CreatedAt = now
            });

            await _dbContext.SaveChangesAsync();
            return refund;
        }

        public async Task<PaymentRefund> CompleteManualRefundAsync(int refundId, int processedByUserId, string? gatewayRefundReference = null)
        {
            var refund = await _dbContext.PaymentRefunds
                .Include(x => x.Booking).ThenInclude(x => x.Customer)
                .Include(x => x.PaymentTransaction)
                .Include(x => x.Payment).ThenInclude(x => x.Invoice).ThenInclude(x => x.Customer)
                .FirstOrDefaultAsync(x => x.Id == refundId);

            if (refund == null)
                throw new InvalidOperationException("Refund not found.");

            if (refund.Method != RefundMethod.ManualRefund && refund.Method != RefundMethod.WalletCredit)
                throw new InvalidOperationException("This method is only for manual or wallet credit refunds.");

            if (refund.Status != RefundStatus.Pending && refund.Status != RefundStatus.Processing)
                throw new InvalidOperationException($"Refund is not in a completable state: {refund.Status}.");

            var now = DateTime.UtcNow;
            var settings = await GetSettingsAsync();
            var currency = settings.Currency;
            var currencySymbol = settings.CurrencySymbol ?? "Rs";

            refund.Status = RefundStatus.Completed;
            refund.ProcessedByUserId = processedByUserId;
            refund.ProcessedAt = now;
            refund.CompletedAt = now;
            refund.GatewayRefundReference = gatewayRefundReference ?? $"MANUAL-{refund.RefundNumber}";
            refund.UpdatedAt = now;

            // Update PaymentTransaction status
            // Only set to Refunded for full refunds; for partial refunds, keep as Succeeded
            var isFullRefund = refund.Amount >= refund.Booking.PaidAmount - 0.01m; // Allow small rounding difference
            if (isFullRefund)
            {
                refund.PaymentTransaction.Status = PaymentTransactionStatus.Refunded;
                refund.PaymentTransaction.RefundedAt = now;
            }
            refund.PaymentTransaction.UpdatedAt = now;

            // Restore membership hours for full refunds
            if (isFullRefund)
            {
                await RestoreMembershipHoursAsync(refund.BookingId);
            }

            // If linked to a Payment record, update its status and allocations
            if (refund.PaymentId.HasValue && refund.Payment != null)
            {
                var payment = refund.Payment;
                // Only set Payment status to Refunded for full refunds
                if (isFullRefund)
                {
                    payment.Status = PaymentRecordStatus.Refunded;
                }
                payment.UpdatedAt = now;

                var invoice = payment.Invoice;
                if (invoice != null)
                {
                    invoice.PaidAmount = Math.Max(0, Math.Round(invoice.PaidAmount - refund.Amount, 2));
                    invoice.OutstandingAmount = Math.Round(invoice.GrandTotal - invoice.PaidAmount, 2);
                    invoice.Status = invoice.OutstandingAmount > 0 ? InvoiceStatus.PartiallyPaid : InvoiceStatus.Paid;
                    invoice.UpdatedAt = now;
                    invoice.UpdatedByUserId = processedByUserId;

                    invoice.Customer.OutstandingBalance = Math.Max(0, Math.Round(invoice.Customer.OutstandingBalance + refund.Amount, 2));
                }
            }

            // Update booking payment status - only set to Refunded for full refunds
            if (isFullRefund)
            {
                refund.Booking.PaymentStatus = PaymentStatus.Refunded;
                refund.Booking.BalanceAmount = refund.Booking.TotalAmount;
            }
            else
            {
                // For partial refunds, update balance amount
                refund.Booking.BalanceAmount = Math.Round(refund.Booking.TotalAmount - (refund.Booking.PaidAmount - refund.Amount), 2);
            }
            refund.Booking.UpdatedAt = now;
            refund.Booking.UpdatedByUserId = processedByUserId;

            // Record audit trail
            _dbContext.DocumentApprovals.Add(new DocumentApproval
            {
                DocumentType = DocumentType.Booking,
                DocumentId = refund.BookingId,
                DocumentNumber = refund.Booking.BookingNumber,
                RevisionNumber = 1,
                Action = ApprovalAction.RefundCompleted,
                PreviousStatus = "RefundProcessing",
                NewStatus = "RefundCompleted",
                SubmittedByUserId = processedByUserId,
                ActionByUserId = processedByUserId,
                ActionAt = now,
                Comments = $"Manual refund {refund.RefundNumber} completed. {refund.ReasonDetails ?? string.Empty}".Trim(),
                FinancialTotal = refund.Amount,
                Currency = currency,
                IsFinal = true,
                IpAddress = string.Empty,
                CreatedAt = now
            });

            _dbContext.DocumentActivities.Add(new DocumentActivity
            {
                DocumentType = DocumentType.Booking,
                DocumentId = refund.BookingId,
                ActivityType = ApprovalAction.RefundCompleted,
                Description = $"Manual refund {refund.RefundNumber} completed. Amount: {currencySymbol} {refund.Amount:N2}.",
                PerformedByUserId = processedByUserId,
                PerformedAt = now
            });

            _dbContext.CustomerNotifications.Add(new CustomerNotification
            {
                CustomerId = refund.CustomerId,
                BookingId = refund.BookingId,
                Type = CustomerNotificationType.RefundCompleted,
                Title = "Refund Completed",
                Message = $"Your refund of {currencySymbol} {refund.Amount:N2} for booking {refund.Booking.BookingNumber} has been completed.",
                ActionUrl = $"/MyBookings/Details/{refund.BookingId}",
                CreatedAt = now
            });

            await _dbContext.SaveChangesAsync();
            return refund;
        }

        public async Task<decimal> GetRefundableAmountAsync(int bookingId)
        {
            var booking = await _dbContext.Bookings
                .Include(x => x.PaymentTransaction)
                .Include(x => x.SalesOrder)
                .FirstOrDefaultAsync(x => x.Id == bookingId);

            if (booking == null)
                return 0;

            // Calculate total paid amount from PaymentAllocations on the SalesInvoice
            // This is more accurate than booking.PaidAmount which may not be updated from allocations
            decimal totalPaid = 0;

            if (booking.SalesOrderId.HasValue && booking.SalesOrder != null && booking.SalesOrder.InvoiceId.HasValue)
            {
                var invoice = await _dbContext.SalesInvoices
                    .Include(x => x.PaymentAllocations)
                    .FirstOrDefaultAsync(x => x.Id == booking.SalesOrder.InvoiceId.Value);

                if (invoice != null)
                {
                    totalPaid = invoice.PaymentAllocations.Sum(x => x.Amount);
                }
            }
            else if (booking.PaymentTransaction != null)
            {
                // Fallback for bookings without SalesOrder (legacy or non-integrated)
                totalPaid = booking.PaidAmount;
            }

            // Already refunded amount (only completed refunds)
            var refundedAmount = await _dbContext.PaymentRefunds
                .Where(x => x.BookingId == bookingId && x.Status == RefundStatus.Completed)
                .SumAsync(x => (decimal?)x.Amount) ?? 0;

            return Math.Max(0, totalPaid - refundedAmount);
        }

        public async Task<List<PaymentRefund>> GetRefundsForBookingAsync(int bookingId)
        {
            return await _dbContext.PaymentRefunds
                .Include(x => x.PaymentTransaction)
                .Include(x => x.Payment)
                .Include(x => x.InitiatedByUser)
                .Include(x => x.ProcessedByUser)
                .Where(x => x.BookingId == bookingId)
                .OrderByDescending(x => x.CreatedAt)
                .ToListAsync();
        }

        private async Task<string> GenerateRefundNumberAsync(SystemSettings settings)
        {
            var prefix = "REF";
            var latest = await _dbContext.PaymentRefunds
                .Where(x => x.RefundNumber.StartsWith(prefix + "-"))
                .OrderByDescending(x => x.RefundNumber)
                .Select(x => x.RefundNumber)
                .FirstOrDefaultAsync();
            var next = int.TryParse(latest?.Split('-').LastOrDefault(), out var value) ? value + 1 : 1;
            return $"{prefix}-{next:000000}";
        }

        private async Task<SystemSettings> GetSettingsAsync() =>
            await _dbContext.SystemSettings.AsNoTracking().FirstOrDefaultAsync()
            ?? new SystemSettings
            {
                ArenaName = "GameHub Arena",
                Currency = "PKR",
                CurrencySymbol = "Rs",
                TaxPercentage = 0,
                BookingPrefix = "BKG",
                InvoicePrefix = "INV",
                ReceiptPrefix = "RCP",
                Phone = "",
                Email = ""
            };

        private async Task RestoreMembershipHoursAsync(int bookingId)
        {
            var booking = await _dbContext.Bookings
                .Include(x => x.CustomerMembership)
                .FirstOrDefaultAsync(x => x.Id == bookingId);

            if (booking == null || booking.CustomerMembershipId == null)
                return;

            // Only restore if membership hours were previously applied
            if (!booking.MembershipHoursApplied)
                return;

            var membership = await _dbContext.CustomerMemberships
                .FirstOrDefaultAsync(x => x.Id == booking.CustomerMembershipId.Value);

            if (membership == null)
                return;

            // Calculate hours to restore (same logic as ApplyMembershipHoursIfNeededAsync)
            var hoursToRestore = Math.Max(1, (int)Math.Ceiling(booking.DurationMinutes / 60m));
            membership.UsedBookingHours = Math.Max(0, membership.UsedBookingHours - hoursToRestore);
            membership.UpdatedAt = DateTime.UtcNow;

            // Reset the membership hours applied flag on the booking
            booking.MembershipHoursApplied = false;
            booking.UpdatedAt = DateTime.UtcNow;
        }
    }
}