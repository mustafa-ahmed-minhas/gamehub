using System.Security.Claims;
using GameHub.Data;
using GameHub.Filters;
using GameHub.Helpers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GameHub.Controllers
{
    [CustomerAuthorize]
    [Route("Notifications")]
    public class CustomerNotificationsController : Controller
    {
        private readonly ApplicationDbContext _dbContext;
        public CustomerNotificationsController(ApplicationDbContext dbContext) => _dbContext = dbContext;
        [HttpGet("")]
        public async Task<IActionResult> Index(string filter = "all")
        {
            var query = _dbContext.CustomerNotifications.AsNoTracking().Where(x => x.CustomerId == CurrentCustomerId());
            if (filter == "unread") query = query.Where(x => !x.IsRead);
            if (filter == "booking") query = query.Where(x => x.Type.ToString().Contains("Booking") || x.Type.ToString().Contains("Reschedule"));
            if (filter == "payment") query = query.Where(x => x.Type.ToString().Contains("Payment"));
            if (filter == "membership") query = query.Where(x => x.Type.ToString().Contains("Membership"));
            ViewBag.Filter = filter;
            return View(await query.OrderByDescending(x => x.CreatedAt).ToListAsync());
        }
        [HttpGet("UnreadCount")]
        public async Task<IActionResult> UnreadCount() => Json(new { count = await _dbContext.CustomerNotifications.CountAsync(x => x.CustomerId == CurrentCustomerId() && !x.IsRead) });
        [HttpPost("MarkRead/{id:int}"), ValidateAntiForgeryToken]
        public async Task<IActionResult> MarkRead(int id)
        {
            var item = await _dbContext.CustomerNotifications.FirstOrDefaultAsync(x => x.Id == id && x.CustomerId == CurrentCustomerId());
            if (item != null) { item.IsRead = true; item.ReadAt = DateTime.UtcNow; await _dbContext.SaveChangesAsync(); }
            TempData.SetToast("success", "Updated", "Notification marked as read.");
            return RedirectToAction(nameof(Index));
        }
        [HttpPost("MarkAllRead"), ValidateAntiForgeryToken]
        public async Task<IActionResult> MarkAllRead()
        {
            var items = await _dbContext.CustomerNotifications.Where(x => x.CustomerId == CurrentCustomerId() && !x.IsRead).ToListAsync();
            foreach (var item in items) { item.IsRead = true; item.ReadAt = DateTime.UtcNow; }
            await _dbContext.SaveChangesAsync();
            TempData.SetToast("success", "Updated", "All notifications marked as read.");
            return RedirectToAction(nameof(Index));
        }
        private int CurrentCustomerId() => int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : 0;
    }
}
