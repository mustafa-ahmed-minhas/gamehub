using System.Security.Claims;
using GameHub.Data;
using GameHub.Filters;
using GameHub.Helpers;
using GameHub.Models.Entities;
using GameHub.Models.Enums;
using GameHub.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GameHub.Controllers
{
    [CustomerAuthorize]
    public class CustomerHistoryController : Controller
    {
        private readonly ApplicationDbContext _dbContext;
        private readonly IBookingLifecycleService _lifecycleService;
        private readonly ICustomerMembershipService _membershipService;

        public CustomerHistoryController(
            ApplicationDbContext dbContext,
            IBookingLifecycleService lifecycleService,
            ICustomerMembershipService membershipService)
        {
            _dbContext = dbContext;
            _lifecycleService = lifecycleService;
            _membershipService = membershipService;
        }

        [HttpGet]
        public async Task<IActionResult> Index(string tab = "all", string? search = null)
        {
            var customerId = CurrentCustomerId();
            await _lifecycleService.SynchronizeAsync(customerId);
            await _membershipService.SyncExpiredMembershipsAsync();

            var settings = await _dbContext.SystemSettings.AsNoTracking().FirstOrDefaultAsync() ?? new SystemSettings();
            var currency = settings.CurrencySymbol ?? "Rs";
            var today = DateOnly.FromDateTime(DateTime.UtcNow);

            var bookings = _dbContext.Bookings
                .AsNoTracking()
                .Include(x => x.Court)
                .Include(x => x.Sport)
                .Include(x => x.Facility)
                .Where(x => x.CustomerId == customerId);

            var normalizedTab = new[] { "bookings", "memberships", "payments" }.Contains(tab?.ToLowerInvariant() ?? string.Empty)
                ? tab!.ToLowerInvariant()
                : "all";

            if (!string.IsNullOrWhiteSpace(search))
            {
                var term = search.Trim();
                bookings = bookings.Where(x => x.BookingNumber.Contains(term) || x.Court.Name.Contains(term));
            }

            var bookingRows = await bookings
                .OrderByDescending(x => x.BookingDate)
                .ThenByDescending(x => x.StartTime)
                .Take(200)
                .ToListAsync();

            var memberships = await _dbContext.CustomerMemberships
                .AsNoTracking()
                .Include(x => x.MembershipPlan)
                .Where(x => x.CustomerId == customerId)
                .OrderByDescending(x => x.StartDate)
                .ThenByDescending(x => x.Id)
                .Take(200)
                .ToListAsync();

            var payments = await _dbContext.PaymentTransactions
                .AsNoTracking()
                .Include(x => x.Booking)
                .Where(x => x.CustomerId == customerId)
                .OrderByDescending(x => x.CreatedAt)
                .Take(200)
                .ToListAsync();

            ViewBag.Tab = normalizedTab;
            ViewBag.CurrencySymbol = currency;
            ViewBag.Bookings = normalizedTab is "all" or "bookings" ? bookingRows : new List<Booking>();
            ViewBag.Memberships = normalizedTab is "all" or "memberships" ? memberships : new List<CustomerMembership>();
            ViewBag.Payments = normalizedTab is "all" or "payments" ? payments : new List<PaymentTransaction>();
            ViewBag.BookingCount = await _dbContext.Bookings.CountAsync(x => x.CustomerId == customerId);
            ViewBag.MembershipCount = await _dbContext.CustomerMemberships.CountAsync(x => x.CustomerId == customerId);
            ViewBag.PaymentCount = await _dbContext.PaymentTransactions.CountAsync(x => x.CustomerId == customerId);

            // Total spend counts every amount actually collected, including partially paid bookings,
            // and excludes cancelled bookings. Cancelled spend is reported separately.
            var totalSpend = await _dbContext.Bookings
                .Where(x => x.CustomerId == customerId && x.Status != BookingStatus.Cancelled && x.PaidAmount > 0)
                .SumAsync(x => (decimal?)x.PaidAmount) ?? 0;

            var membershipSpend = await _dbContext.PaymentTransactions
                .Where(x => x.CustomerId == customerId && x.Status == PaymentTransactionStatus.Succeeded && x.BookingId == null)
                .SumAsync(x => (decimal?)x.Amount) ?? 0;

            ViewBag.TotalSpend = totalSpend + membershipSpend;
            ViewBag.BookingSpend = totalSpend;
            ViewBag.MembershipSpend = membershipSpend;
            ViewBag.CancelledAmount = await _dbContext.Bookings
                .Where(x => x.CustomerId == customerId && x.Status == BookingStatus.Cancelled && x.PaidAmount > 0)
                .SumAsync(x => (decimal?)x.PaidAmount) ?? 0;
            ViewBag.Today = today;

            return View();
        }

        private int CurrentCustomerId() => int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : 0;
    }
}
