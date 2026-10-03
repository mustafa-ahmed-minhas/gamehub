using System.Linq.Expressions;
using GameHub.Data;
using GameHub.Models.Entities;
using GameHub.Models.Enums;
using GameHub.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace GameHub.Services.Implementations
{
    public class BookingLifecycleService : IBookingLifecycleService
    {
        /// <summary>
        /// Automatic lifecycle enforcement only applies to sessions that end after this instant.
        /// Bookings that had already finished before the feature was deployed are deliberately left
        /// untouched so that historical records are never rewritten without verified evidence.
        /// Staff can still review or close those records from the ERP.
        /// </summary>
        private static readonly DateTime EnforcementStartsFromUtc =
            new(2026, 9, 29, 0, 0, 0, DateTimeKind.Utc);

        private readonly ApplicationDbContext _dbContext;
        private readonly ICustomerMembershipService _membershipService;

        public BookingLifecycleService(ApplicationDbContext dbContext, ICustomerMembershipService membershipService)
        {
            _dbContext = dbContext;
            _membershipService = membershipService;
        }

        public async Task<int> SynchronizeAsync(int customerId)
        {
            var candidates = await LoadPendingAsync(x => x.CustomerId == customerId);
            return await ProcessAsync(candidates);
        }

        public async Task<int> SynchronizeAllAsync()
        {
            var candidates = await LoadPendingAsync(null);
            return await ProcessAsync(candidates);
        }

        private Task<List<Booking>> LoadPendingAsync(Expression<Func<Booking, bool>>? scope)
        {
            var query = _dbContext.Bookings.Where(x => x.Status == BookingStatus.Confirmed || x.Status == BookingStatus.CheckedIn || x.Status == BookingStatus.InProgress);
            if (scope != null) query = query.Where(scope);
            return query
                .Include(x => x.CustomerMembership)
                .ToListAsync();
        }

        private async Task<int> ProcessAsync(List<Booking> candidates)
        {
            if (candidates.Count == 0) return 0;

            var now = DateTime.UtcNow;
            var changed = 0;

            foreach (var booking in candidates)
            {
                var end = booking.BookingDate.ToDateTime(booking.EndTime);
                if (end > now) continue;

                // Never rewrite history: only sessions that ended after the feature went live are
                // advanced automatically. Pre-existing past sessions stay as-is for manual review.
                if (end < EnforcementStartsFromUtc) continue;

                if (booking.Status is BookingStatus.CheckedIn or BookingStatus.InProgress)
                {
                    booking.Status = BookingStatus.Completed;
                    booking.CompletedAt = now;
                    booking.CheckedOutAt = now;
                    booking.UpdatedAt = now;
                    await _membershipService.ApplyMembershipHoursAsync(booking);
                    changed++;
                }
                else if (booking.Status == BookingStatus.Confirmed)
                {
                    await _membershipService.RestoreMembershipHoursAsync(booking);
                    booking.Status = BookingStatus.NoShow;
                    booking.IsActive = false;
                    booking.UpdatedAt = now;
                    changed++;
                }
            }

            if (changed > 0)
            {
                await _dbContext.SaveChangesAsync();
            }

            return changed;
        }
    }
}
