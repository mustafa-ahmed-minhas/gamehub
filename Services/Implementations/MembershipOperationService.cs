using GameHub.Data;
using GameHub.Models.Checkout;
using GameHub.Models.Entities;
using GameHub.Models.Enums;
using GameHub.Models.ViewModels;
using GameHub.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace GameHub.Services.Implementations
{
    public class MembershipOperationService : IMembershipOperationService
    {
        private readonly ApplicationDbContext _dbContext;

        public MembershipOperationService(ApplicationDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        private static DateOnly Today => DateOnly.FromDateTime(DateTime.UtcNow);

        /// <summary>
        /// The membership a customer's self-service operations act on. The ordering is explicit
        /// because a customer can briefly hold more than one row: an upgrade creates the new term
        /// while the replaced one is being closed, and both share an expiry date.
        /// </summary>
        private Task<CustomerMembership?> LoadCurrentAsync(int customerId) =>
            _dbContext.CustomerMemberships
                .Include(x => x.MembershipPlan)
                .Where(x => x.CustomerId == customerId
                    && x.IsActive
                    && x.Status == MembershipStatus.Active
                    && x.StartDate <= DateOnly.FromDateTime(DateTime.UtcNow)
                    && x.ExpiryDate >= DateOnly.FromDateTime(DateTime.UtcNow))
                .OrderByDescending(x => x.ExpiryDate)
                .ThenByDescending(x => x.Id)
                .FirstOrDefaultAsync();

        /// <summary>
        /// The membership a renewal applies to. This is deliberately broader than the current one: a
        /// lapsed membership is still renewable, and a membership the customer cancelled is not.
        /// </summary>
        private Task<CustomerMembership?> LoadRenewableAsync(int customerId) =>
            _dbContext.CustomerMemberships
                .Include(x => x.MembershipPlan)
                .Where(x => x.CustomerId == customerId
                    && (x.Status == MembershipStatus.Active || x.Status == MembershipStatus.Expired))
                .OrderByDescending(x => x.ExpiryDate)
                .ThenByDescending(x => x.Id)
                .FirstOrDefaultAsync();

        public async Task<MembershipQuote> QuoteAsync(int customerId, int planId, MembershipOperation operation)
        {
            var today = Today;

            var customer = await _dbContext.Customers.AsNoTracking().FirstOrDefaultAsync(x => x.Id == customerId);
            if (customer == null || !customer.IsActive || customer.IsBlacklisted)
            {
                return Invalid(operation, "Your account is not able to purchase a membership.");
            }

            if (!customer.EmailVerified)
            {
                return Invalid(operation, "Please verify your email address before continuing.");
            }

            var plan = await _dbContext.MembershipPlans.AsNoTracking().FirstOrDefaultAsync(x => x.Id == planId && x.IsActive);
            if (plan == null)
            {
                return Invalid(operation, "That membership plan is no longer available.");
            }

            var current = await LoadCurrentAsync(customerId);
            var settings = await _dbContext.SystemSettings.AsNoTracking().FirstOrDefaultAsync();

            // Same tax rule the booking flow already applies: SystemSettings.TaxPercentage on the fee.
            var taxPercentage = settings?.TaxPercentage ?? 0m;

            switch (operation)
            {
                case MembershipOperation.Join:
                    if (current != null)
                    {
                        return Invalid(operation, "You already have an active membership. Renew or upgrade it instead.");
                    }
                    return Build(operation, plan, null, plan.JoiningFee, taxPercentage, today, today.AddMonths(plan.DurationMonths));

                case MembershipOperation.Renew:
                    var renewable = await LoadRenewableAsync(customerId);
                    if (renewable == null)
                    {
                        return Invalid(operation, "You do not have a membership to renew.");
                    }
                    if (renewable.MembershipPlanId != plan.Id)
                    {
                        return Invalid(operation, "Renewal keeps your current plan. Use Upgrade to change plan.");
                    }
                    {
                        // Reuses the existing renewal rule: the term continues from the current expiry
                        // date, or from today when the membership has already lapsed.
                        var start = renewable.ExpiryDate < today ? today : renewable.ExpiryDate;
                        return Build(operation, plan, renewable, plan.RenewalFee, taxPercentage, start, start.AddMonths(plan.DurationMonths));
                    }

                case MembershipOperation.Upgrade:
                    if (current == null)
                    {
                        return Invalid(operation, "You do not have an active membership to upgrade.");
                    }
                    if (current.MembershipPlanId == plan.Id)
                    {
                        return Invalid(operation, "You are already on this plan.");
                    }
                    if (plan.JoiningFee <= current.JoiningFee)
                    {
                        return Invalid(operation, "This plan is not a higher tier than your current plan.");
                    }
                    {
                        // An upgrade charges only the difference between the configured plan fees, so the
                        // customer never pays twice for the same term. Tax follows the standard rule.
                        var difference = Math.Round(plan.JoiningFee - current.JoiningFee, 2);
                        // Remaining validity is preserved: the upgrade does not extend or shorten the term.
                        return Build(operation, plan, current, difference, taxPercentage, current.StartDate, current.ExpiryDate);
                    }

                default:
                    return Invalid(operation, "Unsupported membership operation.");
            }
        }

        public async Task<List<MembershipPlan>> GetUpgradePlansAsync(int customerId)
        {
            var current = await LoadCurrentAsync(customerId);
            if (current == null) return new List<MembershipPlan>();

            return await _dbContext.MembershipPlans
                .AsNoTracking()
                .Where(x => x.IsActive
                    && x.Id != current.MembershipPlanId
                    && x.JoiningFee > current.JoiningFee)
                .OrderBy(x => x.JoiningFee)
                .ToListAsync();
        }

        public async Task<string?> GetCancellationBlockReasonAsync(int customerId)
        {
            var settings = await _dbContext.SystemSettings.AsNoTracking().FirstOrDefaultAsync();

            // Reuse the existing system switch that governs customer-initiated cancellation.
            if (settings != null && !settings.AllowCustomerCancellation)
            {
                return "Self-service membership cancellation is currently disabled. Please contact the arena to cancel.";
            }

            var current = await LoadCurrentAsync(customerId);
            if (current == null)
            {
                return "You do not have an active membership to cancel.";
            }

            return null;
        }

        public async Task<(int Used, int Included, int Remaining)> GetHoursBalanceAsync(int customerId)
        {
            var current = await LoadCurrentAsync(customerId);
            if (current == null) return (0, 0, 0);
            var used = current.UsedBookingHours;
            var included = current.IncludedBookingHours;
            return (used, included, Math.Max(0, included - used));
        }

        private static MembershipQuote Invalid(MembershipOperation operation, string message) => new()
        {
            IsValid = false,
            Operation = operation,
            Message = message
        };

        private static MembershipQuote Build(
            MembershipOperation operation,
            MembershipPlan plan,
            CustomerMembership? current,
            decimal fee,
            decimal taxPercentage,
            DateOnly start,
            DateOnly expiry)
        {
            fee = Math.Max(0, Math.Round(fee, 2));
            var tax = Math.Round(fee * taxPercentage / 100, 2);
            var remaining = current == null
                ? 0
                : Math.Max(0, current.IncludedBookingHours - current.UsedBookingHours);

            return new MembershipQuote
            {
                IsValid = true,
                Operation = operation,
                Plan = plan,
                Current = current,
                FeeAmount = fee,
                TaxAmount = tax,
                TotalAmount = fee + tax,
                StartDate = start,
                ExpiryDate = expiry,
                PreviousPlanName = current?.MembershipPlan?.Name,
                PreviousPlanCode = current?.MembershipPlan?.Code,
                PreviousPlanFee = current?.JoiningFee ?? 0m,
                RemainingHours = remaining,
                // An upgrade keeps the customer's unused hours even if the new plan's included hours
                // are configured lower, so remaining time can never be reduced by upgrading.
                IncludedHoursAfterOperation = operation == MembershipOperation.Upgrade
                    ? Math.Max(plan.IncludedBookingHours, remaining)
                    : plan.IncludedBookingHours
            };
        }
    }
}
