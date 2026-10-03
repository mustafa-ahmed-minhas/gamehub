using GameHub.Data;
using GameHub.Models.Checkout;
using GameHub.Models.Entities;
using GameHub.Models.Enums;
using GameHub.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace GameHub.Services.Implementations
{
    public class InvoiceService : IInvoiceService
    {
        private const string MembershipInvoicePrefix = "MEM";

        private readonly ApplicationDbContext _dbContext;

        public InvoiceService(ApplicationDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public async Task<BookingInvoice> EnsureBookingInvoiceAsync(int bookingId)
        {
            var existing = await _dbContext.BookingInvoices
                .FirstOrDefaultAsync(x => x.BookingId == bookingId);
            if (existing != null) return existing;

            var booking = await _dbContext.Bookings
                .Include(x => x.Court)
                    .ThenInclude(x => x.Sport)
                .Include(x => x.Customer)
                .FirstOrDefaultAsync(x => x.Id == bookingId);

            if (booking == null) throw new InvalidOperationException("Booking not found.");

            var settings = await GetSettingsAsync();
            var court = booking.Court;

            var invoice = new BookingInvoice
            {
                InvoiceNumber = await GenerateNumberAsync(string.IsNullOrWhiteSpace(settings.InvoicePrefix) ? "INV" : settings.InvoicePrefix, _dbContext.BookingInvoices.Select(x => x.InvoiceNumber)),
                BookingId = booking.Id,
                CustomerId = booking.CustomerId,
                InvoiceDate = DateTime.UtcNow,
                Subtotal = booking.BaseAmount,
                DiscountAmount = booking.MembershipDiscountAmount + booking.ManualDiscountAmount,
                TaxAmount = booking.TaxAmount,
                TotalAmount = booking.TotalAmount,
                PaidAmount = booking.PaidAmount,
                BalanceAmount = booking.BalanceAmount,
                Currency = settings.Currency,
                CurrencySymbol = settings.CurrencySymbol ?? "Rs",
                ArenaName = settings.ArenaName,
                ArenaPhone = settings.Phone,
                ArenaEmail = settings.Email,
                ArenaAddress = settings.Address,
                CustomerName = booking.Customer.FullName,
                CustomerEmail = booking.Customer.Email ?? string.Empty,
                CustomerPhone = booking.Customer.PrimaryPhone,
                BookingNumber = booking.BookingNumber,
                CourtName = court?.Name ?? string.Empty,
                SportName = court?.Sport?.Name ?? string.Empty,
                BookingDate = booking.BookingDate,
                StartTime = booking.StartTime,
                EndTime = booking.EndTime,
                IsPaid = booking.BalanceAmount <= 0 && booking.PaidAmount > 0,
                CreatedAt = DateTime.UtcNow
            };

            _dbContext.BookingInvoices.Add(invoice);
            await _dbContext.SaveChangesAsync();
            return invoice;
        }

        public async Task<PaymentReceipt> EnsurePaymentReceiptAsync(int paymentTransactionId)
        {
            var existing = await _dbContext.PaymentReceipts
                .FirstOrDefaultAsync(x => x.PaymentTransactionId == paymentTransactionId);
            if (existing != null) return existing;

            var transaction = await _dbContext.PaymentTransactions
                .Include(x => x.Booking)
                    .ThenInclude(x => x!.Court)
                .Include(x => x.Customer)
                .FirstOrDefaultAsync(x => x.Id == paymentTransactionId);

            if (transaction == null) throw new InvalidOperationException("Payment transaction not found.");
            if (transaction.BookingId == null || transaction.Booking == null)
            {
                throw new InvalidOperationException("A booking receipt is only available for court bookings.");
            }

            var settings = await GetSettingsAsync();
            var booking = transaction.Booking;

            var receipt = new PaymentReceipt
            {
                ReceiptNumber = await GenerateNumberAsync(string.IsNullOrWhiteSpace(settings.ReceiptPrefix) ? "RCP" : settings.ReceiptPrefix, _dbContext.PaymentReceipts.Select(x => x.ReceiptNumber)),
                PaymentTransactionId = transaction.Id,
                BookingId = booking.Id,
                CustomerId = transaction.CustomerId,
                ReceiptDate = DateTime.UtcNow,
                AmountPaid = transaction.Amount,
                PaymentMethod = transaction.PaymentMethod,
                PaymentReference = transaction.GatewayReference ?? transaction.GatewayTransactionId,
                Currency = transaction.Currency,
                CurrencySymbol = transaction.CurrencySymbol,
                CustomerName = transaction.Customer.FullName,
                BookingNumber = booking.BookingNumber,
                ArenaName = settings.ArenaName,
                CreatedAt = DateTime.UtcNow
            };

            _dbContext.PaymentReceipts.Add(receipt);
            await _dbContext.SaveChangesAsync();
            return receipt;
        }

        public async Task<MembershipInvoice> EnsureMembershipInvoiceAsync(
            int customerMembershipId,
            MembershipOperation operation,
            decimal billedFee,
            string? previousPlanName = null,
            string? previousPlanCode = null)
        {
            var existing = await _dbContext.MembershipInvoices
                .FirstOrDefaultAsync(x => x.CustomerMembershipId == customerMembershipId);
            if (existing != null) return existing;

            var membership = await _dbContext.CustomerMemberships
                .Include(x => x.MembershipPlan)
                .Include(x => x.Customer)
                .FirstOrDefaultAsync(x => x.Id == customerMembershipId);

            if (membership == null) throw new InvalidOperationException("Membership not found.");

            var settings = await GetSettingsAsync();
            var plan = membership.MembershipPlan;

            // The linked payment transaction is the authoritative record of what was actually billed.
            // Each membership term (join, renew, upgrade) is its own record with its own transaction,
            // so the invoice always matches exactly one payment.
            var payment = membership.PaymentTransactionId.HasValue
                ? await _dbContext.PaymentTransactions
                    .AsNoTracking()
                    .FirstOrDefaultAsync(x => x.Id == membership.PaymentTransactionId.Value)
                : null;

            // Fall back to the fee implied by the operation only when there is no payment evidence,
            // so a legacy or manually created record still produces a sensible invoice.
            var fallbackFee = operation == MembershipOperation.Renew
                ? membership.RenewalFee
                : membership.JoiningFee;
            var feeAmount = Math.Round(billedFee > 0 ? billedFee : fallbackFee, 2);

            // Reuse the booking tax rule (SystemSettings.TaxPercentage on the taxable fee).
            var taxAmount = Math.Round(Math.Max(0, feeAmount) * settings.TaxPercentage / 100, 2);
            var expectedTotal = Math.Max(0, feeAmount) + taxAmount;

            // Only a settled, successful payment may be reported as paid. Records without payment
            // evidence stay unpaid rather than being assumed paid.
            var isPaid = payment != null && payment.Status == PaymentTransactionStatus.Succeeded;
            var totalAmount = isPaid ? payment!.Amount : expectedTotal;
            var paidAmount = isPaid ? payment!.Amount : 0m;
            var billedTax = isPaid ? Math.Max(0, totalAmount - feeAmount) : taxAmount;

            var invoice = new MembershipInvoice
            {
                InvoiceNumber = await GenerateNumberAsync(
                    string.IsNullOrWhiteSpace(settings.InvoicePrefix) ? MembershipInvoicePrefix : settings.InvoicePrefix + "-" + MembershipInvoicePrefix,
                    _dbContext.MembershipInvoices.Select(x => x.InvoiceNumber)),
                CustomerMembershipId = membership.Id,
                CustomerId = membership.CustomerId,
                PaymentTransactionId = membership.PaymentTransactionId,
                CheckoutToken = BuildCheckoutToken(membership),
                InvoiceDate = DateTime.UtcNow,
                PlanName = plan.Name,
                PlanCode = plan.Code,
                PlanDescription = Trim(plan.Description, 80),
                Operation = operation.ToString(),
                PreviousPlanName = Trim(previousPlanName, 100),
                PreviousPlanCode = Trim(previousPlanCode, 20),
                DurationMonths = plan.DurationMonths,
                Subtotal = feeAmount,
                TaxAmount = billedTax,
                TotalAmount = totalAmount,
                PaidAmount = paidAmount,
                BalanceAmount = Math.Max(0, totalAmount - paidAmount),
                Currency = settings.Currency,
                CurrencySymbol = settings.CurrencySymbol ?? "Rs",
                ArenaName = settings.ArenaName,
                ArenaPhone = settings.Phone,
                ArenaEmail = settings.Email,
                ArenaAddress = settings.Address,
                CustomerName = membership.Customer.FullName,
                CustomerEmail = membership.Customer.Email ?? string.Empty,
                CustomerPhone = membership.Customer.PrimaryPhone,
                MembershipNumber = membership.MembershipNumber,
                StartDate = membership.StartDate,
                ExpiryDate = membership.ExpiryDate,
                IsPaid = isPaid,
                CreatedAt = DateTime.UtcNow
            };

            _dbContext.MembershipInvoices.Add(invoice);
            await _dbContext.SaveChangesAsync();
            return invoice;
        }

        private static string BuildCheckoutToken(CustomerMembership membership) => "MEM-" + membership.Id.ToString("D6");

        private async Task<SystemSettings> GetSettingsAsync() =>
            await _dbContext.SystemSettings.AsNoTracking().FirstOrDefaultAsync()
            ?? new SystemSettings
            {
                ArenaName = "GameHub Arena",
                Currency = "PKR",
                CurrencySymbol = "Rs",
                InvoicePrefix = "INV",
                ReceiptPrefix = "RCP",
                Phone = string.Empty,
                Email = string.Empty
            };

        private async Task<string> GenerateNumberAsync(string prefix, IQueryable<string> source)
        {
            var latest = await source
                .Where(x => x.StartsWith(prefix + "-"))
                .OrderByDescending(x => x)
                .FirstOrDefaultAsync();

            var next = int.TryParse(latest?.Split('-').LastOrDefault(), out var n) ? n + 1 : 1;
            return $"{prefix}-{next:000000}";
        }

        private static string? Trim(string? value, int max) =>
            string.IsNullOrWhiteSpace(value) ? null : value.Length <= max ? value.Trim() : value.Trim()[..max];
    }
}
