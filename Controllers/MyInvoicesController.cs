using System.Security.Claims;
using GameHub.Data;
using GameHub.Filters;
using GameHub.Models.Checkout;
using GameHub.Models.Entities;
using GameHub.Models.Enums;
using GameHub.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GameHub.Controllers
{
    [CustomerAuthorize]
    public class MyInvoicesController : Controller
    {
        private readonly ApplicationDbContext _dbContext;
        private readonly IInvoiceService _invoiceService;

        public MyInvoicesController(ApplicationDbContext dbContext, IInvoiceService invoiceService)
        {
            _dbContext = dbContext;
            _invoiceService = invoiceService;
        }

        [HttpGet]
        public async Task<IActionResult> Index(string tab = "all")
        {
            var customerId = CurrentCustomerId();
            var settings = await _dbContext.SystemSettings.AsNoTracking().FirstOrDefaultAsync() ?? new SystemSettings();

            var bookingInvoices = await _dbContext.BookingInvoices
                .AsNoTracking()
                .Include(x => x.Booking)
                .Where(x => x.CustomerId == customerId)
                .OrderByDescending(x => x.InvoiceDate)
                .ToListAsync();

            var membershipInvoices = await _dbContext.MembershipInvoices
                .AsNoTracking()
                .Include(x => x.CustomerMembership)
                    .ThenInclude(x => x.MembershipPlan)
                .Where(x => x.CustomerId == customerId)
                .OrderByDescending(x => x.InvoiceDate)
                .ToListAsync();

            var normalizedTab = new[] { "bookings", "memberships" }.Contains(tab?.ToLowerInvariant() ?? string.Empty)
                ? tab!.ToLowerInvariant()
                : "all";

            ViewBag.Tab = normalizedTab;
            ViewBag.CurrencySymbol = settings.CurrencySymbol ?? "Rs";
            ViewBag.BookingInvoices = normalizedTab is "all" or "bookings" ? bookingInvoices : new List<BookingInvoice>();
            ViewBag.MembershipInvoices = normalizedTab is "all" or "memberships" ? membershipInvoices : new List<MembershipInvoice>();
            ViewBag.BookingCount = bookingInvoices.Count;
            ViewBag.MembershipCount = membershipInvoices.Count;
            ViewBag.Outstanding = bookingInvoices.Where(x => x.BalanceAmount > 0).Sum(x => x.BalanceAmount);

            return View();
        }

        [HttpGet]
        public async Task<IActionResult> Details(int id, string? type)
        {
            var customerId = CurrentCustomerId();
            var normalized = (type ?? string.Empty).Trim().ToLowerInvariant();

            // Booking and membership invoices live in separate tables with independent identity
            // sequences, so a bare id is ambiguous. Resolve strictly by type, and only fall back to
            // the legacy id-only lookup when no type was supplied.
            if (normalized == "membership")
            {
                return await ShowMembershipInvoiceAsync(id, customerId);
            }

            if (normalized == "booking")
            {
                return await ShowBookingInvoiceAsync(id, customerId);
            }

            var bookingInvoice = await _dbContext.BookingInvoices
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == id && x.CustomerId == customerId);

            if (bookingInvoice != null) return await ShowBookingInvoiceAsync(bookingInvoice.Id, customerId);

            return await ShowMembershipInvoiceAsync(id, customerId);
        }

        private async Task<IActionResult> ShowBookingInvoiceAsync(int id, int customerId)
        {
            var bookingInvoice = await _dbContext.BookingInvoices
                .AsNoTracking()
                .Include(x => x.Customer)
                .FirstOrDefaultAsync(x => x.Id == id && x.CustomerId == customerId);

            if (bookingInvoice == null) return RedirectToAction("NotFound", "Error");

            ViewBag.IsMembership = false;
            ViewBag.BookingId = bookingInvoice.BookingId;
            return View("Details", bookingInvoice);
        }

        private async Task<IActionResult> ShowMembershipInvoiceAsync(int id, int customerId)
        {
            var membershipInvoice = await _dbContext.MembershipInvoices
                .AsNoTracking()
                .Include(x => x.Customer)
                .Include(x => x.CustomerMembership)
                    .ThenInclude(x => x.MembershipPlan)
                .FirstOrDefaultAsync(x => x.Id == id && x.CustomerId == customerId);

            if (membershipInvoice == null) return RedirectToAction("NotFound", "Error");

            ViewBag.IsMembership = true;
            return View("Details", membershipInvoice);
        }

        [HttpGet]
        public async Task<IActionResult> Booking(int bookingId)
        {
            var customerId = CurrentCustomerId();
            var owns = await _dbContext.Bookings.AsNoTracking().AnyAsync(x => x.Id == bookingId && x.CustomerId == customerId);
            if (!owns) return RedirectToAction("NotFound", "Error");

            var invoice = await _dbContext.BookingInvoices.AsNoTracking().FirstOrDefaultAsync(x => x.BookingId == bookingId && x.CustomerId == customerId);
            invoice ??= await _invoiceService.EnsureBookingInvoiceAsync(bookingId);
            return RedirectToAction("Details", new { id = invoice.Id, type = "booking" });
        }

        [HttpGet]
        public async Task<IActionResult> Membership(int membershipId)
        {
            var customerId = CurrentCustomerId();
            var membership = await _dbContext.CustomerMemberships
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == membershipId && x.CustomerId == customerId);
            if (membership == null) return RedirectToAction("NotFound", "Error");

            var invoice = await _dbContext.MembershipInvoices.AsNoTracking().FirstOrDefaultAsync(x => x.CustomerMembershipId == membershipId);

            if (invoice == null)
            {
                // Backstop only: a membership created before this screen existed has no invoice yet.
                // Such a record is always the original purchase, because renewals and upgrades each
                // create their own term record with its own invoice.
                invoice = await _invoiceService.EnsureMembershipInvoiceAsync(
                    membershipId,
                    MembershipOperation.Join,
                    membership.JoiningFee);
            }

            return RedirectToAction("Details", new { id = invoice.Id, type = "membership" });
        }

        private int CurrentCustomerId() => int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : 0;
    }
}
