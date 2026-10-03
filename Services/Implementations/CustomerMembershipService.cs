using GameHub.Data;
using GameHub.Models.Entities;
using GameHub.Models.Enums;
using GameHub.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace GameHub.Services.Implementations
{
    public class CustomerMembershipService : ICustomerMembershipService
    {
        private readonly ApplicationDbContext _dbContext;

        public CustomerMembershipService(ApplicationDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        private IQueryable<CustomerMembership> ActiveMembershipQuery(int customerId, DateOnly date) =>
            _dbContext.CustomerMemberships
                .Include(x => x.MembershipPlan)
                .Where(x => x.CustomerId == customerId
                    && x.IsActive
                    && x.Status == MembershipStatus.Active
                    && x.StartDate <= date
                    && x.ExpiryDate >= date);

        public async Task<CustomerMembershipResult> GetActiveMembershipAsync(int customerId, DateOnly date)
        {
            var membership = await ActiveMembershipQuery(customerId, date)
                .OrderByDescending(x => x.ExpiryDate)
                .AsNoTracking()
                .FirstOrDefaultAsync();

            return new CustomerMembershipResult { Membership = membership };
        }

        public async Task<int?> GetApplicableMembershipIdAsync(int customerId, int sportId, DateOnly date, bool isPeak)
        {
            var result = await GetActiveMembershipAsync(customerId, date);
            if (result.Membership == null) return null;

            var sportCode = await _dbContext.Sports
                .AsNoTracking()
                .Where(x => x.Id == sportId)
                .Select(x => x.Code)
                .FirstOrDefaultAsync();

            return ValidateMembershipBenefit(result.Membership, sportCode, isPeak).Valid
                ? result.Membership.Id
                : null;
        }

        public (bool Valid, string Message) ValidateMembershipBenefit(CustomerMembership membership, string? sportCode, bool isPeak)
        {
            var allowed = membership.MembershipPlan?.AllowedSportCodes;
            if (!string.IsNullOrWhiteSpace(allowed)
                && allowed != "ALL"
                && !string.IsNullOrWhiteSpace(sportCode)
                && !allowed.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).Contains(sportCode))
            {
                return (false, "Membership does not allow this sport.");
            }

            if (isPeak && membership.MembershipPlan?.AllowPeakHours != true)
            {
                return (false, "Membership does not allow peak-hour bookings.");
            }

            if (!isPeak && membership.MembershipPlan?.AllowOffPeakHours != true)
            {
                return (false, "Membership does not allow off-peak bookings.");
            }

            return (true, "Membership benefits applied.");
        }

        public async Task SyncCustomerSummaryAsync(int customerId)
        {
            var customer = await _dbContext.Customers.FindAsync(customerId);
            if (customer == null) return;

            var today = DateOnly.FromDateTime(DateTime.UtcNow);

            // A membership only counts as active once its validity window has actually started.
            // Future-dated memberships must not mark the customer as a member yet.
            var active = await _dbContext.CustomerMemberships
                .Where(x => x.CustomerId == customerId
                    && x.IsActive
                    && x.Status == MembershipStatus.Active
                    && x.StartDate <= today
                    && x.ExpiryDate >= today)
                .OrderByDescending(x => x.ExpiryDate)
                .FirstOrDefaultAsync();

            customer.IsMember = active != null;
            customer.MembershipNumber = active?.MembershipNumber;
            customer.MembershipStartDate = active?.StartDate;
            customer.MembershipExpiryDate = active?.ExpiryDate;
            customer.UpdatedAt = DateTime.UtcNow;
            await _dbContext.SaveChangesAsync();
        }

        public async Task SyncExpiredMembershipsAsync()
        {
            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            var expired = await _dbContext.CustomerMemberships
                .Where(x => x.IsActive && x.Status == MembershipStatus.Active && x.ExpiryDate < today)
                .ToListAsync();

            if (expired.Count == 0) return;

            foreach (var item in expired)
            {
                item.Status = MembershipStatus.Expired;
                item.IsActive = false;
                item.UpdatedAt = DateTime.UtcNow;
            }

            await _dbContext.SaveChangesAsync();

            foreach (var customerId in expired.Select(x => x.CustomerId).Distinct())
            {
                await SyncCustomerSummaryAsync(customerId);
            }
        }

        public async Task ApplyMembershipHoursAsync(Booking booking)
        {
            if (booking.MembershipHoursApplied || booking.CustomerMembershipId == null) return;
            if (booking.Status is not (BookingStatus.Confirmed or BookingStatus.Completed)) return;

            var membership = await _dbContext.CustomerMemberships
                .FirstOrDefaultAsync(x => x.Id == booking.CustomerMembershipId.Value);
            if (membership == null) return;

            membership.UsedBookingHours += Math.Max(1, (int)Math.Ceiling(booking.DurationMinutes / 60m));
            membership.UpdatedAt = DateTime.UtcNow;
            booking.MembershipHoursApplied = true;
            await _dbContext.SaveChangesAsync();
        }

        public async Task RestoreMembershipHoursAsync(Booking booking)
        {
            if (!booking.MembershipHoursApplied || booking.CustomerMembershipId == null) return;

            var membership = await _dbContext.CustomerMemberships
                .FirstOrDefaultAsync(x => x.Id == booking.CustomerMembershipId.Value);
            if (membership == null) return;

            membership.UsedBookingHours = Math.Max(0, membership.UsedBookingHours - Math.Max(1, (int)Math.Ceiling(booking.DurationMinutes / 60m)));
            membership.UpdatedAt = DateTime.UtcNow;
            booking.MembershipHoursApplied = false;
            await _dbContext.SaveChangesAsync();
        }
    }
}
