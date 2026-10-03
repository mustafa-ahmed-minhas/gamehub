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
    public class MyBookingsController : Controller
    {
        private readonly ApplicationDbContext _dbContext;
        private readonly IBookingValidationService _validationService;
        private readonly IBookingLifecycleService _lifecycleService;
        private readonly ICustomerMembershipService _membershipService;
        private readonly IInvoiceService _invoiceService;

        public MyBookingsController(
            ApplicationDbContext dbContext,
            IBookingValidationService validationService,
            IBookingLifecycleService lifecycleService,
            ICustomerMembershipService membershipService,
            IInvoiceService invoiceService)
        {
            _dbContext = dbContext;
            _validationService = validationService;
            _lifecycleService = lifecycleService;
            _membershipService = membershipService;
            _invoiceService = invoiceService;
        }

        [HttpGet]
        public async Task<IActionResult> Index(string tab = "upcoming", string? search = null)
        {
            var customerId = CurrentCustomerId();
            await ProcessDueRemindersAsync(customerId);
            await _lifecycleService.SynchronizeAsync(customerId);

            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            var query = CustomerBookings()
                .Include(x => x.Court)
                .Include(x => x.Sport)
                .Include(x => x.Facility)
                .AsQueryable();

            query = ApplyTab(query, tab, today);

            if (!string.IsNullOrWhiteSpace(search))
            {
                var term = search.Trim();
                query = query.Where(x => x.BookingNumber.Contains(term) || x.Court.Name.Contains(term));
            }

            var bookings = await query
                .OrderByDescending(x => x.BookingDate)
                .ThenByDescending(x => x.StartTime)
                .ToListAsync();

            var settings = await GetSettingsAsync();

            ViewBag.Tab = NormalizeTab(tab);
            ViewBag.CurrencySymbol = settings.CurrencySymbol ?? "Rs";
            ViewBag.Upcoming = await CountTabAsync("upcoming", today);
            ViewBag.Completed = await CountTabAsync("completed", today);
            ViewBag.Past = await CountTabAsync("past", today);
            ViewBag.Cancelled = await CountTabAsync("cancelled", today);
            ViewBag.All = await CountTabAsync("all", today);
            // Total spend counts every amount actually collected, including partially paid bookings,
            // and excludes cancelled bookings. Cancelled spend is reported separately.
            ViewBag.TotalSpent = await CustomerBookings()
                .Where(x => x.Status != BookingStatus.Cancelled && x.PaidAmount > 0)
                .SumAsync(x => (decimal?)x.PaidAmount) ?? 0;
            ViewBag.CancelledAmount = await CustomerBookings()
                .Where(x => x.Status == BookingStatus.Cancelled && x.PaidAmount > 0)
                .SumAsync(x => (decimal?)x.PaidAmount) ?? 0;

            return View(bookings);
        }

        [HttpGet]
        public async Task<IActionResult> Details(int id)
        {
            var customerId = CurrentCustomerId();
            await ProcessDueRemindersAsync(customerId);
            await _lifecycleService.SynchronizeAsync(customerId);

            var booking = await CustomerBookings()
                .Include(x => x.Court)
                .Include(x => x.Sport)
                .Include(x => x.Facility)
                .Include(x => x.CustomerMembership)
                .ThenInclude(x => x!.MembershipPlan)
                .FirstOrDefaultAsync(x => x.Id == id);

            if (booking == null) return RedirectToAction("NotFound", "Error");

            var settings = await GetSettingsAsync();

            ViewBag.Settings = settings;
            ViewBag.CurrencySymbol = settings.CurrencySymbol ?? "Rs";
            ViewBag.Invoice = await _dbContext.BookingInvoices.AsNoTracking().FirstOrDefaultAsync(x => x.BookingId == id);
            ViewBag.Receipt = await _dbContext.PaymentReceipts.AsNoTracking().FirstOrDefaultAsync(x => x.BookingId == id);
            ViewBag.Reminders = await _dbContext.BookingReminders.AsNoTracking().Where(x => x.BookingId == id && x.IsActive).OrderBy(x => x.ScheduledFor).ToListAsync();
            ViewBag.Requests = await _dbContext.CustomerBookingRequests.AsNoTracking().Where(x => x.BookingId == id).OrderByDescending(x => x.RequestedAt).ToListAsync();
            ViewBag.CanCancel = CanCancel(booking, settings);
            ViewBag.CanReschedule = CanReschedule(booking, settings);

            return View(booking);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Cancel(int id, string? reason)
        {
            var customerId = CurrentCustomerId();
            var settings = await GetSettingsAsync();
            var booking = await _dbContext.Bookings.FirstOrDefaultAsync(x => x.Id == id && x.CustomerId == customerId);

            if (booking == null) return RedirectToAction("NotFound", "Error");

            if (!CanCancel(booking, settings))
            {
                TempData.SetToast("warning", "Not Available", settings.AllowCustomerCancellation
                    ? "This booking can no longer be cancelled because the cancellation cutoff has passed."
                    : "Online cancellation is currently disabled. Please contact the arena.");
                return RedirectToAction(nameof(Details), new { id });
            }

            var cleaned = Clean(reason) ?? "Customer requested cancellation.";
            var now = DateTime.UtcNow;

            // Membership hours are returned to the pool because the slot is released immediately.
            await _membershipService.RestoreMembershipHoursAsync(booking);

            booking.Status = BookingStatus.Cancelled;
            booking.IsActive = false;
            booking.CancelledAt = now;
            booking.CustomerCancelledAt = now;
            booking.CancellationReason = cleaned;
            booking.UpdatedAt = now;

            // Audit trail: the customer requested it and policy allowed it, so it is recorded as approved.
            _dbContext.CustomerBookingRequests.Add(new CustomerBookingRequest
            {
                BookingId = id,
                CustomerId = customerId,
                RequestType = CustomerBookingRequestType.Cancellation,
                Status = CustomerBookingRequestStatus.Approved,
                Reason = cleaned,
                AdminResponse = "Auto-approved: cancelled online within the permitted cutoff.",
                RequestedAt = now,
                ReviewedAt = now
            });

            _dbContext.BookingReminders.RemoveRange(
                await _dbContext.BookingReminders.Where(x => x.BookingId == id && x.IsActive).ToListAsync());

            _dbContext.CustomerNotifications.Add(new CustomerNotification
            {
                CustomerId = customerId,
                BookingId = id,
                Type = CustomerNotificationType.BookingCancelled,
                Title = "Booking Cancelled",
                Message = $"Booking {booking.BookingNumber} was cancelled. Any amount already paid is handled by the arena according to its cancellation policy.",
                ActionUrl = $"/MyBookings/Details/{id}",
                CreatedAt = now
            });

            await _dbContext.SaveChangesAsync();

            TempData.SetToast("success", "Booking Cancelled", "Your booking has been cancelled and the slot released.");
            return RedirectToAction(nameof(Details), new { id });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Reschedule(int id, DateOnly requestedDate, TimeOnly requestedStartTime, string? reason)
        {
            var customerId = CurrentCustomerId();
            var settings = await GetSettingsAsync();
            var booking = await _dbContext.Bookings.FirstOrDefaultAsync(x => x.Id == id && x.CustomerId == customerId);

            if (booking == null) return RedirectToAction("NotFound", "Error");

            if (!CanReschedule(booking, settings))
            {
                TempData.SetToast("warning", "Not Available", settings.AllowCustomerRescheduling
                    ? "This booking can no longer be rescheduled because the reschedule cutoff has passed."
                    : "Online rescheduling is currently disabled. Please contact the arena.");
                return RedirectToAction(nameof(Details), new { id });
            }

            if (string.IsNullOrWhiteSpace(reason) || reason.Trim().Length < 10)
            {
                TempData.SetToast("warning", "Reason Required", "Reschedule reason must be at least 10 characters.");
                return RedirectToAction(nameof(Details), new { id });
            }

            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            if (requestedDate < today || requestedDate > today.AddDays(Math.Max(0, settings.AdvanceBookingDays)))
            {
                TempData.SetToast("warning", "Invalid Date", $"Reschedule date must be between today and {today.AddDays(Math.Max(0, settings.AdvanceBookingDays)):MMM dd, yyyy}.");
                return RedirectToAction(nameof(Details), new { id });
            }

            var requestedEndTime = requestedStartTime.AddMinutes(Math.Max(1, booking.DurationMinutes));
            var availability = await _validationService.CheckAvailabilityAsync(
                booking.CourtId, requestedDate, requestedStartTime, requestedEndTime, excludeBookingId: booking.Id);

            if (!availability.IsAvailable)
            {
                TempData.SetToast("warning", "Slot Unavailable", availability.Message);
                return RedirectToAction(nameof(Details), new { id });
            }

            _dbContext.CustomerBookingRequests.Add(new CustomerBookingRequest
            {
                BookingId = id,
                CustomerId = customerId,
                RequestType = CustomerBookingRequestType.Reschedule,
                Status = CustomerBookingRequestStatus.Pending,
                RequestedDate = requestedDate,
                RequestedStartTime = requestedStartTime,
                RequestedEndTime = requestedEndTime,
                Reason = Clean(reason) ?? "Customer requested reschedule.",
                RequestedAt = DateTime.UtcNow
            });

            booking.CustomerRescheduleRequestedAt = DateTime.UtcNow;
            await _dbContext.SaveChangesAsync();

            TempData.SetToast("success", "Request Submitted", "Reschedule request submitted for review.");
            return RedirectToAction(nameof(Details), new { id });
        }

        [HttpGet]
        public async Task<IActionResult> Invoice(int bookingId)
        {
            var customerId = CurrentCustomerId();
            var owns = await _dbContext.Bookings.AsNoTracking().AnyAsync(x => x.Id == bookingId && x.CustomerId == customerId);
            if (!owns) return RedirectToAction("NotFound", "Error");

            var existing = await _dbContext.BookingInvoices
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.BookingId == bookingId && x.CustomerId == customerId);

            // Older bookings created before online payments existed still get a printable invoice on demand.
            if (existing == null) await _invoiceService.EnsureBookingInvoiceAsync(bookingId);

            var invoice = await _dbContext.BookingInvoices
                .AsNoTracking()
                .Include(x => x.Booking).ThenInclude(x => x.Facility)
                .Include(x => x.Booking).ThenInclude(x => x.CustomerMembership)
                .Include(x => x.Customer)
                .FirstOrDefaultAsync(x => x.BookingId == bookingId && x.CustomerId == customerId);

            if (invoice == null) return RedirectToAction("NotFound", "Error");

            return View("Invoice", invoice);
        }

        [HttpGet]
        public async Task<IActionResult> Receipt(int bookingId)
        {
            var customerId = CurrentCustomerId();
            var owns = await _dbContext.Bookings.AsNoTracking().AnyAsync(x => x.Id == bookingId && x.CustomerId == customerId);
            if (!owns) return RedirectToAction("NotFound", "Error");

            var hasReceipt = await _dbContext.PaymentReceipts.AsNoTracking().AnyAsync(x => x.BookingId == bookingId);

            if (!hasReceipt)
            {
                var transaction = await _dbContext.PaymentTransactions
                    .AsNoTracking()
                    .FirstOrDefaultAsync(x => x.BookingId == bookingId && x.CustomerId == customerId);

                if (transaction == null)
                {
                    TempData.SetToast("info", "No Receipt", "This booking has no completed payment, so no receipt is available.");
                    return RedirectToAction(nameof(Details), new { id = bookingId });
                }

                await _invoiceService.EnsurePaymentReceiptAsync(transaction.Id);
            }

            var receipt = await _dbContext.PaymentReceipts
                .AsNoTracking()
                .Include(x => x.Booking).ThenInclude(x => x.Court)
                .Include(x => x.Booking).ThenInclude(x => x.Sport)
                .Include(x => x.PaymentTransaction)
                .Include(x => x.Customer)
                .FirstOrDefaultAsync(x => x.BookingId == bookingId && x.CustomerId == customerId);

            if (receipt == null) return RedirectToAction("NotFound", "Error");

            return View("Receipt", receipt);
        }

        private static string NormalizeTab(string? tab) =>
            new[] { "upcoming", "completed", "past", "cancelled", "all" }.Contains(tab?.ToLowerInvariant() ?? string.Empty)
                ? tab!.ToLowerInvariant()
                : "upcoming";

        private static IQueryable<Booking> ApplyTab(IQueryable<Booking> query, string tab, DateOnly today) => NormalizeTab(tab) switch
        {
            "upcoming" => query.Where(x => x.BookingDate >= today && x.Status == BookingStatus.Confirmed && x.IsActive),
            "completed" => query.Where(x => x.Status == BookingStatus.Completed),
            "past" => query.Where(x => x.BookingDate < today && (x.Status == BookingStatus.Completed || x.Status == BookingStatus.NoShow)),
            "cancelled" => query.Where(x => x.Status == BookingStatus.Cancelled),
            _ => query
        };

        private Task<int> CountTabAsync(string tab, DateOnly today) =>
            ApplyTab(CustomerBookings(), tab, today).CountAsync();

        private static bool CanCancel(Booking booking, SystemSettings settings) =>
            settings.AllowCustomerCancellation
            && booking.Status == BookingStatus.Confirmed
            && booking.IsActive
            && booking.BookingDate.ToDateTime(booking.StartTime).AddHours(-Math.Max(0, settings.CancellationCutoffHours)) > DateTime.Now;

        private static bool CanReschedule(Booking booking, SystemSettings settings) =>
            settings.AllowCustomerRescheduling
            && booking.Status == BookingStatus.Confirmed
            && booking.IsActive
            && booking.BookingDate.ToDateTime(booking.StartTime).AddHours(-Math.Max(0, settings.RescheduleCutoffHours)) > DateTime.Now;

        private IQueryable<Booking> CustomerBookings() =>
            _dbContext.Bookings.AsNoTracking().Where(x => x.CustomerId == CurrentCustomerId());

        private int CurrentCustomerId() => int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : 0;

        private async Task<SystemSettings> GetSettingsAsync() =>
            await _dbContext.SystemSettings.AsNoTracking().FirstOrDefaultAsync() ?? new SystemSettings();

        private async Task ProcessDueRemindersAsync(int customerId)
        {
            var due = await _dbContext.BookingReminders.Where(x => x.CustomerId == customerId && x.IsActive && !x.IsProcessed && x.ScheduledFor <= DateTime.UtcNow).ToListAsync();
            foreach (var reminder in due)
            {
                _dbContext.CustomerNotifications.Add(new CustomerNotification { CustomerId = customerId, BookingId = reminder.BookingId, Type = CustomerNotificationType.BookingReminder, Title = "Booking Reminder", Message = "You have an upcoming GameHub booking.", ActionUrl = $"/MyBookings/Details/{reminder.BookingId}", CreatedAt = DateTime.UtcNow });
                reminder.IsProcessed = true;
                reminder.DisplayedAt = DateTime.UtcNow;
                reminder.UpdatedAt = DateTime.UtcNow;
            }
            if (due.Count > 0) await _dbContext.SaveChangesAsync();
        }

        private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
