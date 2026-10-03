using GameHub.Models.Checkout;
using GameHub.Models.Entities;
using GameHub.Models.Enums;
using GameHub.Models.ViewModels;

namespace GameHub.Services.Interfaces
{
    /// <summary>
    /// Single source of truth for membership join / renew / upgrade pricing and eligibility.
    /// Reuses the existing plan configuration (JoiningFee, RenewalFee, DurationMonths) and the
    /// existing SystemSettings.TaxPercentage rule. No pricing is invented here.
    /// </summary>
    public interface IMembershipOperationService
    {
        Task<MembershipQuote> QuoteAsync(int customerId, int planId, MembershipOperation operation);

        /// <summary>Plans that are a genuine upgrade over the customer's current plan.</summary>
        Task<List<MembershipPlan>> GetUpgradePlansAsync(int customerId);

        /// <summary>Reason a cancellation is not permitted, or null when it is allowed.</summary>
        Task<string?> GetCancellationBlockReasonAsync(int customerId);

        /// <summary>Hours already used and still available on the current membership.</summary>
        Task<(int Used, int Included, int Remaining)> GetHoursBalanceAsync(int customerId);
    }
}
