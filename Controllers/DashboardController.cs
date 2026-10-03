using GameHub.Data;
using GameHub.Filters;
using GameHub.Helpers;
using GameHub.Models.Entities;
using GameHub.Models.Enums;
using GameHub.Models.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GameHub.Controllers
{
    [GameHubAuthorize(
        UserRole.SuperAdmin,
        UserRole.Admin,
        UserRole.BookingManager,
        UserRole.CourtManager,
        UserRole.FinanceManager,
        UserRole.StaffManager,
        UserRole.Receptionist,
        UserRole.Viewer)]
    [ValidateActiveUser]
    public class DashboardController : Controller
    {
        private readonly ApplicationDbContext _db;
        public DashboardController(ApplicationDbContext db) => _db = db;

        public async Task<IActionResult> Index()
        {
            var now = DateTime.UtcNow;
            var today = DateOnly.FromDateTime(now);
            var monthStart = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
            var last7DaysStart = now.Date.AddDays(-6);
            var last30DaysStart = now.Date.AddDays(-30);

            var settings = await _db.SystemSettings.AsNoTracking().FirstOrDefaultAsync()
                ?? new SystemSettings { CurrencySymbol = "Rs", OpeningTime = new TimeOnly(6, 0), ClosingTime = new TimeOnly(23, 0) };
            var firstName = User.FindFirst(System.Security.Claims.ClaimTypes.GivenName)?.Value ?? "User";
            var hour = DateTime.Now.Hour;

            var model = new DashboardViewModel
            {
                Greeting = hour < 12 ? "Good Morning" : hour < 17 ? "Good Afternoon" : "Good Evening",
                FirstName = firstName,
                CurrencySymbol = settings.CurrencySymbol ?? "Rs"
            };

            var todaysBookingsQuery = _db.Bookings.AsNoTracking()
                .Where(x => x.BookingDate == today && x.IsActive && x.Status != BookingStatus.Cancelled && x.Status != BookingStatus.NoShow);
            model.TodaysBookings = await todaysBookingsQuery.CountAsync();

            model.TodaysRevenue = await _db.PaymentTransactions.AsNoTracking()
                .Where(x => x.Status == PaymentTransactionStatus.Succeeded && x.CompletedAt != null && x.CompletedAt.Value.Date == now.Date)
                .SumAsync(x => (decimal?)x.Amount) ?? 0;

            var activeCourts = await _db.Courts.AsNoTracking().CountAsync(x => x.IsActive);
            var occupiedCourts = await _db.Courts.AsNoTracking().CountAsync(x => x.IsActive && x.Status != CourtStatus.Available);
            model.CourtOccupancyPercent = activeCourts == 0 ? 0 : Math.Round((decimal)occupiedCourts / activeCourts * 100, 1);
            model.CourtsTotal = activeCourts;
            model.CourtsAvailable = activeCourts - occupiedCourts;

            model.ActiveCustomers = await _db.Customers.AsNoTracking().CountAsync(x => x.IsActive);

            var outstandingQuery = _db.Bookings.AsNoTracking()
                .Where(x => x.IsActive && x.BalanceAmount > 0 && (x.PaymentStatus == PaymentStatus.Unpaid || x.PaymentStatus == PaymentStatus.PartiallyPaid));
            model.PendingPaymentsTotal = await outstandingQuery.SumAsync(x => (decimal?)x.BalanceAmount) ?? 0;
            model.PendingPaymentsCount = await outstandingQuery.CountAsync();

            var courtsAll = await _db.Courts.AsNoTracking()
                .Include(c => c.Sport)
                .Where(c => c.IsActive)
                .Select(c => new { c.Id, c.Name, Status = c.Status, SportIcon = c.Sport.IconClass })
                .ToListAsync();

            var courtIds = courtsAll.Select(c => c.Id).ToList();
            var nowTime = TimeOnly.FromDateTime(now);

            var currentBookings = courtIds.Count > 0
                ? await _db.Bookings.AsNoTracking()
                    .Where(b => courtIds.Contains(b.CourtId) && b.BookingDate == today && b.IsActive
                        && b.Status != BookingStatus.Cancelled && b.Status != BookingStatus.NoShow
                        && b.StartTime <= nowTime && b.EndTime > nowTime)
                    .Select(b => new { b.CourtId, b.StartTime, b.EndTime })
                    .ToListAsync()
                : new();

            var nextBookings = courtIds.Count > 0
                ? await _db.Bookings.AsNoTracking()
                    .Where(b => courtIds.Contains(b.CourtId) && b.BookingDate == today && b.IsActive
                        && b.Status != BookingStatus.Cancelled && b.Status != BookingStatus.NoShow
                        && b.StartTime > nowTime)
                    .OrderBy(b => b.StartTime)
                    .Select(b => new { b.CourtId, b.StartTime })
                    .GroupBy(b => b.CourtId)
                    .Select(g => g.First())
                    .ToListAsync()
                : new();

            var statusOrder = new Dictionary<CourtStatus, int> {
                { CourtStatus.Occupied, 0 }, { CourtStatus.Reserved, 1 },
                { CourtStatus.Maintenance, 2 }, { CourtStatus.Closed, 3 }, { CourtStatus.Available, 4 }
            };

            model.LiveCourts = courtsAll
                .OrderBy(c => statusOrder.GetValueOrDefault(c.Status, 4))
                .Take(6)
                .Select(c =>
                {
                    var curBooking = currentBookings.FirstOrDefault(b => b.CourtId == c.Id);
                    var nxtBooking = nextBookings.FirstOrDefault(b => b.CourtId == c.Id);
                    var detail = c.Status switch
                    {
                        CourtStatus.Occupied => curBooking != null
                            ? $"{curBooking.StartTime:hh:mm tt} to {curBooking.EndTime:hh:mm tt}" : "Currently in use",
                        CourtStatus.Reserved => nxtBooking != null
                            ? $"Next booking {nxtBooking.StartTime:hh:mm tt}" : "Reserved",
                        CourtStatus.Maintenance => "Maintenance in progress",
                        CourtStatus.Closed => "Temporarily closed",
                        CourtStatus.Available => nxtBooking != null
                            ? $"Next booking {nxtBooking.StartTime:hh:mm tt}" : "No upcoming bookings",
                        _ => ""
                    };
                    return new DashboardCourtItem
                    {
                        Name = c.Name,
                        Status = c.Status.ToString(),
                        StatusClass = c.Status.ToString().ToLowerInvariant(),
                        Detail = detail
                    };
                }).ToList();

            var scheduleData = await _db.Bookings.AsNoTracking()
                .Include(b => b.Customer)
                .Include(b => b.Court).ThenInclude(c => c.Sport)
                .Where(b => b.BookingDate == today && b.IsActive
                    && b.Status != BookingStatus.Cancelled && b.Status != BookingStatus.NoShow)
                .OrderBy(b => b.StartTime)
                .Take(10)
                .Select(b => new
                {
                    b.StartTime, b.DurationMinutes,
                    b.Customer.FirstName, b.Customer.LastName,
                    CourtName = b.Court.Name,
                    SportIcon = b.Court.Sport.IconClass,
                    b.PaymentStatus, b.Status
                })
                .ToListAsync();

            model.TodaySchedule = scheduleData.Select(b => new DashboardScheduleItem
            {
                Time = b.StartTime.ToString("hh:mm tt"),
                CustomerName = $"{b.FirstName} {b.LastName}",
                Initials = Initials(b.FirstName, b.LastName),
                CourtName = b.CourtName,
                SportIcon = b.SportIcon ?? "bi-dribbble",
                Duration = $"{b.DurationMinutes} min",
                PaymentStatus = b.PaymentStatus == PaymentStatus.Paid ? "Paid"
                    : b.PaymentStatus == PaymentStatus.Unpaid ? "Unpaid" : "Pending",
                PaymentClass = b.PaymentStatus == PaymentStatus.Paid ? "paid"
                    : b.PaymentStatus == PaymentStatus.Unpaid ? "unpaid" : "pending",
                BookingStatus = b.Status.GetDisplayName(),
                StatusClass = b.Status.ToString().ToLowerInvariant()
            }).ToList();

            var revenueRaw = await _db.PaymentTransactions.AsNoTracking()
                .Where(x => x.Status == PaymentTransactionStatus.Succeeded && x.CompletedAt != null && x.CompletedAt.Value >= last7DaysStart)
                .GroupBy(x => x.CompletedAt!.Value.Date)
                .Select(g => new { Date = g.Key, Total = g.Sum(x => x.Amount) })
                .ToDictionaryAsync(g => g.Date, g => g.Total);

            model.RevenueLabels = new string[7];
            model.RevenueData = new decimal[7];
            for (var i = 0; i < 7; i++)
            {
                var date = last7DaysStart.AddDays(i);
                model.RevenueLabels[i] = date.ToString("ddd");
                model.RevenueData[i] = revenueRaw.GetValueOrDefault(date, 0);
            }

            var sportData = await _db.Bookings.AsNoTracking()
                .Where(b => b.BookingDate >= DateOnly.FromDateTime(monthStart) && b.BookingDate <= today && b.IsActive)
                .GroupBy(b => b.Sport.Name)
                .Select(g => new { Sport = g.Key ?? "Other", Count = g.Count() })
                .OrderByDescending(x => x.Count)
                .ToListAsync();

            model.SportLabels = sportData.Select(x => x.Sport).ToArray();
            model.SportData = sportData.Select(x => x.Count).ToArray();

            var statusRaw = await _db.Bookings.AsNoTracking()
                .Where(b => b.IsActive)
                .GroupBy(b => b.Status)
                .Select(g => new { Status = g.Key, Count = g.Count() })
                .ToListAsync();

            var statusMap = statusRaw.ToDictionary(x => x.Status, x => x.Count);
            var order = new[] { BookingStatus.Confirmed, BookingStatus.Pending, BookingStatus.Completed, BookingStatus.Cancelled };
            model.BookingStatusLabels = order.Select(s => s.ToString()).ToArray();
            model.BookingStatusData = order.Select(s => statusMap.GetValueOrDefault(s, 0)).ToArray();
            model.BookingStatusTotal = statusRaw.Sum(x => x.Count);

            var courtPerfRaw = await _db.Bookings.AsNoTracking()
                .Where(b => b.BookingDate >= DateOnly.FromDateTime(last30DaysStart) && b.BookingDate <= today
                    && b.IsActive && b.Status != BookingStatus.Cancelled)
                .GroupBy(b => b.CourtId)
                .Select(g => new { CourtId = g.Key, Minutes = g.Sum(x => x.DurationMinutes), Revenue = g.Sum(x => x.TotalAmount) })
                .OrderByDescending(x => x.Minutes)
                .ToListAsync();

            Dictionary<int, (string Name, string Icon)> courtNames;
            if (courtPerfRaw.Count > 0)
            {
                courtNames = await _db.Courts.AsNoTracking()
                    .Include(c => c.Sport)
                    .Where(c => courtPerfRaw.Select(x => x.CourtId).Contains(c.Id))
                    .ToDictionaryAsync(c => c.Id, c => (c.Name, c.Sport.IconClass ?? "bi-dribbble"));
            }
            else
            {
                courtNames = new();
            }

            var openMinutesPerDay = (int)(settings.ClosingTime - settings.OpeningTime).TotalMinutes;
            var totalAvailMinutes = Math.Max(1, openMinutesPerDay * 30);

            model.TopCourts = courtPerfRaw.Select((x, i) =>
            {
                courtNames.TryGetValue(x.CourtId, out var cn);
                return new DashboardTopCourtItem
                {
                    Rank = i + 1,
                    Name = cn.Name ?? "Court",
                    SportIcon = cn.Icon ?? "bi-dribbble",
                    OccupancyPercent = Math.Min(100, (int)Math.Round((decimal)x.Minutes / totalAvailMinutes * 100)),
                    Revenue = x.Revenue
                };
            }).Take(4).ToList();

            var heatRows = await _db.Bookings.AsNoTracking()
                .Where(b => b.BookingDate >= DateOnly.FromDateTime(last7DaysStart) && b.BookingDate <= today
                    && b.IsActive && b.Status != BookingStatus.Cancelled)
                .Select(b => new { b.BookingDate, b.StartTime })
                .ToListAsync();

            var heatRaw = heatRows
                .GroupBy(b => new { DOW = b.BookingDate.DayOfWeek, Hour = b.StartTime.Hour })
                .Select(g => new { g.Key.DOW, g.Key.Hour, Count = g.Count() })
                .ToList();

            model.HeatmapValues = new int[7][];
            for (var d = 0; d < 7; d++) model.HeatmapValues[d] = new int[6];
            foreach (var h in heatRaw)
            {
                var dayIdx = ((int)h.DOW + 6) % 7;
                var slot = h.Hour switch { < 9 => 0, < 12 => 1, < 15 => 2, < 18 => 3, < 21 => 4, _ => 5 };
                if (dayIdx >= 0 && dayIdx < 7) model.HeatmapValues[dayIdx][slot] = h.Count;
            }

            var monthBookings = await _db.Bookings.AsNoTracking()
                .Where(b => b.BookingDate >= DateOnly.FromDateTime(monthStart) && b.BookingDate <= today && b.IsActive)
                .GroupBy(b => b.CustomerId)
                .Select(g => new { Count = g.Count(), Total = g.Sum(x => x.TotalAmount) })
                .ToListAsync();

            model.NewCustomers = await _db.Customers.AsNoTracking()
                .CountAsync(x => x.IsActive && x.CreatedAt >= monthStart);

            var totalDistinctCustomers = monthBookings.Count;
            var returningCount = monthBookings.Count(x => x.Count > 1);
            model.ReturningCustomerPercent = totalDistinctCustomers == 0 ? 0
                : Math.Round((decimal)returningCount / totalDistinctCustomers * 100);
            model.AvgBookingValue = monthBookings.Count == 0 ? 0
                : Math.Round(monthBookings.Average(x => x.Total));

            model.MembershipRenewals = await _db.CustomerMemberships.AsNoTracking()
                .CountAsync(x => x.RenewalFee > 0 && x.UpdatedAt != null && x.UpdatedAt >= monthStart);

            model.RecentActivity = await BuildRecentActivityAsync(now);
            model.AttentionItems = await BuildAttentionAsync();
            model.QuickActions = BuildQuickActions();

            return View(model);
        }

        private async Task<List<DashboardActivityItem>> BuildRecentActivityAsync(DateTime now)
        {
            var todayStart = now.Date;
            var items = new List<DashboardActivityItem>();

            var recentBookings = await _db.Bookings.AsNoTracking()
                .Include(b => b.Customer).Include(b => b.Court)
                .Where(b => b.IsActive && b.UpdatedAt != null && b.UpdatedAt >= todayStart)
                .OrderByDescending(b => b.UpdatedAt)
                .Take(4)
                .Select(b => new { b.Status, b.UpdatedAt, CourtName = b.Court!.Name, b.Customer!.FirstName, b.Customer.LastName, b.StartTime })
                .ToListAsync();

            foreach (var b in recentBookings)
            {
                var (icon, title) = b.Status switch
                {
                    BookingStatus.Cancelled => ("bi-x-circle", "Booking cancelled"),
                    BookingStatus.Completed => ("bi-check-circle", "Booking completed"),
                    BookingStatus.Confirmed => ("bi-plus-circle", "Booking confirmed"),
                    BookingStatus.CheckedIn => ("bi-box-arrow-in-right", "Customer checked in"),
                    _ => ("bi-calendar2-week", "Booking updated")
                };
                items.Add(new DashboardActivityItem
                {
                    Icon = icon, Title = title,
                    Detail = $"{b.CourtName} — {b.StartTime:hh:mm tt}",
                    TimeAgo = TimeAgo(now, b.UpdatedAt!.Value),
                    SortKey = b.UpdatedAt!.Value
                });
            }

            var recentPayments = await _db.PaymentTransactions.AsNoTracking()
                .Include(x => x.Customer)
                .Where(x => x.CompletedAt != null && x.CompletedAt >= todayStart && x.Status == PaymentTransactionStatus.Succeeded)
                .OrderByDescending(x => x.CompletedAt)
                .Take(3)
                .Select(x => new { x.Amount, x.CompletedAt, x.Customer!.FirstName, x.Customer.LastName, x.CurrencySymbol })
                .ToListAsync();

            foreach (var p in recentPayments)
            {
                items.Add(new DashboardActivityItem
                {
                    Icon = "bi-cash-stack",
                    Title = $"Payment of {p.CurrencySymbol} {p.Amount:N0} received",
                    Detail = $"{p.FirstName} {p.LastName}",
                    TimeAgo = TimeAgo(now, p.CompletedAt!.Value),
                    SortKey = p.CompletedAt!.Value
                });
            }

            var recentRequests = await _db.CustomerBookingRequests.AsNoTracking()
                .Include(r => r.Customer).Include(r => r.Booking).ThenInclude(b => b!.Court)
                .Where(r => r.Status == CustomerBookingRequestStatus.Pending)
                .OrderByDescending(r => r.RequestedAt)
                .Take(2)
                .Select(r => new { r.RequestType, r.RequestedAt, r.Customer!.FirstName, r.Customer.LastName, CourtName = r.Booking!.Court!.Name })
                .ToListAsync();

            foreach (var r in recentRequests)
            {
                var action = r.RequestType == CustomerBookingRequestType.Cancellation ? "Cancellation" : "Reschedule";
                items.Add(new DashboardActivityItem
                {
                    Icon = "bi-arrow-left-right",
                    Title = $"{action} requested",
                    Detail = $"{r.FirstName} {r.LastName} — {r.CourtName}",
                    TimeAgo = TimeAgo(now, r.RequestedAt),
                    SortKey = r.RequestedAt
                });
            }

            return items.OrderByDescending(x => x.SortKey).Take(6).ToList();
        }

        private async Task<List<DashboardAttentionItem>> BuildAttentionAsync()
        {
            var items = new List<DashboardAttentionItem>();

            var pendingPayCount = await _db.Bookings.AsNoTracking()
                .CountAsync(x => x.IsActive && x.BalanceAmount > 0
                    && (x.PaymentStatus == PaymentStatus.Unpaid || x.PaymentStatus == PaymentStatus.PartiallyPaid));
            if (pendingPayCount > 0)
                items.Add(new DashboardAttentionItem
                {
                    Tone = "warning", Label = "Warning",
                    Message = $"{pendingPayCount} pending payment{(pendingPayCount != 1 ? "s" : "")}"
                });

            var maintCourts = await _db.Courts.AsNoTracking()
                .Where(x => x.IsActive && x.Status == CourtStatus.Maintenance)
                .Select(x => x.Name).ToListAsync();
            foreach (var c in maintCourts.Take(2))
                items.Add(new DashboardAttentionItem { Tone = "danger", Label = "Danger", Message = $"{c} maintenance due" });

            var pendingReqs = await _db.CustomerBookingRequests.AsNoTracking()
                .CountAsync(x => x.Status == CustomerBookingRequestStatus.Pending);
            if (pendingReqs > 0)
                items.Add(new DashboardAttentionItem
                {
                    Tone = "info", Label = "Info",
                    Message = $"{pendingReqs} booking{(pendingReqs != 1 ? "s" : "")} awaiting confirmation"
                });

            var failedPayments = await _db.PaymentTransactions.AsNoTracking()
                .CountAsync(x => x.Status == PaymentTransactionStatus.Failed && x.FailedAt != null && x.FailedAt >= DateTime.UtcNow.AddDays(-7));
            if (failedPayments > 0)
                items.Add(new DashboardAttentionItem
                {
                    Tone = "danger", Label = "Danger",
                    Message = $"{failedPayments} failed payment{(failedPayments != 1 ? "s" : "")} this week"
                });

            return items;
        }

        private List<DashboardQuickAction> BuildQuickActions()
        {
            var user = User;
            var actions = new List<DashboardQuickAction>();

            if (RolePermissions.CanAccessModule(user, "Bookings"))
                actions.Add(new DashboardQuickAction { Icon = "bi-calendar-plus", Label = "Create Booking", Url = Url.Action("Index", "Bookings", new { view = "list" }) ?? "#" });
            if (RolePermissions.CanAccessModule(user, "Customers"))
                actions.Add(new DashboardQuickAction { Icon = "bi-person-plus", Label = "Register Customer", Url = Url.Action("Index", "Customers") ?? "#" });
            if (RolePermissions.CanAccessModule(user, "Payments"))
                actions.Add(new DashboardQuickAction { Icon = "bi-wallet2", Label = "Add Payment", Url = Url.Action("Index", "Payments") ?? "#" });
            if (RolePermissions.CanAccessModule(user, "ArenaSetup"))
            {
                actions.Add(new DashboardQuickAction { Icon = "bi-lock", Label = "Block Court", Url = Url.Action("Index", "ArenaSetup", new { tab = "courts" }) ?? "#" });
                actions.Add(new DashboardQuickAction { Icon = "bi-tools", Label = "Schedule Maintenance", Url = Url.Action("Index", "ArenaSetup", new { tab = "courts" }) ?? "#" });
            }
            if (RolePermissions.CanAccessModule(user, "Reports"))
                actions.Add(new DashboardQuickAction { Icon = "bi-file-earmark-bar-graph", Label = "Generate Report", Url = Url.Action("Index", "Reports") ?? "#" });

            return actions;
        }

        private static string TimeAgo(DateTime now, DateTime then)
        {
            var diff = now - then;
            if (diff.TotalMinutes < 1) return "just now";
            if (diff.TotalMinutes < 60) return $"{(int)diff.TotalMinutes} min ago";
            if (diff.TotalHours < 24) return $"{(int)diff.TotalHours} hr ago";
            return $"{(int)diff.TotalDays} day{((int)diff.TotalDays != 1 ? "s" : "")} ago";
        }

        private static string Initials(string? first, string? last)
        {
            var f = string.IsNullOrWhiteSpace(first) ? "" : first.Trim()[0].ToString();
            var l = string.IsNullOrWhiteSpace(last) ? "" : last.Trim()[0].ToString();
            return (f + l).ToUpper();
        }
    }
}
