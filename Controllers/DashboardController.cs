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
    [ModuleAuthorize(RolePermissions.Modules.Dashboard)]
    [ValidateActiveUser]
    public class DashboardController : Controller
    {
        private readonly ApplicationDbContext _db;
        public DashboardController(ApplicationDbContext db) => _db = db;

        public async Task<IActionResult> Index()
        {
            if (!RolePermissions.TryGetCurrentRole(User, out var role))
            {
                return new ForbidResult(AuthConstants.Scheme);
            }

            return role == UserRole.Admin
                ? await BuildAdminDashboardAsync()
                : await BuildRoleDashboardAsync(role);
        }

        private async Task<IActionResult> BuildAdminDashboardAsync()
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

            return View("Index", model);
        }

        /// <summary>
        /// Builds the dashboard for every non-Admin role. Each branch queries only
        /// the modules that role is allowed to open, so a role dashboard can never
        /// surface another department's data.
        /// </summary>
        private async Task<IActionResult> BuildRoleDashboardAsync(UserRole role)
        {
            var now = DateTime.UtcNow;
            var today = DateOnly.FromDateTime(now);
            var monthStart = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);

            var settings = await _db.SystemSettings.AsNoTracking().FirstOrDefaultAsync();
            var firstName = User.FindFirst(System.Security.Claims.ClaimTypes.GivenName)?.Value ?? "User";
            var hour = DateTime.Now.Hour;

            var model = new RoleDashboardViewModel
            {
                Role = role,
                RoleName = role.GetDisplayName(),
                Greeting = hour < 12 ? "Good Morning" : hour < 17 ? "Good Afternoon" : "Good Evening",
                FirstName = firstName,
                CurrencySymbol = settings?.CurrencySymbol ?? "Rs",
                Subtitle = $"Everything here is scoped to the {role.GetDisplayName()} role."
            };

            switch (role)
            {
                case UserRole.Manager:
                {
                    model.Headline = "Approvals Workspace";
                    model.PanelTitle = "Pending Booking Approvals";
                    model.PanelSubtitle = "Oldest requests first";
                    model.PanelLinkLabel = "Open Approvals";
                    model.PanelController = "Bookings";
                    model.PanelAction = "Approvals";
                    model.EmptyState = "No bookings are waiting for approval.";

                    var pendingApprovals = await _db.Bookings.AsNoTracking()
                        .CountAsync(b => b.IsActive && b.Status == BookingStatus.PendingApproval);
                    var pendingRefunds = await _db.PaymentRefunds.AsNoTracking()
                        .CountAsync(r => r.Status == RefundStatus.Pending || r.Status == RefundStatus.Processing);
                    var decisionsThisMonth = await _db.DocumentApprovals.AsNoTracking()
                        .CountAsync(a => a.ActionAt >= monthStart);

                    model.Kpis.Add(new RoleDashboardKpi { Label = "Pending Bookings", Value = pendingApprovals.ToString("N0"), Hint = "Awaiting your decision", Icon = "bi-hourglass-split" });
                    model.Kpis.Add(new RoleDashboardKpi { Label = "Pending Refunds", Value = pendingRefunds.ToString("N0"), Hint = "Refund requests in queue", Icon = "bi-cash-coin" });
                    model.Kpis.Add(new RoleDashboardKpi { Label = "Decisions This Month", Value = decisionsThisMonth.ToString("N0"), Hint = "Recorded approval actions", Icon = "bi-clipboard-check" });

                    var pending = await _db.Bookings.AsNoTracking()
                        .Include(b => b.Customer)
                        .Include(b => b.Court)
                        .Where(b => b.IsActive && b.Status == BookingStatus.PendingApproval)
                        .OrderBy(b => b.BookingDate).ThenBy(b => b.StartTime)
                        .Take(8)
                        .ToListAsync();

                    model.Rows = pending.Select(b => new RoleDashboardRow
                    {
                        Icon = "bi-calendar2-check",
                        Title = $"{b.BookingNumber} · {b.Customer?.FirstName} {b.Customer?.LastName}".Trim(),
                        Subtitle = $"{b.Court?.Name} · {b.BookingDate:dd MMM yyyy} {b.StartTime:hh:mm tt}",
                        Meta = $"{model.CurrencySymbol} {b.TotalAmount:N0}",
                        Badge = "Awaiting approval",
                        BadgeClass = "pending"
                    }).ToList();

                    model.QuickActions.Add(new RoleDashboardAction { Label = "Booking Approvals", Description = "Review pending booking requests", Controller = "Bookings", Action = "Approvals", Icon = "bi-hourglass-split" });
                    model.QuickActions.Add(new RoleDashboardAction { Label = "Refund Management", Description = "Review pending refunds", Controller = "Bookings", Action = "Refunds", Icon = "bi-cash-coin" });
                    break;
                }

                case UserRole.Receptionist:
                {
                    model.Headline = "Front Desk";
                    model.PanelTitle = "Today's Bookings";
                    model.PanelSubtitle = "Chronological front-desk schedule";
                    model.PanelLinkLabel = "Open Bookings";
                    model.PanelController = "Bookings";
                    model.PanelAction = "Index";
                    model.EmptyState = "No bookings are scheduled for today.";

                    var todaysBookings = await _db.Bookings.AsNoTracking()
                        .CountAsync(b => b.BookingDate == today && b.IsActive
                            && b.Status != BookingStatus.Cancelled && b.Status != BookingStatus.NoShow);
                    var checkedIn = await _db.Bookings.AsNoTracking()
                        .CountAsync(b => b.BookingDate == today && b.IsActive
                            && (b.Status == BookingStatus.CheckedIn || b.Status == BookingStatus.InProgress || b.Status == BookingStatus.Completed));
                    var activeMemberships = await _db.CustomerMemberships.AsNoTracking()
                        .CountAsync(m => m.Status == MembershipStatus.Active);
                    var activeCustomers = await _db.Customers.AsNoTracking()
                        .CountAsync(c => c.IsActive);

                    model.Kpis.Add(new RoleDashboardKpi { Label = "Today's Bookings", Value = todaysBookings.ToString("N0"), Hint = "Scheduled for today", Icon = "bi-calendar-check" });
                    model.Kpis.Add(new RoleDashboardKpi { Label = "Checked In", Value = checkedIn.ToString("N0"), Hint = "Arrived or in play", Icon = "bi-box-arrow-in-right" });
                    model.Kpis.Add(new RoleDashboardKpi { Label = "Active Memberships", Value = activeMemberships.ToString("N0"), Hint = "Members currently active", Icon = "bi-person-badge" });
                    model.Kpis.Add(new RoleDashboardKpi { Label = "Customers", Value = activeCustomers.ToString("N0"), Hint = "Active customer records", Icon = "bi-people" });

                    var schedule = await _db.Bookings.AsNoTracking()
                        .Include(b => b.Customer)
                        .Include(b => b.Court)
                        .Where(b => b.BookingDate == today && b.IsActive
                            && b.Status != BookingStatus.Cancelled && b.Status != BookingStatus.NoShow)
                        .OrderBy(b => b.StartTime)
                        .Take(8)
                        .ToListAsync();

                    model.Rows = schedule.Select(b => new RoleDashboardRow
                    {
                        Icon = "bi-clock",
                        Title = $"{b.StartTime:hh:mm tt} · {b.Customer?.FirstName} {b.Customer?.LastName}".Trim(),
                        Subtitle = $"{b.Court?.Name} · {b.DurationMinutes} min",
                        Meta = b.PaidAmount > 0 ? $"Paid {model.CurrencySymbol} {b.PaidAmount:N0}" : "Payment pending",
                        Badge = b.Status.GetDisplayName(),
                        BadgeClass = b.Status.ToString().ToLowerInvariant()
                    }).ToList();

                    model.QuickActions.Add(new RoleDashboardAction { Label = "New Booking", Description = "Create a front-desk booking", Controller = "Bookings", Action = "Create", Icon = "bi-plus-lg" });
                    model.QuickActions.Add(new RoleDashboardAction { Label = "Bookings", Description = "Manage the booking list", Controller = "Bookings", Action = "Index", Icon = "bi-calendar-check" });
                    model.QuickActions.Add(new RoleDashboardAction { Label = "Memberships", Description = "Memberships and renewals", Controller = "Memberships", Action = "Index", Icon = "bi-person-badge" });
                    model.QuickActions.Add(new RoleDashboardAction { Label = "Customers", Description = "Customer records", Controller = "Customers", Action = "Index", Icon = "bi-people" });
                    break;
                }

                case UserRole.Finance:
                {
                    model.Headline = "Finance Overview";
                    model.PanelTitle = "Recent Payments";
                    model.PanelSubtitle = "Latest successful collections";
                    model.PanelLinkLabel = "Open Payments";
                    model.PanelController = "Payments";
                    model.PanelAction = "Index";
                    model.EmptyState = "No payments have been recorded yet.";

                    var revenueToday = await _db.PaymentTransactions.AsNoTracking()
                        .Where(x => x.Status == PaymentTransactionStatus.Succeeded && x.CompletedAt != null && x.CompletedAt.Value.Date == now.Date)
                        .SumAsync(x => (decimal?)x.Amount) ?? 0;
                    var revenueMonth = await _db.PaymentTransactions.AsNoTracking()
                        .Where(x => x.Status == PaymentTransactionStatus.Succeeded && x.CompletedAt != null && x.CompletedAt.Value >= monthStart)
                        .SumAsync(x => (decimal?)x.Amount) ?? 0;
                    var outstanding = await _db.SalesInvoices.AsNoTracking()
                        .Where(i => i.OutstandingAmount > 0
                            && i.Status != InvoiceStatus.Draft && i.Status != InvoiceStatus.Cancelled && i.Status != InvoiceStatus.Void)
                        .SumAsync(i => (decimal?)i.OutstandingAmount) ?? 0;
                    var outstandingCount = await _db.SalesInvoices.AsNoTracking()
                        .CountAsync(i => i.OutstandingAmount > 0
                            && i.Status != InvoiceStatus.Draft && i.Status != InvoiceStatus.Cancelled && i.Status != InvoiceStatus.Void);
                    var pendingRefunds = await _db.PaymentRefunds.AsNoTracking()
                        .CountAsync(r => r.Status == RefundStatus.Pending || r.Status == RefundStatus.Processing);

                    model.Kpis.Add(new RoleDashboardKpi { Label = "Collected Today", Value = $"{model.CurrencySymbol} {revenueToday:N0}", Hint = "Successful payments", Icon = "bi-cash-stack" });
                    model.Kpis.Add(new RoleDashboardKpi { Label = "This Month", Value = $"{model.CurrencySymbol} {revenueMonth:N0}", Hint = "Month-to-date collections", Icon = "bi-graph-up" });
                    model.Kpis.Add(new RoleDashboardKpi { Label = "Outstanding", Value = $"{model.CurrencySymbol} {outstanding:N0}", Hint = $"{outstandingCount} unpaid invoice(s)", Icon = "bi-receipt" });
                    model.Kpis.Add(new RoleDashboardKpi { Label = "Pending Refunds", Value = pendingRefunds.ToString("N0"), Hint = "Refund requests in queue", Icon = "bi-arrow-counterclockwise" });

                    var payments = await _db.PaymentTransactions.AsNoTracking()
                        .Include(x => x.Customer)
                        .Where(x => x.Status == PaymentTransactionStatus.Succeeded && x.CompletedAt != null)
                        .OrderByDescending(x => x.CompletedAt)
                        .Take(8)
                        .ToListAsync();

                    model.Rows = payments.Select(p => new RoleDashboardRow
                    {
                        Icon = "bi-wallet2",
                        Title = $"{p.CurrencySymbol} {p.Amount:N0} · {p.Customer?.FirstName} {p.Customer?.LastName}".Trim(),
                        Subtitle = $"{p.TransactionNumber} · {p.PaymentMethod.GetDisplayName()}",
                        Meta = p.CompletedAt?.ToLocalTime().ToString("dd MMM, hh:mm tt") ?? string.Empty,
                        Badge = p.Status.GetDisplayName(),
                        BadgeClass = "paid"
                    }).ToList();

                    model.QuickActions.Add(new RoleDashboardAction { Label = "Payments", Description = "Record and reconcile payments", Controller = "Payments", Action = "Index", Icon = "bi-wallet2" });
                    model.QuickActions.Add(new RoleDashboardAction { Label = "Invoices", Description = "Review sales invoices", Controller = "Invoices", Action = "Index", Icon = "bi-receipt" });
                    model.QuickActions.Add(new RoleDashboardAction { Label = "Refund Management", Description = "Process refund requests", Controller = "Bookings", Action = "Refunds", Icon = "bi-cash-coin" });
                    break;
                }

                case UserRole.Sales:
                {
                    model.Headline = "Sales Cycle";
                    model.PanelTitle = "Latest Quotations";
                    model.PanelSubtitle = "Most recent sales documents";
                    model.PanelLinkLabel = "Open Sales";
                    model.PanelController = "Sales";
                    model.PanelAction = "Index";
                    model.EmptyState = "No quotations yet. Start a quotation to begin the sales cycle.";

                    var openQuotationStatuses = new[]
                    {
                        SalesQuotationStatus.Draft, SalesQuotationStatus.Confirmed, SalesQuotationStatus.SubmittedForApproval,
                        SalesQuotationStatus.Approved, SalesQuotationStatus.AcceptedByCustomer, SalesQuotationStatus.RevisedDraft
                    };
                    var openOrderStatuses = new[]
                    {
                        SalesOrderStatus.Draft, SalesOrderStatus.SubmittedForApproval, SalesOrderStatus.Approved,
                        SalesOrderStatus.RevisedDraft, SalesOrderStatus.Confirmed, SalesOrderStatus.InProgress, SalesOrderStatus.ReadyForInvoice
                    };

                    var openQuotations = await _db.SalesQuotations.AsNoTracking()
                        .CountAsync(q => q.IsActive && openQuotationStatuses.Contains(q.Status));
                    var openOrders = await _db.SalesOrders.AsNoTracking()
                        .CountAsync(o => openOrderStatuses.Contains(o.Status));
                    var pipelineValue = await _db.SalesQuotations.AsNoTracking()
                        .Where(q => q.IsActive && openQuotationStatuses.Contains(q.Status))
                        .SumAsync(q => (decimal?)q.GrandTotal) ?? 0;
                    var unpaidInvoices = await _db.SalesInvoices.AsNoTracking()
                        .CountAsync(i => i.OutstandingAmount > 0
                            && i.Status != InvoiceStatus.Draft && i.Status != InvoiceStatus.Cancelled && i.Status != InvoiceStatus.Void);

                    model.Kpis.Add(new RoleDashboardKpi { Label = "Open Quotations", Value = openQuotations.ToString("N0"), Hint = "Quotations in progress", Icon = "bi-file-earmark-text" });
                    model.Kpis.Add(new RoleDashboardKpi { Label = "Open Orders", Value = openOrders.ToString("N0"), Hint = "Orders not yet closed", Icon = "bi-bag-check" });
                    model.Kpis.Add(new RoleDashboardKpi { Label = "Pipeline Value", Value = $"{model.CurrencySymbol} {pipelineValue:N0}", Hint = "Open quotation value", Icon = "bi-graph-up-arrow" });
                    model.Kpis.Add(new RoleDashboardKpi { Label = "Invoices Due", Value = unpaidInvoices.ToString("N0"), Hint = "Awaiting payment", Icon = "bi-receipt" });

                    var quotations = await _db.SalesQuotations.AsNoTracking()
                        .Include(q => q.Customer)
                        .Where(q => q.IsActive)
                        .OrderByDescending(q => q.CreatedAt)
                        .Take(8)
                        .ToListAsync();

                    model.Rows = quotations.Select(q => new RoleDashboardRow
                    {
                        Icon = "bi-file-earmark-text",
                        Title = $"{q.QuotationNumber} · {q.Customer?.FirstName} {q.Customer?.LastName}".Trim(),
                        Subtitle = $"{q.QuotationDate:dd MMM yyyy} · valid until {q.ValidUntil:dd MMM yyyy}",
                        Meta = $"{q.CurrencySymbol} {q.GrandTotal:N0}",
                        Badge = q.Status.GetDisplayName(),
                        BadgeClass = q.Status is SalesQuotationStatus.Rejected or SalesQuotationStatus.Cancelled or SalesQuotationStatus.Expired ? "cancelled" : "pending"
                    }).ToList();

                    model.QuickActions.Add(new RoleDashboardAction { Label = "Quotations", Description = "Create and track quotations", Controller = "Sales", Action = "Index", Icon = "bi-file-earmark-text" });
                    model.QuickActions.Add(new RoleDashboardAction { Label = "Sales Orders", Description = "Manage confirmed orders", Controller = "SalesOrders", Action = "Index", Icon = "bi-bag-check" });
                    model.QuickActions.Add(new RoleDashboardAction { Label = "Invoices", Description = "Invoices from sales orders", Controller = "Invoices", Action = "Index", Icon = "bi-receipt" });
                    break;
                }

                case UserRole.CRM:
                {
                    model.Headline = "Customer Relationship Desk";
                    model.PanelTitle = "Follow-ups Due";
                    model.PanelSubtitle = "Leads with the nearest follow-up";
                    model.PanelLinkLabel = "Open Follow-ups";
                    model.PanelController = "CRM";
                    model.PanelAction = "FollowUps";
                    model.EmptyState = "No follow-ups are scheduled.";

                    var activeCustomers = await _db.Customers.AsNoTracking().CountAsync(c => c.IsActive);
                    var newCustomers = await _db.Customers.AsNoTracking().CountAsync(c => c.IsActive && c.CreatedAt >= monthStart);
                    var openLeadStatuses = new[] { LeadStatus.New, LeadStatus.Contacted, LeadStatus.FollowUp, LeadStatus.Qualified };
                    var openLeads = await _db.Leads.AsNoTracking()
                        .CountAsync(l => l.IsActive && openLeadStatuses.Contains(l.Status));
                    var followUpsDue = await _db.Leads.AsNoTracking()
                        .CountAsync(l => l.IsActive && l.NextFollowUpAt != null && l.NextFollowUpAt <= now);

                    model.Kpis.Add(new RoleDashboardKpi { Label = "Active Customers", Value = activeCustomers.ToString("N0"), Hint = "Customer records", Icon = "bi-people" });
                    model.Kpis.Add(new RoleDashboardKpi { Label = "New This Month", Value = newCustomers.ToString("N0"), Hint = "Recently added customers", Icon = "bi-person-plus" });
                    model.Kpis.Add(new RoleDashboardKpi { Label = "Open Leads", Value = openLeads.ToString("N0"), Hint = "Leads in progress", Icon = "bi-person-lines-fill" });
                    model.Kpis.Add(new RoleDashboardKpi { Label = "Follow-ups Due", Value = followUpsDue.ToString("N0"), Hint = "At or past due", Icon = "bi-alarm" });

                    var leads = await _db.Leads.AsNoTracking()
                        .Where(l => l.IsActive && l.NextFollowUpAt != null)
                        .OrderBy(l => l.NextFollowUpAt)
                        .Take(8)
                        .ToListAsync();

                    model.Rows = leads.Select(l => new RoleDashboardRow
                    {
                        Icon = "bi-person-lines-fill",
                        Title = $"{l.LeadNumber} · {l.CustomerName}",
                        Subtitle = l.InquirySubject ?? l.Source.GetDisplayName(),
                        Meta = l.NextFollowUpAt?.ToLocalTime().ToString("dd MMM, hh:mm tt") ?? string.Empty,
                        Badge = l.Status.GetDisplayName(),
                        BadgeClass = l.NextFollowUpAt <= now ? "pending" : "confirmed"
                    }).ToList();

                    model.QuickActions.Add(new RoleDashboardAction { Label = "Customers", Description = "Customer records", Controller = "Customers", Action = "Index", Icon = "bi-people" });
                    model.QuickActions.Add(new RoleDashboardAction { Label = "Leads", Description = "Manage the lead pipeline", Controller = "CRM", Action = "Index", Icon = "bi-person-lines-fill" });
                    model.QuickActions.Add(new RoleDashboardAction { Label = "Follow-ups", Description = "Scheduled customer follow-ups", Controller = "CRM", Action = "FollowUps", Icon = "bi-alarm" });
                    break;
                }

                case UserRole.HR:
                {
                    model.Headline = "People Operations";
                    model.PanelTitle = "Recent Applications";
                    model.PanelSubtitle = "Latest recruitment activity";
                    model.PanelLinkLabel = "Open Recruitment";
                    model.PanelController = "Recruitment";
                    model.PanelAction = "Index";
                    model.EmptyState = "No job applications have been received yet.";

                    var activeEmployees = await _db.Employees.AsNoTracking()
                        .CountAsync(e => e.IsActive && e.EmploymentStatus == EmploymentStatus.Active);
                    var openPostings = await _db.JobPostings.AsNoTracking()
                        .CountAsync(p => p.Status == JobPostingStatus.Open);
                    var activeApplicationStatuses = new[]
                    {
                        JobApplicationStatus.Submitted, JobApplicationStatus.Screening, JobApplicationStatus.Shortlisted,
                        JobApplicationStatus.Interview, JobApplicationStatus.Offer
                    };
                    var activeApplications = await _db.JobApplications.AsNoTracking()
                        .CountAsync(a => activeApplicationStatuses.Contains(a.Status));
                    var pendingTerminations = await _db.TerminationRequests.AsNoTracking()
                        .CountAsync(t => t.IsActive && t.Status == TerminationStatus.PendingApproval);

                    model.Kpis.Add(new RoleDashboardKpi { Label = "Active Employees", Value = activeEmployees.ToString("N0"), Hint = "Currently employed", Icon = "bi-person-vcard" });
                    model.Kpis.Add(new RoleDashboardKpi { Label = "Open Postings", Value = openPostings.ToString("N0"), Hint = "Accepting applications", Icon = "bi-journal-text" });
                    model.Kpis.Add(new RoleDashboardKpi { Label = "Applications", Value = activeApplications.ToString("N0"), Hint = "In the hiring pipeline", Icon = "bi-inbox" });
                    model.Kpis.Add(new RoleDashboardKpi { Label = "Pending Terminations", Value = pendingTerminations.ToString("N0"), Hint = "Awaiting approval", Icon = "bi-box-arrow-right" });

                    var applications = await _db.JobApplications.AsNoTracking()
                        .Include(a => a.JobPosting)
                        .OrderByDescending(a => a.ApplicationDate)
                        .Take(8)
                        .ToListAsync();

                    model.Rows = applications.Select(a => new RoleDashboardRow
                    {
                        Icon = "bi-file-earmark-person",
                        Title = $"{a.ApplicationNumber} · {a.FirstName} {a.LastName}",
                        Subtitle = a.JobPosting?.JobTitle ?? "Application",
                        Meta = a.ApplicationDate.ToString("dd MMM yyyy"),
                        Badge = a.Status.GetDisplayName(),
                        BadgeClass = a.Status is JobApplicationStatus.Rejected ? "cancelled" : "pending"
                    }).ToList();

                    model.QuickActions.Add(new RoleDashboardAction { Label = "HR Dashboard", Description = "People operations overview", Controller = "HR", Action = "Index", Icon = "bi-people-fill" });
                    model.QuickActions.Add(new RoleDashboardAction { Label = "Employees", Description = "Employee records", Controller = "Employees", Action = "Index", Icon = "bi-person-vcard" });
                    model.QuickActions.Add(new RoleDashboardAction { Label = "Recruitment", Description = "Postings and applications", Controller = "Recruitment", Action = "Index", Icon = "bi-person-plus" });
                    model.QuickActions.Add(new RoleDashboardAction { Label = "HR Reports", Description = "HR reporting", Controller = "HRReports", Action = "Index", Icon = "bi-bar-chart-line" });
                    break;
                }

                case UserRole.Operations:
                {
                    model.Headline = "Operations Floor";
                    model.PanelTitle = "Court Status";
                    model.PanelSubtitle = "Live operational state";
                    model.PanelLinkLabel = "Open Bookings";
                    model.PanelController = "Bookings";
                    model.PanelAction = "Index";
                    model.EmptyState = "No courts are configured.";

                    var todaysBookings = await _db.Bookings.AsNoTracking()
                        .CountAsync(b => b.BookingDate == today && b.IsActive
                            && b.Status != BookingStatus.Cancelled && b.Status != BookingStatus.NoShow);
                    var activeCourts = await _db.Courts.AsNoTracking().CountAsync(c => c.IsActive);
                    var availableCourts = await _db.Courts.AsNoTracking()
                        .CountAsync(c => c.IsActive && c.Status == CourtStatus.Available);
                    var maintenanceCourts = await _db.Courts.AsNoTracking()
                        .CountAsync(c => c.IsActive && c.Status == CourtStatus.Maintenance);

                    model.Kpis.Add(new RoleDashboardKpi { Label = "Today's Bookings", Value = todaysBookings.ToString("N0"), Hint = "On the operational schedule", Icon = "bi-calendar-check" });
                    model.Kpis.Add(new RoleDashboardKpi { Label = "Courts Available", Value = $"{availableCourts} / {activeCourts}", Hint = "Ready for play", Icon = "bi-grid-3x3-gap" });
                    model.Kpis.Add(new RoleDashboardKpi { Label = "In Use", Value = (activeCourts - availableCourts - maintenanceCourts).ToString("N0"), Hint = "Occupied or reserved", Icon = "bi-activity" });
                    model.Kpis.Add(new RoleDashboardKpi { Label = "Maintenance", Value = maintenanceCourts.ToString("N0"), Hint = "Out of service", Icon = "bi-tools" });

                    var courts = await _db.Courts.AsNoTracking()
                        .Include(c => c.Sport)
                        .Where(c => c.IsActive)
                        .OrderBy(c => c.Name)
                        .Take(8)
                        .ToListAsync();

                    model.Rows = courts.Select(c => new RoleDashboardRow
                    {
                        Icon = string.IsNullOrWhiteSpace(c.Sport?.IconClass) ? "bi-grid-3x3-gap" : c.Sport!.IconClass!,
                        Title = c.Name,
                        Subtitle = c.Sport?.Name ?? "Court",
                        Meta = string.Empty,
                        Badge = c.Status.GetDisplayName(),
                        BadgeClass = c.Status == CourtStatus.Available ? "confirmed" : c.Status == CourtStatus.Maintenance || c.Status == CourtStatus.Closed ? "cancelled" : "pending"
                    }).ToList();

                    model.QuickActions.Add(new RoleDashboardAction { Label = "Bookings", Description = "Operational booking list", Controller = "Bookings", Action = "Index", Icon = "bi-calendar-check" });
                    model.QuickActions.Add(new RoleDashboardAction { Label = "New Booking", Description = "Add a booking to the schedule", Controller = "Bookings", Action = "Create", Icon = "bi-plus-lg" });
                    break;
                }
            }

            return View("Role", model);
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
            if (RolePermissions.CanAccessModule(user, RolePermissions.Modules.MasterData))
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
