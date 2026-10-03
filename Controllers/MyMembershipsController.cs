using System.Security.Claims;
using System.Text.Json;
using GameHub.Data;
using GameHub.Filters;
using GameHub.Helpers;
using GameHub.Models.Checkout;
using GameHub.Models.Entities;
using GameHub.Models.Enums;
using GameHub.Models.ViewModels;
using GameHub.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GameHub.Controllers
{
    [CustomerAuthorize]
    public class MyMembershipsController : Controller
    {
        public const string MembershipCheckoutSessionKey = "GameHub.Membership.Checkout";

        private readonly ApplicationDbContext _dbContext;
        private readonly ICustomerMembershipService _membershipService;
        private readonly IMembershipOperationService _operationService;

        public MyMembershipsController(
            ApplicationDbContext dbContext,
            ICustomerMembershipService membershipService,
            IMembershipOperationService operationService)
        {
            _dbContext = dbContext;
            _membershipService = membershipService;
            _operationService = operationService;
        }

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var customerId = CurrentCustomerId();
            var settings = await GetSettingsAsync();
            var today = DateOnly.FromDateTime(DateTime.UtcNow);

            var memberships = await LoadMembershipsAsync(customerId);

            var current = memberships
                .Where(x => x.IsActive && x.Status == MembershipStatus.Active && x.ExpiryDate >= today)
                .OrderByDescending(x => x.ExpiryDate)
                .FirstOrDefault();

            var model = new MyMembershipViewModel
            {
                ArenaName = settings.ArenaName,
                CurrencySymbol = settings.CurrencySymbol ?? "Rs",
                HasMembership = current != null,
                // With a membership in hand the dashboard compares tiers, so cards are priced as
                // upgrade quotes rather than as fresh joins.
                Plans = await BuildPlanCardsAsync(customerId, current, current != null ? MembershipOperation.Upgrade : MembershipOperation.Join)
            };

            if (current != null)
            {
                var plan = current.MembershipPlan;
                model.MembershipId = current.Id;
                model.MembershipNumber = current.MembershipNumber;
                model.PlanName = plan.Name;
                model.PlanCode = plan.Code;
                model.PlanDescription = plan.Description;
                model.Benefits = plan.Benefits;
                model.Status = current.Status.ToString();
                model.StartDate = current.StartDate;
                model.ExpiryDate = current.ExpiryDate;
                model.RemainingDays = Math.Max(0, current.ExpiryDate.DayNumber - today.DayNumber);
                model.DiscountPercentage = current.DiscountPercentage;
                model.IncludedBookingHours = current.IncludedBookingHours;
                model.UsedBookingHours = current.UsedBookingHours;
                model.RemainingHours = Math.Max(0, current.IncludedBookingHours - current.UsedBookingHours);
                model.NextFee = current.RenewalFee;
                model.NextExpiryDate = current.ExpiryDate.AddMonths(plan.DurationMonths);
                // Renewal is available for any existing membership, matching the rule enforced at
                // checkout: a renewal extends the current expiry date and never overlaps.
                model.CanRenew = true;
                model.CanUpgrade = (await _operationService.GetUpgradePlansAsync(customerId)).Count > 0;
            }

            var cancellationBlockReason = await _operationService.GetCancellationBlockReasonAsync(customerId);
            model.CanCancel = cancellationBlockReason == null;
            model.CancellationBlockReason = cancellationBlockReason;

            model.UpcomingBookingCount = await _dbContext.Bookings.CountAsync(x =>
                x.CustomerId == customerId
                && x.IsActive
                && x.BookingDate >= today
                && x.Status == BookingStatus.Confirmed);

            model.TotalPaid = await TotalPaidAsync(customerId);
            model.History = await BuildHistoryAsync(customerId, model.CurrencySymbol);
            return View(model);
        }

        [HttpGet]
        public async Task<IActionResult> History()
        {
            var customerId = CurrentCustomerId();
            var settings = await GetSettingsAsync();
            return View(await BuildHistoryAsync(customerId, settings.CurrencySymbol ?? "Rs"));
        }

        [HttpGet]
        public IActionResult Join(int planId)
        {
            TempData.SetToast("info", "Choose a Plan", "Select a membership plan to continue.");
            return RedirectToAction(nameof(Plans), new { planId });
        }

        [HttpGet]
        [AllowAnonymous]
        public async Task<IActionResult> Plans(int? planId)
        {
            var customerId = CurrentCustomerId();
            var settings = await GetSettingsAsync();
            var current = customerId == 0 ? null : await LoadCurrentAsync(customerId);

            var model = await BuildCheckoutModelAsync(customerId, MembershipOperation.Join, planId, current, settings);
            model.IsSignedIn = customerId != 0;
            model.RequiresEmailVerification = customerId != 0
                && !await _dbContext.Customers.AsNoTracking().AnyAsync(x => x.Id == customerId && x.EmailVerified);
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Join(MembershipJoinViewModel model)
        {
            var customerId = CurrentCustomerId();
            var settings = await GetSettingsAsync();
            var current = await LoadCurrentAsync(customerId);

            var quote = await _operationService.QuoteAsync(customerId, model.MembershipPlanId, MembershipOperation.Join);
            if (!quote.IsValid)
            {
                ModelState.AddModelError(string.Empty, quote.Message);
                return View(nameof(Plans), await BuildCheckoutModelAsync(customerId, MembershipOperation.Join, model.MembershipPlanId, current, settings));
            }

            if (!model.AcceptMembershipTerms)
            {
                ModelState.AddModelError(nameof(model.AcceptMembershipTerms), "Please accept the membership terms.");
                return View(nameof(Plans), await BuildCheckoutModelAsync(customerId, MembershipOperation.Join, model.MembershipPlanId, current, settings));
            }

            StoreSession(await CreateSessionAsync(customerId, quote, settings));
            TempData.SetToast("info", "Checkout Ready", "Review the fee and complete the demo payment.");
            return RedirectToAction("MembershipCheckout", "OnlinePayment");
        }

        [HttpGet]
        public async Task<IActionResult> Renew()
        {
            var customerId = CurrentCustomerId();
            var settings = await GetSettingsAsync();
            var renewable = await LoadRenewableAsync(customerId);

            if (renewable == null)
            {
                TempData.SetToast("warning", "No Membership", "You do not have a membership to renew.");
                return RedirectToAction(nameof(Plans));
            }

            return View(nameof(Plans), await BuildCheckoutModelAsync(customerId, MembershipOperation.Renew, renewable.MembershipPlanId, renewable, settings));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Renew(MembershipJoinViewModel model)
        {
            var customerId = CurrentCustomerId();
            var settings = await GetSettingsAsync();
            var renewable = await LoadRenewableAsync(customerId);

            if (renewable == null)
            {
                TempData.SetToast("warning", "No Membership", "You do not have a membership to renew.");
                return RedirectToAction(nameof(Plans));
            }

            var quote = await _operationService.QuoteAsync(customerId, model.MembershipPlanId, MembershipOperation.Renew);
            if (!quote.IsValid)
            {
                ModelState.AddModelError(string.Empty, quote.Message);
                return View(nameof(Plans), await BuildCheckoutModelAsync(customerId, MembershipOperation.Renew, model.MembershipPlanId, renewable, settings));
            }

            if (!model.AcceptMembershipTerms)
            {
                ModelState.AddModelError(nameof(model.AcceptMembershipTerms), "Please accept the membership terms.");
                return View(nameof(Plans), await BuildCheckoutModelAsync(customerId, MembershipOperation.Renew, model.MembershipPlanId, renewable, settings));
            }

            StoreSession(await CreateSessionAsync(customerId, quote, settings));
            TempData.SetToast("info", "Checkout Ready", "Review the renewal fee and complete the demo payment.");
            return RedirectToAction("MembershipCheckout", "OnlinePayment");
        }

        [HttpGet]
        public async Task<IActionResult> Upgrade(int? planId = null)
        {
            var customerId = CurrentCustomerId();
            var settings = await GetSettingsAsync();
            var current = await LoadCurrentAsync(customerId);

            if (current == null)
            {
                TempData.SetToast("warning", "No Membership", "You do not have an active membership to upgrade.");
                return RedirectToAction(nameof(Plans));
            }

            var model = await BuildCheckoutModelAsync(customerId, MembershipOperation.Upgrade, planId, current, settings);
            if (model.PlanName.Length == 0)
            {
                TempData.SetToast("info", "No Upgrade Available", "There is no higher membership plan available right now.");
                return RedirectToAction(nameof(Index));
            }

            return View("Upgrade", model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Upgrade(MembershipJoinViewModel model)
        {
            var customerId = CurrentCustomerId();
            var settings = await GetSettingsAsync();
            var current = await LoadCurrentAsync(customerId);

            if (current == null)
            {
                TempData.SetToast("warning", "No Membership", "You do not have an active membership to upgrade.");
                return RedirectToAction(nameof(Plans));
            }

            var quote = await _operationService.QuoteAsync(customerId, model.MembershipPlanId, MembershipOperation.Upgrade);
            if (!quote.IsValid)
            {
                ModelState.AddModelError(string.Empty, quote.Message);
                return View("Upgrade", await BuildCheckoutModelAsync(customerId, MembershipOperation.Upgrade, model.MembershipPlanId, current, settings));
            }

            if (!model.AcceptMembershipTerms)
            {
                ModelState.AddModelError(nameof(model.AcceptMembershipTerms), "Please accept the membership terms.");
                return View("Upgrade", await BuildCheckoutModelAsync(customerId, MembershipOperation.Upgrade, model.MembershipPlanId, current, settings));
            }

            StoreSession(await CreateSessionAsync(customerId, quote, settings));
            TempData.SetToast("info", "Upgrade Checkout Ready", "Review the price difference and complete the demo payment.");
            return RedirectToAction("MembershipCheckout", "OnlinePayment");
        }

        [HttpGet]
        public async Task<IActionResult> Cancel()
        {
            var customerId = CurrentCustomerId();
            var settings = await GetSettingsAsync();
            var model = await BuildCancelModelAsync(customerId, settings);
            if (model == null)
            {
                TempData.SetToast("warning", "No Membership", "You do not have an active membership to cancel.");
                return RedirectToAction(nameof(Index));
            }

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Cancel(MembershipCancelViewModel model)
        {
            var customerId = CurrentCustomerId();
            var settings = await GetSettingsAsync();

            var blockReason = await _operationService.GetCancellationBlockReasonAsync(customerId);
            if (blockReason != null)
            {
                TempData.SetToast("warning", "Cancellation Not Available", blockReason);
                return RedirectToAction(nameof(Index));
            }

            // Ownership is verified from the database, never from the posted id.
            var membership = await _dbContext.CustomerMemberships
                .Include(x => x.MembershipPlan)
                .FirstOrDefaultAsync(x => x.Id == model.MembershipId && x.CustomerId == customerId);

            if (membership == null || !membership.IsActive || membership.Status != MembershipStatus.Active)
            {
                TempData.SetToast("warning", "No Membership", "You do not have an active membership to cancel.");
                return RedirectToAction(nameof(Index));
            }

            if (!model.ConfirmCancellation)
            {
                ModelState.AddModelError(nameof(model.ConfirmCancellation), "Please confirm the cancellation to continue.");
                return View("Cancel", await BuildCancelViewModelPreservingReasonAsync(customerId, settings, model));
            }

            var reason = model.Reason?.Trim();
            if (string.IsNullOrWhiteSpace(reason) || reason.Length < 5)
            {
                ModelState.AddModelError(nameof(model.Reason), "Please give a short reason so our team can help (at least 5 characters).");
                return View("Cancel", await BuildCancelViewModelPreservingReasonAsync(customerId, settings, model));
            }

            // Cancellation closes the membership but never deletes it: the record, its payments and
            // its invoices all stay in the customer's history. No refund is issued here, because a
            // refund must be handled by staff against the real gateway.
            membership.Status = MembershipStatus.Cancelled;
            membership.IsActive = false;
            membership.AutoRenew = false;
            membership.CancelledAt = todayDate();
            membership.CancellationReason = reason;
            membership.UpdatedAt = DateTime.UtcNow;
            await _dbContext.SaveChangesAsync();

            await _membershipService.SyncCustomerSummaryAsync(customerId);

            _dbContext.CustomerNotifications.Add(new CustomerNotification
            {
                CustomerId = customerId,
                Type = CustomerNotificationType.General,
                Title = "Membership Cancelled",
                Message = $"Your {membership.MembershipPlan.Name} membership has been cancelled. Your booking history and invoices are unchanged. Contact the arena if you need a refund.",
                ActionUrl = "/MyMemberships",
                CreatedAt = DateTime.UtcNow
            });
            await _dbContext.SaveChangesAsync();

            TempData.SetToast("success", "Membership Cancelled", "Your membership is cancelled. Your payments and invoices are still available in your history.");
            return RedirectToAction(nameof(Index));
        }

        private async Task<MembershipJoinViewModel> BuildCheckoutModelAsync(
            int customerId,
            MembershipOperation operation,
            int? planId,
            CustomerMembership? current,
            SystemSettings settings)
        {
            var plans = await LoadPlansAsync();
            var currency = settings.CurrencySymbol ?? "Rs";

            if (operation == MembershipOperation.Upgrade)
            {
                var eligible = await _operationService.GetUpgradePlansAsync(customerId);
                var candidates = eligible.Count > 0 ? eligible : new List<MembershipPlan>();
                var selected = candidates.FirstOrDefault(x => x.Id == planId) ?? candidates.FirstOrDefault();

                if (selected == null)
                {
                    return new MembershipJoinViewModel
                    {
                        ArenaName = settings.ArenaName,
                        CurrencySymbol = currency,
                        IsSignedIn = true,
                        IsUpgrade = true,
                        ExistingMembershipId = current?.Id,
                        CurrentMembershipNumber = current?.MembershipNumber,
                        Plans = new List<MembershipPlanCardViewModel>()
                    };
                }

                var upgradeQuote = await _operationService.QuoteAsync(customerId, selected.Id, MembershipOperation.Upgrade);
                var upgradeModel = new MembershipJoinViewModel
                {
                    ArenaName = settings.ArenaName,
                    CurrencySymbol = currency,
                    MembershipPlanId = selected.Id,
                    PlanName = selected.Name,
                    PlanCode = selected.Code,
                    PlanDescription = selected.Description,
                    Benefits = selected.Benefits,
                    DurationMonths = selected.DurationMonths,
                    IncludedBookingHours = upgradeQuote.IncludedHoursAfterOperation,
                    DiscountPercentage = selected.DiscountPercentage,
                    IsUpgrade = true,
                    ExistingMembershipId = current?.Id,
                    CurrentMembershipNumber = current?.MembershipNumber,
                    StartDate = upgradeQuote.StartDate,
                    ExpiryDate = upgradeQuote.ExpiryDate,
                    FeeAmount = upgradeQuote.FeeAmount,
                    TaxAmount = upgradeQuote.TaxAmount,
                    TotalAmount = upgradeQuote.TotalAmount,
                    PreviousPlanName = upgradeQuote.PreviousPlanName,
                    PreviousPlanCode = upgradeQuote.PreviousPlanCode,
                    PreviousPlanFee = upgradeQuote.PreviousPlanFee,
                    PreviousPlanIncludedHours = current?.IncludedBookingHours ?? 0,
                    RemainingHours = upgradeQuote.RemainingHours,
                    RemainingDays = Math.Max(0, (current?.ExpiryDate.DayNumber ?? 0) - todayDate().DayNumber),
                    IsSignedIn = true,
                    RequiresEmailVerification = !await _dbContext.Customers
                        .AsNoTracking()
                        .AnyAsync(x => x.Id == customerId && x.EmailVerified),
                    Plans = candidates.Select(p => MapUpgradePlan(p, current!, upgradeQuote)).ToList()
                };
                return upgradeModel;
            }

            var selectedPlan = plans.FirstOrDefault(x => x.Id == planId)
                ?? (current != null ? plans.FirstOrDefault(x => x.Id == current.MembershipPlanId) : null)
                ?? plans.FirstOrDefault();

            var quote = customerId == 0
                ? null
                : await _operationService.QuoteAsync(customerId, selectedPlan?.Id ?? 0, operation);

            // QuoteAsync reports ineligibility by returning a default-shaped quote (IsValid = false)
            // rather than null, so its dates are 0001-01-01 and its amounts are 0. Projecting that
            // straight into the view model would show a "Jan 01, 0001" validity window and a zero
            // total. Drop it so the summary falls back to the plan's real fee and duration.
            if (quote is { IsValid: false })
            {
                quote = null;
            }

            // A signed-out visitor has no quote, so the summary still needs the same fee and tax rule
            // the service would use, otherwise the plans page would show a total of zero.
            var fee = quote?.FeeAmount
                ?? (operation == MembershipOperation.Renew ? selectedPlan?.RenewalFee ?? 0 : selectedPlan?.JoiningFee ?? 0);
            var tax = quote?.TaxAmount ?? Math.Round(Math.Max(0, fee) * settings.TaxPercentage / 100, 2);

            return new MembershipJoinViewModel
            {
                ArenaName = settings.ArenaName,
                CurrencySymbol = currency,
                MembershipPlanId = selectedPlan?.Id ?? 0,
                PlanName = selectedPlan?.Name ?? string.Empty,
                PlanCode = selectedPlan?.Code ?? string.Empty,
                PlanDescription = selectedPlan?.Description,
                Benefits = selectedPlan?.Benefits,
                DurationMonths = selectedPlan?.DurationMonths ?? 0,
                IncludedBookingHours = quote?.IncludedHoursAfterOperation ?? selectedPlan?.IncludedBookingHours ?? 0,
                DiscountPercentage = selectedPlan?.DiscountPercentage ?? 0,
                IsRenewal = operation == MembershipOperation.Renew,
                ExistingMembershipId = current?.Id,
                CurrentMembershipNumber = current?.MembershipNumber,
                StartDate = quote?.StartDate ?? todayDate(),
                ExpiryDate = quote?.ExpiryDate ?? todayDate().AddMonths(selectedPlan?.DurationMonths ?? 1),
                FeeAmount = fee,
                TaxAmount = tax,
                TotalAmount = Math.Max(0, fee) + tax,
                PreviousPlanIncludedHours = current?.IncludedBookingHours ?? 0,
                RemainingHours = quote?.RemainingHours ?? 0,
                RemainingDays = Math.Max(0, (current?.ExpiryDate.DayNumber ?? 0) - todayDate().DayNumber),
                Plans = plans.Select(p => MapPlan(p, current?.MembershipPlanId, operation, quote)).ToList()
            };
        }

        private async Task<MembershipCancelViewModel?> BuildCancelModelAsync(int customerId, SystemSettings settings)
        {
            var current = await LoadCurrentAsync(customerId);
            if (current == null) return null;

            var today = todayDate();
            var currency = settings.CurrencySymbol ?? "Rs";
            var blockReason = await _operationService.GetCancellationBlockReasonAsync(customerId);

            return new MembershipCancelViewModel
            {
                MembershipId = current.Id,
                MembershipNumber = current.MembershipNumber,
                PlanName = current.MembershipPlan.Name,
                CurrencySymbol = currency,
                ExpiryDate = current.ExpiryDate,
                RemainingDays = Math.Max(0, current.ExpiryDate.DayNumber - today.DayNumber),
                IncludedHours = current.IncludedBookingHours,
                UsedHours = current.UsedBookingHours,
                RemainingHours = Math.Max(0, current.IncludedBookingHours - current.UsedBookingHours),
                TotalPaid = await TotalPaidAsync(customerId),
                AllowCustomerCancellation = blockReason == null,
                BlockReason = blockReason,
                PolicySummary = BuildCancellationPolicySummary(current, today)
            };
        }

        /// <summary>
        /// Rebuilds the cancel screen after a validation failure, keeping what the customer typed so
        /// they do not have to retype their reason.
        /// </summary>
        private async Task<MembershipCancelViewModel> BuildCancelViewModelPreservingReasonAsync(
            int customerId,
            SystemSettings settings,
            MembershipCancelViewModel posted)
        {
            var rebuilt = await BuildCancelModelAsync(customerId, settings);
            if (rebuilt == null) return posted;

            rebuilt.Reason = posted.Reason;
            rebuilt.ConfirmCancellation = posted.ConfirmCancellation;
            return rebuilt;
        }

        /// <summary>
        /// Describes only what the existing system rules actually do, so nothing is promised that the
        /// arena does not deliver.
        /// </summary>
        private static string BuildCancellationPolicySummary(CustomerMembership membership, DateOnly today)
        {
            var remainingDays = Math.Max(0, membership.ExpiryDate.DayNumber - today.DayNumber);
            return $"Cancelling ends your membership immediately. You have {remainingDays} day{(remainingDays == 1 ? "" : "s")} left and "
                + $"{Math.Max(0, membership.IncludedBookingHours - membership.UsedBookingHours)} unused booking hour(s). "
                + "Cancelling does not refund any payment automatically and does not affect court bookings you have already made. "
                + "If you paid and need a refund, please contact the arena with your transaction number.";
        }

        /// <summary>
        /// Membership money the customer has actually paid: settled transactions that are not tied to
        /// a court booking. Used for the dashboard and the cancellation disclosure.
        /// </summary>
        private async Task<decimal> TotalPaidAsync(int customerId)
        {
            return await _dbContext.PaymentTransactions
                .AsNoTracking()
                .Where(x => x.CustomerId == customerId
                    && x.BookingId == null
                    && x.Status == PaymentTransactionStatus.Succeeded)
                .SumAsync(x => (decimal?)x.Amount) ?? 0m;
        }

        private async Task<List<MembershipPlanCardViewModel>> BuildPlanCardsAsync(
            int customerId,
            CustomerMembership? current,
            MembershipOperation operation)
        {
            var plans = await LoadPlansAsync();
            var result = new List<MembershipPlanCardViewModel>();

            foreach (var plan in plans)
            {
                var card = MapPlan(plan, current?.MembershipPlanId, operation, null);
                if (customerId != 0 && operation == MembershipOperation.Upgrade && current != null)
                {
                    var quote = await _operationService.QuoteAsync(customerId, plan.Id, MembershipOperation.Upgrade);
                    card.IsUpgrade = quote.IsValid;
                    card.PayableAmount = quote.IsValid ? quote.TotalAmount : plan.JoiningFee;
                    card.CurrentPlanFee = current.JoiningFee;
                    card.IncludedBookingHours = quote.IsValid ? quote.IncludedHoursAfterOperation : plan.IncludedBookingHours;
                }
                result.Add(card);
            }

            return result;
        }

        private async Task<List<MembershipHistoryItemViewModel>> BuildHistoryAsync(int customerId, string currencySymbol)
        {
            var memberships = await LoadMembershipsAsync(customerId);
            if (memberships.Count == 0) return new List<MembershipHistoryItemViewModel>();

            var ids = memberships.Select(x => x.Id).ToList();

            var invoices = await _dbContext.MembershipInvoices
                .AsNoTracking()
                .Where(x => ids.Contains(x.CustomerMembershipId))
                .ToListAsync();

            var payments = await _dbContext.PaymentTransactions
                .AsNoTracking()
                .Where(x => x.CustomerId == customerId
                    && x.BookingId == null
                    && x.Status == PaymentTransactionStatus.Succeeded)
                .ToListAsync();

            var invoicesByMembership = invoices
                .GroupBy(x => x.CustomerMembershipId)
                .ToDictionary(g => g.Key, g => g.OrderByDescending(x => x.Id).First());

            var paymentsById = payments.ToDictionary(x => x.Id);

            return memberships
                .Select(membership =>
                {
                    var item = MapHistory(membership, currencySymbol);
                    if (invoicesByMembership.TryGetValue(membership.Id, out var invoice))
                    {
                        item.InvoiceId = invoice.Id;
                        item.UpgradedFromPlan = invoice.PreviousPlanName;
                        item.TransactionNumber = invoice.Operation ?? null;
                        if (invoice.PaymentTransactionId.HasValue
                            && paymentsById.TryGetValue(invoice.PaymentTransactionId.Value, out var payment))
                        {
                            item.TransactionNumber = payment.TransactionNumber;
                            item.PaidAmount = payment.Amount;
                            item.PaymentDate = payment.CompletedAt?.ToString("MMM dd, yyyy");
                        }
                    }
                    return item;
                })
                .ToList();
        }

        private Task<List<CustomerMembership>> LoadMembershipsAsync(int customerId) =>
            _dbContext.CustomerMemberships
                .AsNoTracking()
                .Include(x => x.MembershipPlan)
                .Include(x => x.SupersededByMembership).ThenInclude(x => x!.MembershipPlan)
                .Where(x => x.CustomerId == customerId)
                .OrderByDescending(x => x.StartDate)
                .ThenByDescending(x => x.Id)
                .ToListAsync();

        private async Task<CustomerMembership?> LoadCurrentAsync(int customerId)
        {
            var today = todayDate();
            return await _dbContext.CustomerMemberships
                .AsNoTracking()
                .Include(x => x.MembershipPlan)
                .Where(x => x.CustomerId == customerId
                    && x.IsActive
                    && x.Status == MembershipStatus.Active
                    && x.StartDate <= today
                    && x.ExpiryDate >= today)
                .OrderByDescending(x => x.ExpiryDate)
                .ThenByDescending(x => x.Id)
                .FirstOrDefaultAsync();
        }

        /// <summary>
        /// A lapsed membership is still renewable; a membership the customer cancelled is not. This
        /// mirrors the rule enforced by <see cref="MembershipOperationService"/>.
        /// </summary>
        private async Task<CustomerMembership?> LoadRenewableAsync(int customerId) =>
            await _dbContext.CustomerMemberships
                .AsNoTracking()
                .Include(x => x.MembershipPlan)
                .Where(x => x.CustomerId == customerId
                    && (x.Status == MembershipStatus.Active || x.Status == MembershipStatus.Expired))
                .OrderByDescending(x => x.ExpiryDate)
                .ThenByDescending(x => x.Id)
                .FirstOrDefaultAsync();

        private async Task<MembershipCheckoutSession> CreateSessionAsync(int customerId, MembershipQuote quote, SystemSettings settings)
        {
            var plan = quote.Plan!;
            var customer = await _dbContext.Customers.AsNoTracking().FirstOrDefaultAsync(x => x.Id == customerId);

            return new MembershipCheckoutSession
            {
                CheckoutToken = Guid.NewGuid().ToString("N"),
                CustomerId = customerId,
                MembershipPlanId = plan.Id,
                ExistingMembershipId = quote.Current?.Id,
                IsRenewal = quote.Operation == MembershipOperation.Renew,
                Operation = quote.Operation,
                ArenaName = settings.ArenaName,
                CustomerName = customer?.FullName ?? string.Empty,
                CustomerCode = customer?.CustomerCode ?? string.Empty,
                PlanName = plan.Name,
                PlanCode = plan.Code,
                PlanDescription = plan.Description,
                Benefits = plan.Benefits,
                DurationMonths = plan.DurationMonths,
                DiscountPercentage = plan.DiscountPercentage,
                IncludedBookingHours = quote.IncludedHoursAfterOperation,
                StartDate = quote.StartDate,
                ExpiryDate = quote.ExpiryDate,
                CurrencySymbol = settings.CurrencySymbol ?? "Rs",
                PreviousPlanName = quote.PreviousPlanName,
                PreviousPlanCode = quote.PreviousPlanCode,
                PreviousPlanFee = quote.PreviousPlanFee,
                FeeAmount = quote.FeeAmount,
                TaxAmount = quote.TaxAmount,
                TotalAmount = quote.TotalAmount,
                CreatedAtUtc = DateTime.UtcNow,
                ExpiresAtUtc = DateTime.UtcNow.AddMinutes(15)
            };
        }

        private Task<List<MembershipPlan>> LoadPlansAsync() => _dbContext.MembershipPlans
            .AsNoTracking()
            .Where(x => x.IsActive)
            .OrderBy(x => x.DisplayOrder)
            .ThenBy(x => x.JoiningFee)
            .ToListAsync();

        private static MembershipPlanCardViewModel MapPlan(
            MembershipPlan plan,
            int? currentPlanId,
            MembershipOperation operation,
            MembershipQuote? selectedQuote)
        {
            var fee = operation == MembershipOperation.Renew ? plan.RenewalFee : plan.JoiningFee;
            return new MembershipPlanCardViewModel
            {
                Id = plan.Id,
                Name = plan.Name,
                Code = plan.Code,
                Description = plan.Description,
                DurationMonths = plan.DurationMonths,
                JoiningFee = plan.JoiningFee,
                RenewalFee = plan.RenewalFee,
                DiscountPercentage = plan.DiscountPercentage,
                IncludedBookingHours = plan.IncludedBookingHours,
                PriorityBookingDays = plan.PriorityBookingDays,
                AllowPeakHours = plan.AllowPeakHours,
                AllowOffPeakHours = plan.AllowOffPeakHours,
                AllowedSportCodes = plan.AllowedSportCodes,
                Benefits = plan.Benefits,
                IsCurrentPlan = currentPlanId == plan.Id,
                PayableAmount = selectedQuote != null && selectedQuote.Plan?.Id == plan.Id
                    ? selectedQuote.TotalAmount
                    : fee,
                DurationLabel = plan.DurationMonths == 1 ? "1 month" : $"{plan.DurationMonths} months"
            };
        }

        private static MembershipPlanCardViewModel MapUpgradePlan(MembershipPlan plan, CustomerMembership current, MembershipQuote selectedQuote)
        {
            var isSelected = selectedQuote.Plan?.Id == plan.Id;
            return new MembershipPlanCardViewModel
            {
                Id = plan.Id,
                Name = plan.Name,
                Code = plan.Code,
                Description = plan.Description,
                DurationMonths = plan.DurationMonths,
                JoiningFee = plan.JoiningFee,
                RenewalFee = plan.RenewalFee,
                DiscountPercentage = plan.DiscountPercentage,
                IncludedBookingHours = isSelected ? selectedQuote.IncludedHoursAfterOperation : plan.IncludedBookingHours,
                PriorityBookingDays = plan.PriorityBookingDays,
                AllowPeakHours = plan.AllowPeakHours,
                AllowOffPeakHours = plan.AllowOffPeakHours,
                AllowedSportCodes = plan.AllowedSportCodes,
                Benefits = plan.Benefits,
                IsCurrentPlan = false,
                IsUpgrade = true,
                PayableAmount = isSelected ? selectedQuote.TotalAmount : plan.JoiningFee,
                CurrentPlanFee = current.JoiningFee,
                DurationLabel = plan.DurationMonths == 1 ? "1 month" : $"{plan.DurationMonths} months"
            };
        }

        private static MembershipHistoryItemViewModel MapHistory(CustomerMembership membership, string currencySymbol = "Rs") => new()
        {
            CurrencySymbol = currencySymbol,
            Id = membership.Id,
            MembershipNumber = membership.MembershipNumber,
            PlanName = membership.MembershipPlan.Name,
            PlanCode = membership.MembershipPlan.Code,
            StartDate = membership.StartDate,
            ExpiryDate = membership.ExpiryDate,
            Status = membership.ExpiryDate < DateOnly.FromDateTime(DateTime.UtcNow) && membership.Status == MembershipStatus.Active
                ? MembershipStatus.Expired.ToString()
                : membership.Status.ToString(),
            JoiningFee = membership.JoiningFee,
            RenewalFee = membership.RenewalFee,
            AutoRenew = membership.AutoRenew,
            IsActive = membership.IsActive,
            CancelledAt = membership.CancelledAt?.ToString("MMM dd, yyyy"),
            CancellationReason = membership.CancellationReason,
            CreatedAt = membership.CreatedAt,
            IncludedBookingHours = membership.IncludedBookingHours,
            UsedBookingHours = membership.UsedBookingHours
        };

        private void StoreSession(MembershipCheckoutSession session) =>
            HttpContext.Session.SetString(MembershipCheckoutSessionKey, JsonSerializer.Serialize(session));

        private static DateOnly todayDate() => DateOnly.FromDateTime(DateTime.UtcNow);

        private int CurrentCustomerId() => int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : 0;

        private async Task<SystemSettings> GetSettingsAsync() =>
            await _dbContext.SystemSettings.AsNoTracking().FirstOrDefaultAsync()
            ?? new SystemSettings { ArenaName = "GameHub Arena", Currency = "PKR", CurrencySymbol = "Rs" };
    }
}
