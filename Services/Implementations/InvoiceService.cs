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

        public async Task<(SalesOrder SalesOrder, SalesInvoice Invoice, Payment Payment)?> CreateSalesRecordsForApprovedBookingAsync(
            int bookingId,
            int approvedByUserId,
            string? approvalComments = null)
        {
            var booking = await _dbContext.Bookings
                .Include(x => x.Customer)
                .Include(x => x.Court).ThenInclude(x => x.Facility)
                .Include(x => x.Court).ThenInclude(x => x.Sport)
                .Include(x => x.Sport)
                .Include(x => x.Facility)
                .Include(x => x.PaymentTransaction)
                .Include(x => x.CustomerMembership)
                .FirstOrDefaultAsync(x => x.Id == bookingId && x.IsActive);

            if (booking == null)
                throw new InvalidOperationException("Booking not found.");

            if (booking.Status != BookingStatus.Confirmed)
                throw new InvalidOperationException("Booking must be in Confirmed status.");

            if (booking.PaymentTransaction == null || booking.PaymentTransaction.Status != PaymentTransactionStatus.Succeeded)
                throw new InvalidOperationException("Payment transaction not found or not succeeded.");

            // Check if already linked to a Sales Order
            if (booking.SalesOrderId.HasValue)
            {
                var existingOrder = await _dbContext.SalesOrders
                    .Include(x => x.Items)
                    .FirstOrDefaultAsync(x => x.Id == booking.SalesOrderId.Value);
                if (existingOrder != null)
                {
                    var existingInvoice = existingOrder.InvoiceCreated
                        ? await _dbContext.SalesInvoices.Include(x => x.Items).FirstOrDefaultAsync(x => x.Id == existingOrder.InvoiceId)
                        : null;
                    var existingPayment = existingInvoice != null
                        ? await _dbContext.Payments.Include(x => x.Allocations).FirstOrDefaultAsync(x => x.InvoiceId == existingInvoice.Id)
                        : null;
                    return (existingOrder, existingInvoice!, existingPayment!);
                }
            }

            var settings = await GetSettingsAsync();
            var now = DateTime.UtcNow;

            // Create Sales Order
            var salesOrder = new SalesOrder
            {
                SalesOrderNumber = await GenerateSalesOrderNumberAsync(settings),
                CustomerId = booking.CustomerId,
                AssignedToUserId = approvedByUserId,
                OrderDate = DateOnly.FromDateTime(now),
                ServiceStartDate = booking.BookingDate,
                ServiceEndDate = booking.BookingDate,
                Status = SalesOrderStatus.Confirmed,
                ConfirmedAt = now,
                Subject = $"Court booking: {booking.Court?.Name} - {booking.Sport?.Name}",
                CustomerNotes = booking.CustomerNotes,
                InternalNotes = $"Auto-created from approved website booking {booking.BookingNumber}. {approvalComments ?? string.Empty}".Trim(),
                Currency = settings.Currency,
                CurrencySymbol = settings.CurrencySymbol ?? "Rs",
                SourceType = "WebsiteBooking",
                CreatedByUserId = approvedByUserId,
                CreatedAt = now,
                IsActive = true
            };

            // Create Sales Order Item from booking
            var salesOrderItem = new SalesOrderItem
            {
                LineNumber = 1,
                ItemType = SalesItemType.CourtBooking,
                SportId = booking.SportId,
                FacilityId = booking.FacilityId,
                CourtId = booking.CourtId,
                Description = $"{booking.Court?.Name} ({booking.Facility?.Name}) - {booking.Sport?.Name} on {booking.BookingDate:dd MMM yyyy} {booking.StartTime:HH:mm}-{booking.EndTime:HH:mm}",
                Quantity = 1,
                UnitOfMeasure = "Session",
                UnitPrice = booking.BaseAmount,
                DiscountPercentage = booking.BaseAmount > 0 ? Math.Round(booking.MembershipDiscountAmount / booking.BaseAmount * 100, 2) : 0,
                DiscountAmount = booking.MembershipDiscountAmount,
                TaxPercentage = settings.TaxPercentage,
                TaxAmount = booking.TaxAmount,
                LineSubtotal = booking.BaseAmount,
                LineTotal = booking.TotalAmount,
                ServiceDate = booking.BookingDate,
                StartTime = booking.StartTime,
                EndTime = booking.EndTime,
                DisplayOrder = 1,
                CreatedAt = now
            };
            salesOrder.Items.Add(salesOrderItem);

            salesOrder.Subtotal = booking.BaseAmount;
            salesOrder.DiscountTotal = booking.MembershipDiscountAmount;
            salesOrder.TaxTotal = booking.TaxAmount;
            salesOrder.GrandTotal = booking.TotalAmount;

            _dbContext.SalesOrders.Add(salesOrder);

            // Persist the sales order first so its generated key is available for related records
            await _dbContext.SaveChangesAsync();

            // Link booking to sales order
            booking.SalesOrderId = salesOrder.Id;
            booking.UpdatedAt = now;
            booking.UpdatedByUserId = approvedByUserId;

            // Create Sales Invoice from Sales Order
            var salesInvoice = new SalesInvoice
            {
                InvoiceNumber = await GenerateInvoiceNumberAsync(settings),
                SourceType = InvoiceSourceType.WebsiteBooking,
                SalesOrderId = salesOrder.Id,
                CustomerId = booking.CustomerId,
                InvoiceDate = DateOnly.FromDateTime(now),
                DueDate = DateOnly.FromDateTime(now),
                Subject = salesOrder.Subject,
                CustomerNotes = booking.CustomerNotes,
                InternalNotes = $"Auto-created from approved website booking {booking.BookingNumber}.",
                Currency = settings.Currency,
                CurrencySymbol = settings.CurrencySymbol ?? "Rs",
                Status = InvoiceStatus.Issued,
                IssuedAt = now,
                IsLocked = true,
                CreatedByUserId = approvedByUserId,
                CreatedAt = now,
                IsActive = true
            };

            var salesInvoiceItem = new SalesInvoiceItem
            {
                LineNumber = 1,
                ItemType = SalesItemType.CourtBooking,
                SportId = booking.SportId,
                FacilityId = booking.FacilityId,
                CourtId = booking.CourtId,
                Description = salesOrderItem.Description,
                Quantity = 1,
                UnitOfMeasure = "Session",
                UnitPrice = booking.BaseAmount,
                DiscountPercentage = salesOrderItem.DiscountPercentage,
                DiscountAmount = booking.MembershipDiscountAmount,
                TaxPercentage = settings.TaxPercentage,
                TaxAmount = booking.TaxAmount,
                LineSubtotal = booking.BaseAmount,
                LineTotal = booking.TotalAmount,
                ServiceDate = booking.BookingDate,
                StartTime = booking.StartTime,
                EndTime = booking.EndTime,
                DisplayOrder = 1,
                CreatedAt = now
            };
            salesInvoice.Items.Add(salesInvoiceItem);

            salesInvoice.Subtotal = booking.BaseAmount;
            salesInvoice.DiscountTotal = booking.MembershipDiscountAmount;
            salesInvoice.TaxTotal = booking.TaxAmount;
            salesInvoice.AdjustmentAmount = 0;
            salesInvoice.GrandTotal = booking.TotalAmount;
            salesInvoice.PaidAmount = booking.PaidAmount;
            // OutstandingAmount will be calculated as GrandTotal - PaidAmount
            salesInvoice.OutstandingAmount = salesInvoice.GrandTotal - salesInvoice.PaidAmount;

            _dbContext.SalesInvoices.Add(salesInvoice);

            // Persist the invoice first so its generated key is available for the sales order link and payment
            await _dbContext.SaveChangesAsync();

            // Update Sales Order with Invoice link
            salesOrder.InvoiceCreated = true;
            salesOrder.InvoiceId = salesInvoice.Id;
            salesOrder.UpdatedAt = now;
            salesOrder.UpdatedByUserId = approvedByUserId;

            // Create Payment record linked to the invoice
            var payment = new Payment
            {
                PaymentNumber = await GeneratePaymentNumberAsync(),
                InvoiceId = salesInvoice.Id,
                CustomerId = booking.CustomerId,
                PaymentDate = DateOnly.FromDateTime(booking.PaidAt ?? now),
                Amount = booking.PaidAmount,
                PaymentMethod = MapPaymentMethod(booking.PaymentTransaction.PaymentMethod),
                ReferenceNumber = booking.PaymentTransaction.GatewayReference ?? booking.PaymentTransaction.GatewayTransactionId,
                Notes = $"Auto-recorded from online payment transaction {booking.PaymentTransaction.TransactionNumber} for booking {booking.BookingNumber}.",
                ReceivedByUserId = approvedByUserId,
                CreatedByUserId = approvedByUserId,
                Currency = settings.Currency,
                CurrencySymbol = settings.CurrencySymbol ?? "Rs",
                Status = PaymentRecordStatus.Recorded,
                CreatedAt = now
            };

            payment.Allocations.Add(new PaymentAllocation
            {
                InvoiceId = salesInvoice.Id,
                Amount = booking.PaidAmount,
                CreatedAt = now
            });

            _dbContext.Payments.Add(payment);

            // Update customer outstanding balance
            booking.Customer.OutstandingBalance = Math.Max(0, Math.Round(booking.Customer.OutstandingBalance - booking.PaidAmount, 2));

            // Record document approvals and activities
            await RecordSalesOrderApprovalAsync(salesOrder, approvedByUserId, approvalComments, now);
            await RecordInvoiceApprovalAsync(salesInvoice, approvedByUserId, now);
            await RecordPaymentActivityAsync(salesInvoice, payment, approvedByUserId, now);

            // Do NOT call SaveChangesAsync here - let the caller manage the transaction
            return (salesOrder, salesInvoice, payment);
        }

        private async Task<string> GenerateSalesOrderNumberAsync(SystemSettings settings)
        {
            var prefix = string.IsNullOrWhiteSpace(settings.SalesOrderPrefix) ? "SO" : settings.SalesOrderPrefix.Trim().ToUpperInvariant();
            var candidates = await _dbContext.SalesOrders
                .Where(x => x.SalesOrderNumber.StartsWith(prefix + "-"))
                .Select(x => x.SalesOrderNumber)
                .ToListAsync();
            var next = 1;
            foreach (var candidate in candidates)
            {
                var suffix = candidate.Substring(prefix.Length + 1);
                if (int.TryParse(suffix, out var value) && value >= next)
                {
                    next = value + 1;
                }
            }
            return $"{prefix}-{next:000000}";
        }

        private async Task<string> GenerateInvoiceNumberAsync(SystemSettings settings)
        {
            var prefix = string.IsNullOrWhiteSpace(settings.InvoicePrefix) ? "INV" : settings.InvoicePrefix.Trim().ToUpperInvariant();
            var candidates = await _dbContext.SalesInvoices
                .Where(x => x.InvoiceNumber.StartsWith(prefix + "-"))
                .Select(x => x.InvoiceNumber)
                .ToListAsync();
            var next = 1;
            foreach (var candidate in candidates)
            {
                var suffix = candidate.Substring(prefix.Length + 1);
                if (int.TryParse(suffix, out var value) && value >= next)
                {
                    next = value + 1;
                }
            }
            return $"{prefix}-{next:000000}";
        }

        private async Task<string> GeneratePaymentNumberAsync()
        {
            var prefix = "PAY";
            var candidates = await _dbContext.Payments
                .Where(x => x.PaymentNumber.StartsWith(prefix + "-"))
                .Select(x => x.PaymentNumber)
                .ToListAsync();
            var next = 1;
            foreach (var candidate in candidates)
            {
                var suffix = candidate.Substring(prefix.Length + 1);
                if (int.TryParse(suffix, out var value) && value >= next)
                {
                    next = value + 1;
                }
            }
            return $"{prefix}-{next:000000}";
        }

        private static InvoicePaymentMethod MapPaymentMethod(OnlinePaymentMethod method) => method switch
        {
            OnlinePaymentMethod.Card => InvoicePaymentMethod.OnlinePayment,
            OnlinePaymentMethod.MobileWallet => InvoicePaymentMethod.OnlinePayment,
            OnlinePaymentMethod.BankTransfer => InvoicePaymentMethod.BankTransfer,
            _ => InvoicePaymentMethod.OnlinePayment
        };

        private async Task RecordSalesOrderApprovalAsync(SalesOrder order, int userId, string? comments, DateTime now)
        {
            _dbContext.DocumentApprovals.Add(new DocumentApproval
            {
                DocumentType = DocumentType.SalesOrder,
                DocumentId = order.Id,
                DocumentNumber = order.SalesOrderNumber,
                RevisionNumber = order.RevisionNumber,
                Action = ApprovalAction.Confirmed,
                PreviousStatus = SalesOrderStatus.Draft.ToString(),
                NewStatus = SalesOrderStatus.Confirmed.ToString(),
                SubmittedByUserId = userId,
                ActionByUserId = userId,
                SubmittedAt = now,
                ActionAt = now,
                Comments = comments,
                FinancialTotal = order.GrandTotal,
                Currency = order.Currency,
                IsFinal = true,
                IpAddress = string.Empty,
                CreatedAt = now
            });

            _dbContext.DocumentActivities.Add(new DocumentActivity
            {
                DocumentType = DocumentType.SalesOrder,
                DocumentId = order.Id,
                ActivityType = ApprovalAction.Confirmed,
                Description = $"Sales Order {order.SalesOrderNumber} confirmed from approved website booking.",
                PerformedByUserId = userId,
                PerformedAt = now
            });
        }

        private async Task RecordInvoiceApprovalAsync(SalesInvoice invoice, int userId, DateTime now)
        {
            _dbContext.DocumentApprovals.Add(new DocumentApproval
            {
                DocumentType = DocumentType.SalesInvoice,
                DocumentId = invoice.Id,
                DocumentNumber = invoice.InvoiceNumber,
                RevisionNumber = invoice.RevisionNumber,
                Action = ApprovalAction.Confirmed,
                PreviousStatus = InvoiceStatus.Draft.ToString(),
                NewStatus = InvoiceStatus.Issued.ToString(),
                SubmittedByUserId = userId,
                ActionByUserId = userId,
                SubmittedAt = now,
                ActionAt = now,
                Comments = $"Auto-issued from approved website booking.",
                FinancialTotal = invoice.GrandTotal,
                Currency = invoice.Currency,
                IsFinal = true,
                IpAddress = string.Empty,
                CreatedAt = now
            });

            _dbContext.DocumentActivities.Add(new DocumentActivity
            {
                DocumentType = DocumentType.SalesInvoice,
                DocumentId = invoice.Id,
                ActivityType = ApprovalAction.Confirmed,
                Description = $"Sales Invoice {invoice.InvoiceNumber} issued and paid.",
                PerformedByUserId = userId,
                PerformedAt = now
            });
        }

        private async Task RecordPaymentActivityAsync(SalesInvoice invoice, Payment payment, int userId, DateTime now)
        {
            var previousStatus = invoice.Status;
            invoice.Status = InvoiceStatus.Paid;

            _dbContext.DocumentApprovals.Add(new DocumentApproval
            {
                DocumentType = DocumentType.SalesInvoice,
                DocumentId = invoice.Id,
                DocumentNumber = invoice.InvoiceNumber,
                RevisionNumber = invoice.RevisionNumber,
                Action = ApprovalAction.PaymentRecorded,
                PreviousStatus = previousStatus.ToString(),
                NewStatus = InvoiceStatus.Paid.ToString(),
                SubmittedByUserId = userId,
                ActionByUserId = userId,
                ActionAt = now,
                Comments = $"Payment {payment.PaymentNumber} recorded.",
                FinancialTotal = payment.Amount,
                Currency = invoice.Currency,
                IsFinal = true,
                IpAddress = string.Empty,
                CreatedAt = now
            });

            _dbContext.DocumentActivities.Add(new DocumentActivity
            {
                DocumentType = DocumentType.SalesInvoice,
                DocumentId = invoice.Id,
                ActivityType = ApprovalAction.PaymentRecorded,
                Description = $"Payment {payment.PaymentNumber} recorded for {invoice.CurrencySymbol} {payment.Amount:N2}.",
                PerformedByUserId = userId,
                PerformedAt = now
            });
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
