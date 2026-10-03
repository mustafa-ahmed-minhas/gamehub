using GameHub.Models.Entities;

namespace GameHub.Services.Interfaces
{
    public interface ICustomerMembershipService
    {
        Task<CustomerMembershipResult> GetActiveMembershipAsync(int customerId, DateOnly date);

        Task<int?> GetApplicableMembershipIdAsync(int customerId, int sportId, DateOnly date, bool isPeak);

        (bool Valid, string Message) ValidateMembershipBenefit(CustomerMembership membership, string? sportCode, bool isPeak);

        Task SyncCustomerSummaryAsync(int customerId);

        Task SyncExpiredMembershipsAsync();

        Task ApplyMembershipHoursAsync(Booking booking);

        Task RestoreMembershipHoursAsync(Booking booking);
    }

    public class CustomerMembershipResult
    {
        public CustomerMembership? Membership { get; init; }
        public int RemainingHours => Membership == null
            ? 0
            : Math.Max(0, Membership.IncludedBookingHours - Membership.UsedBookingHours);
        public int RemainingDays => Membership == null
            ? 0
            : Math.Max(0, Membership.ExpiryDate.DayNumber - DateOnly.FromDateTime(DateTime.UtcNow).DayNumber);
    }
}
