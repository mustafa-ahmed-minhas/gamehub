using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using GameHub.Data;
using GameHub.Helpers;
using GameHub.Models.Checkout;
using GameHub.Models.Entities;
using GameHub.Models.Enums;
using GameHub.Models.ViewModels;
using GameHub.Services.Interfaces;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GameHub.Controllers
{
    [Route("Payment")]
    public class OnlinePaymentController : Controller
    {
        private const string CheckoutSessionKey = "GameHub.OnlineBooking.Checkout";
        private const string MembershipCheckoutSessionKey = "GameHub.Membership.Checkout";
        private readonly ApplicationDbContext _dbContext;
        private readonly IPaymentGateway _paymentGateway;
        private readonly IWebHostEnvironment _environment;
        private readonly IBookingValidationService _validationService;
        private readonly ICustomerMembershipService _membershipService;
        private readonly IInvoiceService _invoiceService;
        private readonly IMembershipNumberService _membershipNumberService;
        private readonly IMembershipOperationService _operationService;

        public OnlinePaymentController(
            ApplicationDbContext dbContext,
            IPaymentGateway paymentGateway,
            IWebHostEnvironment environment,
            IBookingValidationService validationService,
            ICustomerMembershipService membershipService,
            IInvoiceService invoiceService,
            IMembershipNumberService membershipNumberService,
            IMembershipOperationService operationService)
        {
            _dbContext = dbContext;
            _paymentGateway = paymentGateway;
            _environment = environment;
            _validationService = validationService;
            _membershipService = membershipService;
            _invoiceService = invoiceService;
            _membershipNumberService = membershipNumberService;
            _operationService = operationService;
        }

        [HttpGet("")]
        public async Task<IActionResult> Index()
        {
            var context = await LoadPaymentContextAsync();
            if (!context.Valid) return RedirectToAction("Checkout", "OnlineBooking");
            return View(ToPaymentViewModel(context.Checkout!));
        }

        [HttpPost("Process")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Process(OnlinePaymentViewModel model)
        {
            var context = await LoadPaymentContextAsync();
            if (!context.Valid || context.Customer == null || context.Checkout == null)
            {
                TempData.SetToast("error", "Payment Blocked", "Checkout session is invalid or expired.");
                return RedirectToAction("Checkout", "OnlineBooking");
            }

            ValidatePaymentForm(model);
            if (!ModelState.IsValid)
            {
                var vm = ToPaymentViewModel(context.Checkout);
                vm.PaymentMethod = model.PaymentMethod;
                return View("Index", vm);
            }

            var transaction = new PaymentTransaction
            {
                TransactionNumber = await GenerateTransactionNumberAsync(),
                CheckoutToken = context.Checkout.CheckoutToken,
                CustomerId = context.Customer.Id,
                Gateway = PaymentGatewayType.Demo,
                PaymentMethod = model.PaymentMethod,
                Status = PaymentTransactionStatus.Initiated,
                GatewayTransactionId = "INIT-" + Guid.NewGuid().ToString("N")[..12].ToUpperInvariant(),
                Amount = context.Checkout.TotalAmount,
                Currency = (await GetSettingsAsync()).Currency,
                CurrencySymbol = context.Checkout.CurrencySymbol,
                IdempotencyKey = Guid.NewGuid().ToString("N"),
                InitiatedAt = DateTime.UtcNow,
                CreatedAt = DateTime.UtcNow,
                ClientIpHash = Hash(Request.HttpContext.Connection.RemoteIpAddress?.ToString()),
                UserAgentSummary = Request.Headers.UserAgent.ToString().Length > 250 ? Request.Headers.UserAgent.ToString()[..250] : Request.Headers.UserAgent.ToString()
            };
            _dbContext.PaymentTransactions.Add(transaction);
            await _dbContext.SaveChangesAsync();

            transaction.Status = PaymentTransactionStatus.Processing;
            transaction.ProcessingAt = DateTime.UtcNow;
            var gateway = await _paymentGateway.ProcessAsync(model, context.Checkout.TotalAmount, transaction.Currency);
            transaction.GatewayTransactionId = gateway.GatewayTransactionId;
            transaction.GatewayReference = gateway.GatewayReference;
            transaction.CardBrand = gateway.CardBrand;
            transaction.CardLastFour = gateway.CardLastFour;
            transaction.CustomerSafeMessage = gateway.CustomerMessage;
            transaction.FailureReason = gateway.FailureReason;
            transaction.UpdatedAt = DateTime.UtcNow;

            if (!gateway.Succeeded)
            {
                transaction.Status = gateway.Cancelled ? PaymentTransactionStatus.Cancelled : PaymentTransactionStatus.Failed;
                transaction.CancelledAt = gateway.Cancelled ? DateTime.UtcNow : null;
                transaction.FailedAt = gateway.Cancelled ? null : DateTime.UtcNow;
                await _dbContext.SaveChangesAsync();
                await CreateNotificationAsync(context.Customer.Id, null, gateway.Cancelled ? CustomerNotificationType.PaymentFailed : CustomerNotificationType.PaymentFailed, gateway.Cancelled ? "Payment Cancelled" : "Payment Failed", gateway.CustomerMessage, "/Payment");
                TempData.SetToast(gateway.Cancelled ? "warning" : "error", gateway.Cancelled ? "Payment Cancelled" : "Payment Failed", gateway.CustomerMessage);
                return RedirectToAction(gateway.Cancelled ? nameof(Cancelled) : nameof(Failed), new { id = transaction.Id });
            }

            transaction.Status = PaymentTransactionStatus.Succeeded;
            transaction.CompletedAt = DateTime.UtcNow;
            await _dbContext.SaveChangesAsync();

            var finalize = await FinalizeBookingAsync(transaction.Id);
            if (!finalize.Success)
            {
                transaction.Status = PaymentTransactionStatus.Refunded;
                transaction.RefundedAt = DateTime.UtcNow;
                transaction.CustomerSafeMessage = finalize.Message;
                await _dbContext.SaveChangesAsync();
                TempData.SetToast("warning", "Slot Unavailable", finalize.Message);
                return RedirectToAction(nameof(Failed), new { id = transaction.Id });
            }

            TempData.SetToast("success", "Booking Confirmed", "Payment successful. Your court is booked.");
            return RedirectToAction(nameof(Success), new { id = transaction.Id });
        }

        [HttpGet("Result")]
        public async Task<IActionResult> Result(int id)
        {
            var customer = await GetCurrentCustomerAsync();
            if (customer == null) return RedirectToAction("Login", "Account", new { returnUrl = Url.Action(nameof(Result), new { id }) });
            var transaction = await _dbContext.PaymentTransactions.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id && x.CustomerId == customer.Id);
            if (transaction == null) return RedirectToAction("NotFound", "Error");
            return transaction.Status switch
            {
                PaymentTransactionStatus.Succeeded => RedirectToAction(nameof(Success), new { id }),
                PaymentTransactionStatus.Cancelled => RedirectToAction(nameof(Cancelled), new { id }),
                _ => RedirectToAction(nameof(Failed), new { id })
            };
        }

        [HttpGet("Success")]
        public async Task<IActionResult> Success(int id)
        {
            var transaction = await LoadCustomerTransactionDetailsAsync(id);
            if (transaction == null || transaction.BookingId == null) return RedirectToAction("NotFound", "Error");
            var receipt = await _dbContext.PaymentReceipts.AsNoTracking().FirstOrDefaultAsync(x => x.PaymentTransactionId == transaction.Id);
            ViewBag.ReceiptNumber = receipt?.ReceiptNumber;
            return View(transaction);
        }

        [HttpGet("Failed")]
        public async Task<IActionResult> Failed(int id)
        {
            var customer = await GetCurrentCustomerAsync();
            if (customer == null) return RedirectToAction("Login", "Account", new { returnUrl = Url.Action(nameof(Failed), new { id }) });
            var transaction = await _dbContext.PaymentTransactions.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id && x.CustomerId == customer.Id);
            if (transaction == null) return RedirectToAction("NotFound", "Error");
            return View(transaction);
        }

        [HttpGet("Cancelled")]
        public async Task<IActionResult> Cancelled(int id)
        {
            var customer = await GetCurrentCustomerAsync();
            if (customer == null) return RedirectToAction("Login", "Account", new { returnUrl = Url.Action(nameof(Cancelled), new { id }) });
            var transaction = await _dbContext.PaymentTransactions.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id && x.CustomerId == customer.Id);
            if (transaction == null) return RedirectToAction("NotFound", "Error");
            return View(transaction);
        }

        [HttpPost("Cancel")]
        [ValidateAntiForgeryToken]
        public IActionResult Cancel()
        {
            TempData.SetToast("warning", "Payment Cancelled", "No booking was created.");
            return RedirectToAction("Checkout", "OnlineBooking");
        }

        [HttpGet("Retry")]
        public IActionResult Retry()
        {
            return RedirectToAction(nameof(Index));
        }

        [HttpGet("MembershipCheckout")]
        public async Task<IActionResult> MembershipCheckout()
        {
            var context = await LoadMembershipPaymentContextAsync();
            if (!context.Valid)
            {
                TempData.SetToast("error", "Checkout Expired", "Your membership checkout session is invalid or expired. Please start again.");
                return RedirectToAction("Plans", "MyMemberships");
            }

            return View("MembershipCheckout", ToMembershipPaymentViewModel(context.Checkout!));
        }

        [HttpPost("MembershipProcess")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MembershipProcess(OnlinePaymentViewModel model)
        {
            var context = await LoadMembershipPaymentContextAsync();
            if (!context.Valid || context.Customer == null || context.Checkout == null)
            {
                TempData.SetToast("error", "Payment Blocked", "Membership checkout session is invalid or expired.");
                return RedirectToAction("Plans", "MyMemberships");
            }

            ValidatePaymentForm(model);
            if (!ModelState.IsValid)
            {
                var vm = ToMembershipPaymentViewModel(context.Checkout);
                vm.PaymentMethod = model.PaymentMethod;
                return View("MembershipCheckout", vm);
            }

            var settings = await GetSettingsAsync();

            // Idempotency: a membership checkout session represents a single payment attempt.
            // Reuse a deterministic key per session so a double-submit or browser retry can never
            // create a second transaction for the same checkout.
            var idempotencyKey = BuildMembershipIdempotencyKey(context.Checkout.CheckoutToken, context.Customer.Id);

            var existingTransaction = await _dbContext.PaymentTransactions
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.IdempotencyKey == idempotencyKey);

            if (existingTransaction != null)
            {
                if (existingTransaction.Status == PaymentTransactionStatus.Succeeded)
                {
                    var alreadyFinalized = await _dbContext.CustomerMemberships
                        .AsNoTracking()
                        .AnyAsync(x => x.PaymentTransactionId == existingTransaction.Id);

                    TempData.SetToast("info", "Already Processed", "This payment has already been processed.");
                    return RedirectToAction(alreadyFinalized ? "MembershipSuccess" : "MembershipFailed", new { id = existingTransaction.Id });
                }

                if (existingTransaction.Status is PaymentTransactionStatus.Initiated or PaymentTransactionStatus.Processing)
                {
                    TempData.SetToast("warning", "Payment In Progress", "A payment for this checkout is already being processed. Please wait a moment.");
                    return RedirectToAction("MembershipCheckout");
                }

                // A previously failed or cancelled attempt keeps its record. The customer must start a
                // new checkout session rather than retrying a settled transaction in place.
                TempData.SetToast("error", "Payment Already Attempted", "A payment attempt was already recorded for this checkout. Please start a new checkout.");
                ClearMembershipCheckoutSession();
                return RedirectToAction("Plans", "MyMemberships");
            }

            var transaction = new PaymentTransaction
            {
                TransactionNumber = await GenerateTransactionNumberAsync(),
                CheckoutToken = context.Checkout.CheckoutToken,
                CustomerId = context.Customer.Id,
                Gateway = PaymentGatewayType.Demo,
                PaymentMethod = model.PaymentMethod,
                Status = PaymentTransactionStatus.Initiated,
                GatewayTransactionId = "INIT-" + Guid.NewGuid().ToString("N")[..12].ToUpperInvariant(),
                Amount = context.Checkout.TotalAmount,
                Currency = settings.Currency,
                CurrencySymbol = context.Checkout.CurrencySymbol,
                IdempotencyKey = idempotencyKey,
                InitiatedAt = DateTime.UtcNow,
                CreatedAt = DateTime.UtcNow,
                ClientIpHash = Hash(Request.HttpContext.Connection.RemoteIpAddress?.ToString()),
                UserAgentSummary = Request.Headers.UserAgent.ToString().Length > 250 ? Request.Headers.UserAgent.ToString()[..250] : Request.Headers.UserAgent.ToString()
            };
            _dbContext.PaymentTransactions.Add(transaction);

            try
            {
                await _dbContext.SaveChangesAsync();
            }
            catch (DbUpdateException)
            {
                // Unique index on IdempotencyKey: a concurrent duplicate submission won the race.
                _dbContext.ChangeTracker.Clear();
                TempData.SetToast("warning", "Duplicate Submission", "This payment was already submitted. Please wait a moment and refresh.");
                return RedirectToAction("MembershipCheckout");
            }

            transaction.Status = PaymentTransactionStatus.Processing;
            transaction.ProcessingAt = DateTime.UtcNow;
            var gateway = await _paymentGateway.ProcessAsync(model, context.Checkout.TotalAmount, transaction.Currency);
            transaction.GatewayTransactionId = gateway.GatewayTransactionId;
            transaction.GatewayReference = gateway.GatewayReference;
            transaction.CardBrand = gateway.CardBrand;
            transaction.CardLastFour = gateway.CardLastFour;
            transaction.CustomerSafeMessage = gateway.CustomerMessage;
            transaction.FailureReason = gateway.FailureReason;
            transaction.UpdatedAt = DateTime.UtcNow;

            if (!gateway.Succeeded)
            {
                transaction.Status = gateway.Cancelled ? PaymentTransactionStatus.Cancelled : PaymentTransactionStatus.Failed;
                transaction.CancelledAt = gateway.Cancelled ? DateTime.UtcNow : null;
                transaction.FailedAt = gateway.Cancelled ? null : DateTime.UtcNow;
                await _dbContext.SaveChangesAsync();
                await CreateNotificationAsync(context.Customer.Id, null, CustomerNotificationType.PaymentFailed, gateway.Cancelled ? "Payment Cancelled" : "Payment Failed", gateway.CustomerMessage, "/MyMemberships");
                TempData.SetToast(gateway.Cancelled ? "warning" : "error", gateway.Cancelled ? "Payment Cancelled" : "Payment Failed", gateway.CustomerMessage);
                return RedirectToAction(gateway.Cancelled ? "MembershipCancelled" : "MembershipFailed", new { id = transaction.Id });
            }

            transaction.Status = PaymentTransactionStatus.Succeeded;
            transaction.CompletedAt = DateTime.UtcNow;
            await _dbContext.SaveChangesAsync();

            var finalize = await FinalizeMembershipAsync(transaction.Id);
            if (!finalize.Success)
            {
                // The payment genuinely succeeded, so the transaction stays Succeeded. No refund status
                // is invented here: any reversal must be performed against the real gateway and
                // recorded through the normal refund path.
                transaction.CustomerSafeMessage = finalize.Message;
                transaction.UpdatedAt = DateTime.UtcNow;
                await _dbContext.SaveChangesAsync();
                ClearMembershipCheckoutSession();
                TempData.SetToast("warning", "Membership Unavailable", finalize.Message);
                return RedirectToAction("MembershipFailed", new { id = transaction.Id });
            }

            TempData.SetToast(
                "success",
                context.Checkout.Operation switch
                {
                    MembershipOperation.Renew => "Membership Renewed",
                    MembershipOperation.Upgrade => "Membership Upgraded",
                    _ => "Membership Active"
                },
                "Payment successful. Your membership is now active.");
            return RedirectToAction("MembershipSuccess", new { id = transaction.Id });
        }

        [HttpGet("MembershipSuccess")]
        public async Task<IActionResult> MembershipSuccess(int id)
        {
            var transaction = await LoadCustomerTransactionDetailsAsync(id);
            if (transaction == null) return RedirectToAction("NotFound", "Error");

            var membership = await _dbContext.CustomerMemberships
                .AsNoTracking()
                .Include(x => x.MembershipPlan)
                .FirstOrDefaultAsync(x => x.PaymentTransactionId == transaction.Id);

            if (membership == null) return RedirectToAction("NotFound", "Error");

            var invoice = await _dbContext.MembershipInvoices
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.CustomerMembershipId == membership.Id);

            ViewBag.Membership = membership;
            ViewBag.Invoice = invoice;
            return View(transaction);
        }

        [HttpGet("MembershipFailed")]
        public async Task<IActionResult> MembershipFailed(int id)
        {
            var transaction = await LoadCustomerTransactionDetailsAsync(id);
            if (transaction == null) return RedirectToAction("NotFound", "Error");
            return View("MembershipFailed", transaction);
        }

        [HttpGet("MembershipCancelled")]
        public async Task<IActionResult> MembershipCancelled(int id)
        {
            var transaction = await LoadCustomerTransactionDetailsAsync(id);
            if (transaction == null) return RedirectToAction("NotFound", "Error");
            return View("MembershipCancelled", transaction);
        }

        [HttpPost("MembershipCancelCheckout")]
        [ValidateAntiForgeryToken]
        public IActionResult MembershipCancelCheckout()
        {
            ClearMembershipCheckoutSession();
            TempData.SetToast("info", "Checkout Cancelled", "No membership payment was taken.");
            return RedirectToAction("Plans", "MyMemberships");
        }

        private async Task<(bool Success, string Message)> FinalizeMembershipAsync(int transactionId)
        {
            await using var dbTransaction = await _dbContext.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
            try
            {
                var transaction = await _dbContext.PaymentTransactions.FirstOrDefaultAsync(x => x.Id == transactionId);
                if (transaction == null || transaction.Status != PaymentTransactionStatus.Succeeded) return (false, "Payment could not be verified.");
                if (await _dbContext.CustomerMemberships.AnyAsync(x => x.PaymentTransactionId == transaction.Id)) return (true, "Already processed.");

                var checkout = GetMembershipCheckoutSession();
                if (checkout == null || checkout.CustomerId != transaction.CustomerId || checkout.ExpiresAtUtc <= DateTime.UtcNow) return (false, "Checkout session expired before the membership was activated.");

                // Sessions written before operations existed only carry the renewal flag, so fall back
                // to it rather than pricing a renewal as a fresh join.
                var operation = checkout.Operation == MembershipOperation.Join && checkout.IsRenewal
                    ? MembershipOperation.Renew
                    : checkout.Operation;

                var customer = await _dbContext.Customers.FirstOrDefaultAsync(x => x.Id == checkout.CustomerId);
                if (customer == null || !customer.IsActive || customer.IsBlacklisted || !customer.EmailVerified) return (false, "Customer account could not be verified.");

                var plan = await _dbContext.MembershipPlans.FirstOrDefaultAsync(x => x.Id == checkout.MembershipPlanId);
                if (plan == null || !plan.IsActive) return (false, "This membership plan is no longer available.");

                if (Math.Round(transaction.Amount, 2) != Math.Round(checkout.TotalAmount, 2)) return (false, "Payment amount mismatch. Please contact support.");

                var today = DateOnly.FromDateTime(DateTime.UtcNow);

                // Re-price the operation from live data. The customer must never be able to change
                // the plan or the amount by editing a hidden field or replaying an old session.
                var quote = await _operationService.QuoteAsync(checkout.CustomerId, checkout.MembershipPlanId, operation);
                if (!quote.IsValid) return (false, quote.Message);
                if (Math.Round(quote.TotalAmount, 2) != Math.Round(checkout.TotalAmount, 2)) return (false, "The membership price changed. Please start a new checkout.");

                CustomerMembership? previous = null;
                if (quote.Operation != MembershipOperation.Join)
                {
                    previous = await _dbContext.CustomerMemberships
                        .Include(x => x.MembershipPlan)
                        .FirstOrDefaultAsync(x => x.Id == quote.Current!.Id && x.CustomerId == checkout.CustomerId);

                    if (previous == null) return (false, "The membership being changed could not be found.");
                }

                var membership = new CustomerMembership
                {
                    MembershipNumber = await _membershipNumberService.GenerateNextNumberAsync(),
                    CustomerId = checkout.CustomerId,
                    MembershipPlanId = plan.Id,
                    PaymentTransactionId = transaction.Id,
                    StartDate = quote.StartDate,
                    ExpiryDate = quote.ExpiryDate,
                    Status = MembershipStatus.Active,
                    JoiningFee = plan.JoiningFee,
                    RenewalFee = plan.RenewalFee,
                    DiscountPercentage = plan.DiscountPercentage,
                    IncludedBookingHours = quote.IncludedHoursAfterOperation,
                    UsedBookingHours = 0,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow
                };

                if (quote.Operation == MembershipOperation.Renew)
                {
                    // A renewal opens a new term rather than overwriting the old one, so every term
                    // keeps its own payment, invoice and record in the customer's history.
                    membership.Notes = $"Renewed from {previous!.MembershipPlan.Name} ({previous.MembershipNumber}) on {DateTime.UtcNow:MMM dd, yyyy}.";
                }
                else if (quote.Operation == MembershipOperation.Upgrade)
                {
                    membership.Notes = $"Upgraded from {previous!.MembershipPlan.Name} ({previous.MembershipNumber}) on {DateTime.UtcNow:MMM dd, yyyy}. "
                        + $"{Math.Max(0, previous.IncludedBookingHours - previous.UsedBookingHours)} unused hour(s) were carried over.";
                }

                _dbContext.CustomerMemberships.Add(membership);
                await _dbContext.SaveChangesAsync();

                if (previous != null)
                {
                    // The old term is never deleted, and it always points at the record that replaced it.
                    previous.SupersededByMembershipId = membership.Id;
                    previous.AutoRenew = false;
                    previous.UpdatedAt = DateTime.UtcNow;

                    // An upgrade replaces the plan across the same validity window, so the old term
                    // must always be closed. Leaving both active would make "which membership is
                    // current" ambiguous, since two rows would share the same expiry date.
                    //
                    // A renewal, however, only starts on the old expiry date. Closing a still-running
                    // term there would leave the customer with no membership until the new term begins,
                    // so an early renewal leaves the running term alone; it lapses on its own and is
                    // cleaned up by SyncExpiredMembershipsAsync.
                    var closePrevious = quote.Operation == MembershipOperation.Upgrade
                        || previous.ExpiryDate <= today;

                    if (closePrevious)
                    {
                        previous.Status = MembershipStatus.Expired;
                        previous.IsActive = false;
                    }

                    await _dbContext.SaveChangesAsync();
                }

                await _invoiceService.EnsureMembershipInvoiceAsync(
                    membership.Id,
                    operation,
                    quote.FeeAmount,
                    quote.PreviousPlanName,
                    quote.PreviousPlanCode);

                await _membershipService.SyncCustomerSummaryAsync(customer.Id);

                _dbContext.CustomerNotifications.Add(new CustomerNotification
                {
                    CustomerId = customer.Id,
                    Type = CustomerNotificationType.PaymentSuccessful,
                    Title = NotificationTitle(operation),
                    Message = quote.Operation == MembershipOperation.Upgrade
                        ? $"You upgraded to {plan.Name} for {quote.FeeAmount:0.##} {checkout.CurrencySymbol}. Your membership runs until {quote.ExpiryDate:MMM dd, yyyy}."
                        : quote.StartDate > today
                            // An early renewal is prepaid: the current term keeps running until then.
                            ? $"Your {plan.Name} membership is prepaid. It starts on {quote.StartDate:MMM dd, yyyy} and runs until {quote.ExpiryDate:MMM dd, yyyy}. Your current term stays active until then."
                            : $"Your {plan.Name} membership is active until {quote.ExpiryDate:MMM dd, yyyy}.",
                    ActionUrl = "/MyMemberships",
                    CreatedAt = DateTime.UtcNow
                });
                await _dbContext.SaveChangesAsync();

                await dbTransaction.CommitAsync();
                ClearMembershipCheckoutSession();
                return (true, "Membership activated.");
            }
            catch (DbUpdateException)
            {
                await dbTransaction.RollbackAsync();
                return (false, "A conflicting membership was detected. Please contact support to complete your membership.");
            }
            catch (Exception)
            {
                await dbTransaction.RollbackAsync();
                return (false, "The membership could not be completed. Please contact support with your transaction number.");
            }
        }

        private static string NotificationTitle(MembershipOperation operation) => operation switch
        {
            MembershipOperation.Renew => "Membership Renewed",
            MembershipOperation.Upgrade => "Membership Upgraded",
            _ => "Membership Activated"
        };

        private static string BuildMembershipIdempotencyKey(string checkoutToken, int customerId) =>
            "MEM:" + customerId + ":" + checkoutToken;

        private OnlinePaymentViewModel ToMembershipPaymentViewModel(MembershipCheckoutSession checkout) => new()
        {
            CheckoutToken = checkout.CheckoutToken,
            ArenaName = checkout.ArenaName,
            CustomerName = checkout.CustomerName,
            CurrencySymbol = checkout.CurrencySymbol,
            IsMembershipPayment = true,
            PlanName = checkout.PlanName,
            PlanCode = checkout.PlanCode,
            PlanBenefits = checkout.Benefits,
            DurationMonths = checkout.DurationMonths,
            IncludedBookingHours = checkout.IncludedBookingHours,
            MembershipStartDate = checkout.StartDate,
            MembershipExpiryDate = checkout.ExpiryDate,
            IsRenewal = checkout.IsRenewal,
            MembershipOperation = checkout.Operation.ToString(),
            PreviousPlanName = checkout.PreviousPlanName,
            BaseAmount = checkout.FeeAmount,
            MembershipDiscountAmount = 0,
            TaxAmount = checkout.TaxAmount,
            TotalAmount = checkout.TotalAmount,
            SecondsRemaining = Math.Max(0, (int)(checkout.ExpiresAtUtc - DateTime.UtcNow).TotalSeconds),
            IsDevelopmentDemo = _environment.IsDevelopment()
        };

        private async Task<(bool Valid, Customer? Customer, MembershipCheckoutSession? Checkout)> LoadMembershipPaymentContextAsync()
        {
            var customer = await GetCurrentCustomerAsync();
            var checkout = GetMembershipCheckoutSession();
            if (customer == null || checkout == null || checkout.CustomerId != customer.Id || checkout.ExpiresAtUtc <= DateTime.UtcNow || !customer.EmailVerified || !customer.IsActive || customer.IsBlacklisted)
            {
                return (false, customer, checkout);
            }
            return (true, customer, checkout);
        }

        private MembershipCheckoutSession? GetMembershipCheckoutSession()
        {
            var json = HttpContext.Session.GetString(MembershipCheckoutSessionKey);
            return string.IsNullOrWhiteSpace(json) ? null : JsonSerializer.Deserialize<MembershipCheckoutSession>(json);
        }

        private void ClearMembershipCheckoutSession() => HttpContext.Session.Remove(MembershipCheckoutSessionKey);

        private async Task<(bool Success, string Message)> FinalizeBookingAsync(int transactionId)
        {
            await using var dbTransaction = await _dbContext.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
            try
            {
                var transaction = await _dbContext.PaymentTransactions.FirstOrDefaultAsync(x => x.Id == transactionId);
                if (transaction == null || transaction.Status != PaymentTransactionStatus.Succeeded) return (false, "Payment could not be verified.");
                if (transaction.BookingId.HasValue || await _dbContext.Bookings.AnyAsync(x => x.PaymentTransactionId == transaction.Id)) return (true, "Already processed.");

                var checkout = GetCheckoutSession();
                if (checkout == null || checkout.CustomerId != transaction.CustomerId || checkout.ExpiresAtUtc <= DateTime.UtcNow) return (false, "Checkout session expired before booking confirmation.");

                var customer = await _dbContext.Customers.FirstOrDefaultAsync(x => x.Id == checkout.CustomerId);
                if (customer == null || !customer.IsActive || customer.IsBlacklisted || !customer.EmailVerified) return (false, "Customer account could not be verified.");

                var court = await _dbContext.Courts.Include(x => x.Sport).Include(x => x.Facility).FirstOrDefaultAsync(x => x.Id == checkout.CourtId);
                var settings = await GetSettingsAsync();
                if (court == null || !court.IsActive || !court.Sport.IsActive || !court.Facility.IsActive || court.Status is CourtStatus.Closed or CourtStatus.Maintenance) return (false, "Court is no longer available.");

                var available = await _validationService.CheckAvailabilityAsync(checkout.CourtId, checkout.BookingDate, checkout.StartTime, checkout.EndTime);
                if (!available.IsAvailable)
                {
                    return (false, "This time slot became unavailable before confirmation. Your test payment has been reversed. Please select another slot.");
                }

            if (Math.Round(transaction.Amount, 2) != Math.Round(checkout.TotalAmount, 2)) return (false, "Payment amount mismatch. Please contact support.");

            var booking = new Booking
            {
                BookingNumber = await GenerateBookingNumberAsync(settings),
                CustomerId = checkout.CustomerId,
                SportId = checkout.SportId,
                FacilityId = checkout.FacilityId,
                CourtId = checkout.CourtId,
                CustomerMembershipId = checkout.CustomerMembershipId,
                BookingDate = checkout.BookingDate,
                StartTime = checkout.StartTime,
                EndTime = checkout.EndTime,
                DurationMinutes = checkout.DurationMinutes,
                PlayerCount = checkout.PlayerCount,
                Source = BookingSource.Website,
                Status = BookingStatus.Confirmed,
                PaymentStatus = PaymentStatus.Paid,
                BaseAmount = checkout.BaseAmount,
                MembershipDiscountAmount = checkout.MembershipDiscountAmount,
                ManualDiscountAmount = 0,
                TaxAmount = checkout.TaxAmount,
                TotalAmount = checkout.TotalAmount,
                PaidAmount = checkout.TotalAmount,
                BalanceAmount = 0,
                CustomerNotes = checkout.CustomerNotes,
                SpecialRequest = checkout.SpecialRequest,
                IsPeakRate = checkout.IsPeakRate,
                IsMemberBooking = checkout.CustomerMembershipId.HasValue && checkout.MembershipDiscountAmount > 0,
                MembershipHoursApplied = false,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                ConfirmedAt = DateTime.UtcNow,
                PaidAt = DateTime.UtcNow,
                PaymentReference = transaction.GatewayReference,
                WebsiteCheckoutToken = checkout.CheckoutToken,
                PaymentTransactionId = transaction.Id,
                CustomerReminderEnabled = settings.EnableCustomerBookingReminders
            };
            _dbContext.Bookings.Add(booking);
            await _dbContext.SaveChangesAsync();

            transaction.BookingId = booking.Id;
            transaction.UpdatedAt = DateTime.UtcNow;

            if (booking.CustomerMembershipId.HasValue)
            {
                var membership = await _dbContext.CustomerMemberships.FirstOrDefaultAsync(x => x.Id == booking.CustomerMembershipId.Value);
                if (membership != null && !booking.MembershipHoursApplied)
                {
                    membership.UsedBookingHours += Math.Max(1, (int)Math.Ceiling(booking.DurationMinutes / 60m));
                    membership.UpdatedAt = DateTime.UtcNow;
                    booking.MembershipHoursApplied = true;
                }
            }

            var invoice = new BookingInvoice
            {
                InvoiceNumber = await GenerateInvoiceNumberAsync(settings),
                BookingId = booking.Id,
                CustomerId = customer.Id,
                InvoiceDate = DateTime.UtcNow,
                Subtotal = booking.BaseAmount,
                DiscountAmount = booking.MembershipDiscountAmount,
                TaxAmount = booking.TaxAmount,
                TotalAmount = booking.TotalAmount,
                PaidAmount = booking.PaidAmount,
                BalanceAmount = 0,
                Currency = settings.Currency,
                CurrencySymbol = settings.CurrencySymbol ?? "Rs",
                ArenaName = settings.ArenaName,
                ArenaPhone = settings.Phone,
                ArenaEmail = settings.Email,
                ArenaAddress = settings.Address,
                CustomerName = customer.FullName,
                CustomerEmail = customer.Email ?? "",
                CustomerPhone = customer.PrimaryPhone,
                BookingNumber = booking.BookingNumber,
                CourtName = court.Name,
                SportName = court.Sport.Name,
                BookingDate = booking.BookingDate,
                StartTime = booking.StartTime,
                EndTime = booking.EndTime,
                IsPaid = true,
                CreatedAt = DateTime.UtcNow
            };
            _dbContext.BookingInvoices.Add(invoice);

            var receipt = new PaymentReceipt
            {
                ReceiptNumber = await GenerateReceiptNumberAsync(settings),
                PaymentTransactionId = transaction.Id,
                BookingId = booking.Id,
                CustomerId = customer.Id,
                ReceiptDate = DateTime.UtcNow,
                AmountPaid = booking.TotalAmount,
                PaymentMethod = transaction.PaymentMethod,
                PaymentReference = transaction.GatewayReference ?? transaction.GatewayTransactionId,
                Currency = settings.Currency,
                CurrencySymbol = settings.CurrencySymbol ?? "Rs",
                CustomerName = customer.FullName,
                BookingNumber = booking.BookingNumber,
                ArenaName = settings.ArenaName,
                CreatedAt = DateTime.UtcNow
            };
            _dbContext.PaymentReceipts.Add(receipt);

            _dbContext.CustomerNotifications.AddRange(
                Notification(customer.Id, booking.Id, CustomerNotificationType.BookingConfirmed, "Booking Confirmed", $"Your booking {booking.BookingNumber} is confirmed.", $"/MyBookings/Details/{booking.Id}"),
                Notification(customer.Id, booking.Id, CustomerNotificationType.PaymentSuccessful, "Payment Successful", $"Payment of {booking.TotalAmount:N0} was received.", $"/MyBookings/Receipt/{booking.Id}"));

            if (settings.EnableCustomerBookingReminders)
            {
                AddReminder(booking, BookingReminderType.TwentyFourHoursBefore, settings.FirstReminderHoursBefore);
                AddReminder(booking, BookingReminderType.ThreeHoursBefore, settings.SecondReminderHoursBefore);
            }

            await _dbContext.SaveChangesAsync();
            await dbTransaction.CommitAsync();
            ClearCheckoutSession();
            return (true, "Booking confirmed.");
            }
            catch (Microsoft.EntityFrameworkCore.DbUpdateException)
            {
                await dbTransaction.RollbackAsync();
                return (false, "A conflicting booking was detected. Your test payment has been reversed. Please select another slot.");
            }
            catch (Exception)
            {
                await dbTransaction.RollbackAsync();
                return (false, "The booking could not be completed. Your test payment has been reversed. Please try again.");
            }
        }

        private OnlinePaymentViewModel ToPaymentViewModel(OnlineBookingCheckoutSession checkout) => new()
        {
            CheckoutToken = checkout.CheckoutToken,
            ArenaName = checkout.ArenaName,
            CustomerName = checkout.CustomerName,
            CourtName = checkout.CourtName,
            SportName = checkout.SportName,
            BookingDate = checkout.BookingDate,
            StartTime = checkout.StartTime,
            EndTime = checkout.EndTime,
            DurationMinutes = checkout.DurationMinutes,
            PlayerCount = checkout.PlayerCount,
            CurrencySymbol = checkout.CurrencySymbol,
            BaseAmount = checkout.BaseAmount,
            MembershipDiscountAmount = checkout.MembershipDiscountAmount,
            TaxAmount = checkout.TaxAmount,
            TotalAmount = checkout.TotalAmount,
            SecondsRemaining = Math.Max(0, (int)(checkout.ExpiresAtUtc - DateTime.UtcNow).TotalSeconds),
            IsDevelopmentDemo = _environment.IsDevelopment()
        };

        private async Task<(bool Valid, Customer? Customer, OnlineBookingCheckoutSession? Checkout)> LoadPaymentContextAsync()
        {
            var customer = await GetCurrentCustomerAsync();
            var checkout = GetCheckoutSession();
            if (customer == null || checkout == null || checkout.CustomerId != customer.Id || checkout.ExpiresAtUtc <= DateTime.UtcNow || !customer.EmailVerified || !customer.IsActive || customer.IsBlacklisted) return (false, customer, checkout);
            var settings = await GetSettingsAsync();
            if (!settings.AllowOnlineBooking) return (false, customer, checkout);
            return (true, customer, checkout);
        }

        private void ValidatePaymentForm(OnlinePaymentViewModel model)
        {
            if (!model.AcceptPaymentTerms) ModelState.AddModelError(nameof(model.AcceptPaymentTerms), "Please accept the payment terms.");
            if (model.PaymentMethod == OnlinePaymentMethod.Card)
            {
                var digits = OnlyDigits(model.CardNumber);
                if (string.IsNullOrWhiteSpace(model.CardholderName)) ModelState.AddModelError(nameof(model.CardholderName), "Cardholder name is required.");
                if (digits.Length != 16) ModelState.AddModelError(nameof(model.CardNumber), "Use a valid 16-digit demo card.");
                if (!int.TryParse(model.ExpiryMonth, out var month) || month is < 1 or > 12) ModelState.AddModelError(nameof(model.ExpiryMonth), "Valid expiry month is required.");
                if (!int.TryParse(model.ExpiryYear, out var year) || year < DateTime.UtcNow.Year) ModelState.AddModelError(nameof(model.ExpiryYear), "Use a future expiry year.");
                if (string.IsNullOrWhiteSpace(model.Cvv) || OnlyDigits(model.Cvv).Length != 3) ModelState.AddModelError(nameof(model.Cvv), "Use any 3-digit demo CVV.");
            }
            if (model.PaymentMethod == OnlinePaymentMethod.MobileWallet && (string.IsNullOrWhiteSpace(model.WalletProvider) || OnlyDigits(model.WalletNumber).Length < 10))
            {
                ModelState.AddModelError(nameof(model.WalletNumber), "Valid demo wallet details are required.");
            }
        }

        private async Task<Customer?> GetCurrentCustomerAsync()
        {
            var auth = await HttpContext.AuthenticateAsync(AuthConstants.CustomerScheme);
            if (!auth.Succeeded || auth.Principal?.Identity?.IsAuthenticated != true) return null;
            if (!int.TryParse(auth.Principal.FindFirstValue(ClaimTypes.NameIdentifier), out var id)) return null;
            return await _dbContext.Customers.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id && x.HasOnlineAccount);
        }

        private async Task<PaymentTransaction?> LoadCustomerTransactionDetailsAsync(int id)
        {
            var customer = await GetCurrentCustomerAsync();
            if (customer == null) return null;
            return await _dbContext.PaymentTransactions.AsNoTracking().Include(x => x.Booking).FirstOrDefaultAsync(x => x.Id == id && x.CustomerId == customer.Id);
        }
        private OnlineBookingCheckoutSession? GetCheckoutSession() => string.IsNullOrWhiteSpace(HttpContext.Session.GetString(CheckoutSessionKey)) ? null : JsonSerializer.Deserialize<OnlineBookingCheckoutSession>(HttpContext.Session.GetString(CheckoutSessionKey)!);
        private void ClearCheckoutSession() => HttpContext.Session.Remove(CheckoutSessionKey);
        private async Task<SystemSettings> GetSettingsAsync() => await _dbContext.SystemSettings.AsNoTracking().FirstOrDefaultAsync() ?? new SystemSettings { ArenaName = "GameHub Arena", Currency = "PKR", CurrencySymbol = "Rs", TaxPercentage = 0, BookingPrefix = "BKG", InvoicePrefix = "INV", ReceiptPrefix = "RCP", Phone = "", Email = "" };
        private async Task<string> GenerateTransactionNumberAsync() => await GenerateNumberAsync("PAY", _dbContext.PaymentTransactions.Select(x => x.TransactionNumber));
        private async Task<string> GenerateBookingNumberAsync(SystemSettings s) => await GenerateNumberAsync(string.IsNullOrWhiteSpace(s.BookingPrefix) ? "BKG" : s.BookingPrefix, _dbContext.Bookings.Select(x => x.BookingNumber));
        private async Task<string> GenerateInvoiceNumberAsync(SystemSettings s) => await GenerateNumberAsync(string.IsNullOrWhiteSpace(s.InvoicePrefix) ? "INV" : s.InvoicePrefix, _dbContext.BookingInvoices.Select(x => x.InvoiceNumber));
        private async Task<string> GenerateReceiptNumberAsync(SystemSettings s) => await GenerateNumberAsync(string.IsNullOrWhiteSpace(s.ReceiptPrefix) ? "RCP" : s.ReceiptPrefix, _dbContext.PaymentReceipts.Select(x => x.ReceiptNumber));
        private async Task<string> GenerateNumberAsync(string prefix, IQueryable<string> source)
        {
            var latest = await source.Where(x => x.StartsWith(prefix + "-")).OrderByDescending(x => x).FirstOrDefaultAsync();
            var next = int.TryParse(latest?.Split('-').LastOrDefault(), out var n) ? n + 1 : 1;
            return $"{prefix}-{next:000000}";
        }
        private void AddReminder(Booking booking, BookingReminderType type, int hoursBefore)
        {
            var scheduled = booking.BookingDate.ToDateTime(booking.StartTime).AddHours(-Math.Max(1, hoursBefore));
            if (scheduled > DateTime.UtcNow) _dbContext.BookingReminders.Add(new BookingReminder { BookingId = booking.Id, CustomerId = booking.CustomerId, ReminderType = type, ScheduledFor = scheduled, IsActive = true, CreatedAt = DateTime.UtcNow });
        }
        private static CustomerNotification Notification(int customerId, int? bookingId, CustomerNotificationType type, string title, string message, string? actionUrl) => new() { CustomerId = customerId, BookingId = bookingId, Type = type, Title = title, Message = message, ActionUrl = actionUrl, CreatedAt = DateTime.UtcNow };
        private async Task CreateNotificationAsync(int customerId, int? bookingId, CustomerNotificationType type, string title, string message, string? actionUrl)
        {
            _dbContext.CustomerNotifications.Add(Notification(customerId, bookingId, type, title, message, actionUrl));
            await _dbContext.SaveChangesAsync();
        }
        private static string Hash(string? value) => string.IsNullOrWhiteSpace(value) ? string.Empty : Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
        private static string OnlyDigits(string? value) => string.IsNullOrWhiteSpace(value) ? string.Empty : new string(value.Where(char.IsDigit).ToArray());
    }
}
