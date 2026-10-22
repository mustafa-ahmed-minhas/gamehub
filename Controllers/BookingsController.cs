using System.Security.Claims;
using GameHub.Data;
using GameHub.Filters;
using GameHub.Helpers;
using GameHub.Models.Entities;
using GameHub.Models.Enums;
using GameHub.Models.ViewModels;
using GameHub.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace GameHub.Controllers
{
    [GameHubAuthorize(UserRole.SuperAdmin, UserRole.Admin, UserRole.BookingManager, UserRole.Receptionist, UserRole.CourtManager, UserRole.FinanceManager, UserRole.Viewer)]
    [ValidateActiveUser]
    public class BookingsController : Controller
    {
        private static readonly BookingStatus[] BlockingStatuses = { BookingStatus.Pending, BookingStatus.PendingApproval, BookingStatus.Confirmed, BookingStatus.CheckedIn, BookingStatus.InProgress };
        private static readonly int[] PageSizes = { 10, 25, 50 };
        private readonly ApplicationDbContext _dbContext;
        private readonly IBookingNumberService _bookingNumberService;
        private readonly IBookingValidationService _validationService;
        private readonly IInvoiceService _invoiceService;
        private readonly IRefundService _refundService;
        private readonly IBookingApprovalNotifier _bookingApprovalNotifier;
        private readonly ILogger<BookingsController> _logger;

        public BookingsController(ApplicationDbContext dbContext, IBookingNumberService bookingNumberService, IBookingValidationService validationService, IInvoiceService invoiceService, IRefundService refundService, IBookingApprovalNotifier bookingApprovalNotifier, ILogger<BookingsController> logger)
        {
            _dbContext = dbContext;
            _bookingNumberService = bookingNumberService;
            _validationService = validationService;
            _invoiceService = invoiceService;
            _refundService = refundService;
            _bookingApprovalNotifier = bookingApprovalNotifier;
            _logger = logger;
        }

        [HttpGet]
        public async Task<IActionResult> Index(string view = "list", string? search = null, int? sportId = null, int? facilityId = null, int? courtId = null, int? customerId = null, BookingStatus? status = null, PaymentStatus? paymentStatus = null, BookingSource? source = null, DateOnly? dateFrom = null, DateOnly? dateTo = null, string sortBy = "status", string sortDirection = "desc", int page = 1, int pageSize = 10)
        {
            page = Math.Max(1, page);
            pageSize = PageSizes.Contains(pageSize) ? pageSize : 10;
            view = view?.Equals("calendar", StringComparison.OrdinalIgnoreCase) == true ? "calendar" : "list";
            var settings = await GetSettingsAsync();
            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            var model = new BookingIndexViewModel
            {
                View = view, Search = search?.Trim(), SportId = sportId, FacilityId = facilityId, CourtId = courtId, CustomerId = customerId,
                Status = status, PaymentStatus = paymentStatus, Source = source, DateFrom = dateFrom, DateTo = dateTo, SortBy = sortBy,
                SortDirection = sortDirection, CurrentPage = page, PageSize = pageSize, CanManage = CanManage(), CanOperate = CanOperate(),
                CurrencySymbol = settings.CurrencySymbol ?? "Rs",
                TodaysBookings = await _dbContext.Bookings.CountAsync(x => x.BookingDate == today),
                Confirmed = await _dbContext.Bookings.CountAsync(x => x.Status == BookingStatus.Confirmed && x.BookingDate >= today),
                Pending = await _dbContext.Bookings.CountAsync(x => x.Status == BookingStatus.Pending || x.Status == BookingStatus.PendingApproval),
                InProgress = await _dbContext.Bookings.CountAsync(x => x.Status == BookingStatus.InProgress || x.Status == BookingStatus.CheckedIn),
                CompletedToday = await _dbContext.Bookings.CountAsync(x => x.Status == BookingStatus.Completed && x.BookingDate == today),
                Cancelled = await _dbContext.Bookings.CountAsync(x => x.Status == BookingStatus.Cancelled && x.BookingDate >= today.AddDays(-7)),
                TodaysRevenue = await _dbContext.Bookings.Where(x => x.BookingDate == today && x.Status != BookingStatus.Cancelled && x.Status != BookingStatus.NoShow).SumAsync(x => x.PaidAmount),
                Sports = await _dbContext.Sports.AsNoTracking().Where(x => x.IsActive).OrderBy(x => x.DisplayOrder).Select(x => new SelectListItem(x.Name, x.Id.ToString())).ToListAsync(),
                Facilities = await _dbContext.Facilities.AsNoTracking().Where(x => x.IsActive).OrderBy(x => x.DisplayOrder).Select(x => new SelectListItem(x.Name, x.Id.ToString())).ToListAsync(),
                Courts = await _dbContext.Courts.AsNoTracking().Where(x => x.IsActive).OrderBy(x => x.DisplayOrder).Select(x => new SelectListItem(x.Name, x.Id.ToString())).ToListAsync(),
                Customers = await _dbContext.Customers.AsNoTracking().Where(x => x.IsActive).OrderBy(x => x.FirstName).Select(x => new SelectListItem(x.FirstName + " " + x.LastName + " - " + x.CustomerCode, x.Id.ToString())).ToListAsync()
            };

            var activeCourts = await _dbContext.Courts.CountAsync(x => x.IsActive && x.Status != CourtStatus.Closed && x.Status != CourtStatus.Maintenance);
            var busyCourts = await _dbContext.Bookings.Where(x => x.BookingDate == today && BlockingStatuses.Contains(x.Status)).Select(x => x.CourtId).Distinct().CountAsync();
            model.CourtOccupancy = activeCourts == 0 ? 0 : Math.Round((decimal)busyCourts * 100 / activeCourts, 1);

            var query = BuildBookingQuery(model);
            model.TotalRecords = await query.CountAsync();
            model.TotalPages = model.TotalRecords == 0 ? 1 : (int)Math.Ceiling(model.TotalRecords / (double)model.PageSize);
            model.CurrentPage = Math.Min(model.CurrentPage, model.TotalPages);
            var bookingRows = await query.Skip((model.CurrentPage - 1) * model.PageSize).Take(model.PageSize).ToListAsync();
            model.Bookings = bookingRows.Select(MapListItem).ToList();

            var calendarFrom = dateFrom ?? today.AddDays(-14);
            var calendarTo = dateTo ?? today.AddDays(45);
            var calendarRows = await _dbContext.Bookings.AsNoTracking()
                .Include(x => x.Customer).Include(x => x.Court).Include(x => x.Facility).Include(x => x.Sport)
                .Where(x => x.BookingDate >= calendarFrom && x.BookingDate <= calendarTo)
                .ToListAsync();
            model.CalendarEvents = calendarRows.Select(x => new BookingCalendarEventViewModel
                {
                    Id = x.Id,
                    Title = x.Customer.FullName + " - " + x.Court.Name,
                    Start = x.BookingDate.ToString("yyyy-MM-dd") + "T" + x.StartTime.ToString("HH:mm:ss"),
                    End = x.BookingDate.ToString("yyyy-MM-dd") + "T" + x.EndTime.ToString("HH:mm:ss"),
                    Status = x.Status.ToString(),
                    Customer = x.Customer.FullName,
                    Court = x.Court.Name,
                    Sport = x.Sport.Name,
                    Amount = x.TotalAmount,
                    Color = x.Status == BookingStatus.Cancelled ? "#d45b5b" : x.Status == BookingStatus.Completed ? "#2f9b65" : (x.Status == BookingStatus.Pending || x.Status == BookingStatus.PendingApproval) ? "#d6a950" : "#1f1a13"
                }).ToList();

            // Pre-compute values for simplified view
            var activeFilterKeys = new[] { "status", "paymentStatus", "source", "sportId", "facilityId", "courtId", "customerId", "dateFrom", "dateTo" };
            var queryValues = new Dictionary<string, string>();
            var requestQuery = HttpContext.Request.Query;
            foreach (var key in activeFilterKeys)
            {
                var val = requestQuery[key].ToString();
                if (!string.IsNullOrWhiteSpace(val)) queryValues[key] = val;
            }
            if (!string.IsNullOrWhiteSpace(search)) queryValues["search"] = search;
            if (!string.IsNullOrWhiteSpace(sortBy) && sortBy != "date_desc") queryValues["sortBy"] = sortBy;
            if (pageSize != 10) queryValues["pageSize"] = pageSize.ToString();
            queryValues["view"] = view;

            var activeFilterCount = queryValues.Count;

            var startRecord = model.TotalRecords == 0 ? 0 : ((model.CurrentPage - 1) * model.PageSize) + 1;
            var endRecord = Math.Min(model.CurrentPage * model.PageSize, model.TotalRecords);

            var pageStart = Math.Max(1, model.CurrentPage - 2);
            var pageEnd = Math.Min(model.TotalPages, pageStart + 4);
            pageStart = Math.Max(1, pageEnd - 4);

            var paginationLinks = new List<PageLink>();
            paginationLinks.Add(new PageLink
            {
                PageNumber = model.CurrentPage - 1,
                Url = model.CurrentPage <= 1 ? "#" : BuildPageUrl(requestQuery, model.CurrentPage - 1),
                IsActive = false,
                IsDisabled = model.CurrentPage <= 1,
                Label = "Previous"
            });
            for (int p = pageStart; p <= pageEnd; p++)
            {
                paginationLinks.Add(new PageLink
                {
                    PageNumber = p,
                    Url = BuildPageUrl(requestQuery, p),
                    IsActive = p == model.CurrentPage,
                    IsDisabled = false,
                    Label = p.ToString()
                });
            }
            paginationLinks.Add(new PageLink
            {
                PageNumber = model.CurrentPage + 1,
                Url = model.CurrentPage >= model.TotalPages ? "#" : BuildPageUrl(requestQuery, model.CurrentPage + 1),
                IsActive = false,
                IsDisabled = model.CurrentPage >= model.TotalPages,
                Label = "Next"
            });

            var statusOptions = Enum.GetValues<BookingStatus>()
                .Select(s => new SelectListItem(s.GetDisplayName(), s.ToString()))
                .ToList();

            var paymentStatusOptions = Enum.GetValues<PaymentStatus>()
                .Select(s => new SelectListItem(s.GetDisplayName(), s.ToString()))
                .ToList();

            var sourceOptions = Enum.GetValues<BookingSource>()
                .Select(s => new SelectListItem(s.GetDisplayName(), s.ToString()))
                .ToList();

            var sortOptions = new List<SelectListItem>
            {
                new("Date Desc", "date_desc"),
                new("Date Asc", "date_asc"),
                new("Newest", "newest"),
                new("Customer", "customer"),
                new("Court", "court"),
                new("Highest Amount", "amount"),
                new("Status", "status")
            };

            var pageSizeOptions = new[] { 10, 25, 50 }
                .Select(s => new SelectListItem(s.ToString(), s.ToString()))
                .ToList();

            model.ActiveFilterCount = activeFilterCount;
            model.StartRecord = startRecord;
            model.EndRecord = endRecord;
            model.PageStart = pageStart;
            model.PageEnd = pageEnd;
            model.StatusOptions = statusOptions;
            model.PaymentStatusOptions = paymentStatusOptions;
            model.SourceOptions = sourceOptions;
            model.SortOptions = sortOptions;
            model.PageSizeOptions = pageSizeOptions;
            model.PaginationLinks = paginationLinks;
            model.CurrentQueryValues = queryValues;

            return View(model);
        }

        [HttpGet]
        public async Task<IActionResult> Create()
        {
            if (!CanManage()) return RedirectToAction("NotFound", "Error");
            var settings = await GetSettingsAsync();
            var model = new BookingFormViewModel
            {
                BookingDate = DateOnly.FromDateTime(DateTime.UtcNow),
                Source = BookingSource.Reception,
                Status = BookingStatus.Pending,
                DurationMinutes = settings.SlotDurationMinutes > 0 ? settings.SlotDurationMinutes : 60,
                StartTime = settings.OpeningTime,
                EndTime = settings.OpeningTime.AddMinutes(settings.SlotDurationMinutes > 0 ? settings.SlotDurationMinutes : 60),
                PlayerCount = 1,
                CurrencySymbol = settings.CurrencySymbol ?? "Rs",
                ArenaName = settings.ArenaName,
                CanManualDiscount = CanManualDiscount()
            };
            return View("Form", await AddFormOptionsAsync(model));
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(BookingFormViewModel model)
        {
            if (!CanManage()) return RedirectToAction("NotFound", "Error");
            NormalizeForm(model);
            await ValidateBookingAsync(model);
            if (!ModelState.IsValid) return View("Form", await AddFormOptionsAsync(model));

            var booking = new Booking { BookingNumber = await _bookingNumberService.GenerateNextNumberAsync(), CreatedAt = DateTime.UtcNow, CreatedByUserId = CurrentUserId() };
            await using var dbTransaction = await _dbContext.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
            try
            {
                var recheck = await _validationService.CheckAvailabilityAsync(model.CourtId, model.BookingDate, model.StartTime, model.EndTime);
                if (!recheck.IsAvailable)
                {
                    await dbTransaction.RollbackAsync();
                    ModelState.AddModelError(string.Empty, recheck.Message);
                    return View("Form", await AddFormOptionsAsync(model));
                }
                await MapBookingAsync(model, booking);
                _dbContext.Bookings.Add(booking);
                await _dbContext.SaveChangesAsync();
                await ApplyMembershipHoursIfNeededAsync(booking);
                await dbTransaction.CommitAsync();
            }
            catch (Microsoft.EntityFrameworkCore.DbUpdateException)
            {
                await dbTransaction.RollbackAsync();
                ModelState.AddModelError(string.Empty, "A conflicting booking was created at the same time. Please select another slot.");
                return View("Form", await AddFormOptionsAsync(model));
            }
            catch (Exception)
            {
                await dbTransaction.RollbackAsync();
                ModelState.AddModelError(string.Empty, "The booking could not be created. Please try again.");
                return View("Form", await AddFormOptionsAsync(model));
            }
            TempData.SetToast("success", "Success", "Booking created successfully.");
            return RedirectToAction(nameof(Details), new { id = booking.Id });
        }

        [HttpGet]
        public async Task<IActionResult> Edit(int id)
        {
            if (!CanManage()) return RedirectToAction("NotFound", "Error");
            var booking = await _dbContext.Bookings.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
            if (booking == null) return NotFound();
            if (!CanEdit(booking.Status))
            {
                TempData.SetToast("warning", "Attention Required", "Completed, cancelled, and no-show bookings cannot be edited.");
                return RedirectToAction(nameof(Details), new { id });
            }
            return View("Form", await AddFormOptionsAsync(MapForm(booking)));
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, BookingFormViewModel model)
        {
            if (!CanManage()) return RedirectToAction("NotFound", "Error");
            if (model.Id != id) return NotFound();
            NormalizeForm(model);
            await ValidateBookingAsync(model, id);
            var booking = await _dbContext.Bookings.FirstOrDefaultAsync(x => x.Id == id);
            if (booking == null) return NotFound();
            if (!CanEdit(booking.Status)) return RedirectToAction(nameof(Details), new { id });
            if (!ModelState.IsValid) return View("Form", await AddFormOptionsAsync(model));

            await using var dbTransaction = await _dbContext.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
            try
            {
                var recheck = await _validationService.CheckAvailabilityAsync(model.CourtId, model.BookingDate, model.StartTime, model.EndTime, id);
                if (!recheck.IsAvailable)
                {
                    await dbTransaction.RollbackAsync();
                    ModelState.AddModelError(string.Empty, recheck.Message);
                    return View("Form", await AddFormOptionsAsync(model));
                }
                var previousApplied = booking.MembershipHoursApplied;
                if (previousApplied) await RestoreMembershipHoursAsync(booking);
                await MapBookingAsync(model, booking);
                booking.UpdatedAt = DateTime.UtcNow;
                booking.UpdatedByUserId = CurrentUserId();
                await _dbContext.SaveChangesAsync();
                await ApplyMembershipHoursIfNeededAsync(booking);
                await dbTransaction.CommitAsync();
            }
            catch (Microsoft.EntityFrameworkCore.DbUpdateException)
            {
                await dbTransaction.RollbackAsync();
                ModelState.AddModelError(string.Empty, "A conflicting booking was created at the same time. Please select another slot.");
                return View("Form", await AddFormOptionsAsync(model));
            }
            catch (Exception)
            {
                await dbTransaction.RollbackAsync();
                ModelState.AddModelError(string.Empty, "The booking could not be updated. Please try again.");
                return View("Form", await AddFormOptionsAsync(model));
            }
            TempData.SetToast("success", "Success", "Booking updated successfully.");
            return RedirectToAction(nameof(Details), new { id });
        }

        [HttpGet]
        public async Task<IActionResult> Details(int id)
        {
            var booking = await _dbContext.Bookings.AsNoTracking()
                .Include(x => x.Customer).Include(x => x.Sport).Include(x => x.Facility).Include(x => x.Court)
                .Include(x => x.CustomerMembership).ThenInclude(x => x!.MembershipPlan)
                .FirstOrDefaultAsync(x => x.Id == id);
            if (booking == null) return NotFound();
            var model = await MapDetailsAsync(booking);
            return View(model);
        }

        [HttpGet]
        public async Task<IActionResult> CustomerRequests()
        {
            if (!CanManage()) return RedirectToAction("NotFound", "Error");
            var requests = await _dbContext.CustomerBookingRequests.AsNoTracking()
                .Include(x => x.Customer)
                .Include(x => x.Booking).ThenInclude(x => x.Court)
                .OrderByDescending(x => x.RequestedAt)
                .Take(100)
                .ToListAsync();
            return View(requests);
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> ApproveCustomerRequest(int id, string? response)
        {
            if (!CanManage()) return RedirectToAction("NotFound", "Error");
            var request = await _dbContext.CustomerBookingRequests.Include(x => x.Booking).FirstOrDefaultAsync(x => x.Id == id);
            if (request == null) return NotFound();
            request.Status = CustomerBookingRequestStatus.Approved;
            request.AdminResponse = Clean(response) ?? "Approved.";
            request.ReviewedAt = DateTime.UtcNow;
            request.ReviewedByUserId = CurrentUserId();
            if (request.RequestType == CustomerBookingRequestType.Cancellation)
            {
                request.Booking.Status = BookingStatus.Cancelled;
                request.Booking.IsActive = false;
                request.Booking.CustomerCancelledAt = DateTime.UtcNow;
                request.Booking.CancelledAt = DateTime.UtcNow;
                await DisableRemindersAsync(request.Booking.Id);
            }
            else if (request.RequestType == CustomerBookingRequestType.Reschedule && request.RequestedDate.HasValue && request.RequestedStartTime.HasValue && request.RequestedEndTime.HasValue)
            {
                var availability = await _validationService.CheckAvailabilityAsync(
                    request.Booking.CourtId, request.RequestedDate.Value,
                    request.RequestedStartTime.Value, request.RequestedEndTime.Value,
                    excludeBookingId: request.BookingId);
                if (!availability.IsAvailable)
                {
                    request.Status = CustomerBookingRequestStatus.Rejected;
                    request.AdminResponse = $"Reschedule rejected: {availability.Message}";
                    request.ReviewedAt = DateTime.UtcNow;
                    _dbContext.CustomerNotifications.Add(new CustomerNotification
                    {
                        CustomerId = request.CustomerId, BookingId = request.BookingId,
                        Type = CustomerNotificationType.RescheduleRejected,
                        Title = "Reschedule Rejected",
                        Message = availability.Message,
                        ActionUrl = $"/MyBookings/Details/{request.BookingId}",
                        CreatedAt = DateTime.UtcNow
                    });
                    await _dbContext.SaveChangesAsync();
                    TempData.SetToast("warning", "Reschedule Rejected", availability.Message);
                    return RedirectToAction(nameof(CustomerRequests));
                }

                request.Booking.BookingDate = request.RequestedDate.Value;
                request.Booking.StartTime = request.RequestedStartTime.Value;
                request.Booking.EndTime = request.RequestedEndTime.Value;
                request.Booking.CustomerRescheduleRequestedAt = DateTime.UtcNow;
                request.Booking.RescheduleReason = request.Reason;
                await DisableRemindersAsync(request.Booking.Id);
            }
            _dbContext.CustomerNotifications.Add(new CustomerNotification { CustomerId = request.CustomerId, BookingId = request.BookingId, Type = request.RequestType == CustomerBookingRequestType.Cancellation ? CustomerNotificationType.BookingCancelled : CustomerNotificationType.RescheduleApproved, Title = "Request Approved", Message = request.AdminResponse, ActionUrl = $"/MyBookings/Details/{request.BookingId}", CreatedAt = DateTime.UtcNow });
            await _dbContext.SaveChangesAsync();
            TempData.SetToast("success", "Request Approved", "Customer request approved.");
            return RedirectToAction(nameof(CustomerRequests));
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> RejectCustomerRequest(int id, string? response)
        {
            if (!CanManage()) return RedirectToAction("NotFound", "Error");
            var request = await _dbContext.CustomerBookingRequests.FirstOrDefaultAsync(x => x.Id == id);
            if (request == null) return NotFound();
            request.Status = CustomerBookingRequestStatus.Rejected;
            request.AdminResponse = Clean(response) ?? "Rejected.";
            request.ReviewedAt = DateTime.UtcNow;
            request.ReviewedByUserId = CurrentUserId();
            _dbContext.CustomerNotifications.Add(new CustomerNotification { CustomerId = request.CustomerId, BookingId = request.BookingId, Type = CustomerNotificationType.RescheduleRejected, Title = "Request Rejected", Message = request.AdminResponse, ActionUrl = $"/MyBookings/Details/{request.BookingId}", CreatedAt = DateTime.UtcNow });
            await _dbContext.SaveChangesAsync();
            TempData.SetToast("warning", "Request Rejected", "Customer request rejected.");
            return RedirectToAction(nameof(CustomerRequests));
        }

        [HttpGet]
        public async Task<IActionResult> Approvals(string? search = null, int? sportId = null, int? facilityId = null, int? courtId = null, int? customerId = null, DateOnly? dateFrom = null, DateOnly? dateTo = null, string sortBy = "date_asc", int page = 1, int pageSize = 25)
        {
            if (!CanManage()) return RedirectToAction("NotFound", "Error");

            page = Math.Max(1, page);
            pageSize = new[] { 10, 25, 50 }.Contains(pageSize) ? pageSize : 25;

            var settings = await GetSettingsAsync();
            var query = _dbContext.Bookings.AsNoTracking()
                .Include(x => x.Customer)
                .Include(x => x.Court).ThenInclude(x => x.Facility)
                .Include(x => x.Sport)
                .Include(x => x.PaymentTransaction)
                .Where(x => x.Status == BookingStatus.PendingApproval && x.IsActive);

            if (!string.IsNullOrWhiteSpace(search))
            {
                var term = search.Trim().ToLower();
                query = query.Where(x => x.BookingNumber.ToLower().Contains(term)
                    || x.Customer.FirstName.ToLower().Contains(term)
                    || x.Customer.LastName.ToLower().Contains(term)
                    || x.Customer.CustomerCode.ToLower().Contains(term)
                    || x.Customer.PrimaryPhone.Contains(term)
                    || x.Court.Name.ToLower().Contains(term)
                    || x.Sport.Name.ToLower().Contains(term));
            }

            if (sportId.HasValue) query = query.Where(x => x.SportId == sportId.Value);
            if (facilityId.HasValue) query = query.Where(x => x.FacilityId == facilityId.Value);
            if (courtId.HasValue) query = query.Where(x => x.CourtId == courtId.Value);
            if (customerId.HasValue) query = query.Where(x => x.CustomerId == customerId.Value);
            if (dateFrom.HasValue) query = query.Where(x => x.BookingDate >= dateFrom.Value);
            if (dateTo.HasValue) query = query.Where(x => x.BookingDate <= dateTo.Value);

            query = sortBy switch
            {
                "date_desc" => query.OrderByDescending(x => x.BookingDate).ThenByDescending(x => x.StartTime),
                "customer" => query.OrderBy(x => x.Customer.FirstName).ThenBy(x => x.Customer.LastName),
                "court" => query.OrderBy(x => x.Court.Name),
                "amount" => query.OrderByDescending(x => x.TotalAmount),
                _ => query.OrderBy(x => x.BookingDate).ThenBy(x => x.StartTime)
            };

            var total = await query.CountAsync();
            var totalPages = Math.Max(1, (int)Math.Ceiling(total / (double)pageSize));
            page = Math.Min(page, totalPages);

            var items = await query.Skip((page - 1) * pageSize).Take(pageSize).Select(x => new BookingApprovalListItemViewModel
            {
                Id = x.Id,
                BookingNumber = x.BookingNumber,
                CustomerName = x.Customer.FirstName + " " + x.Customer.LastName,
                CustomerCode = x.Customer.CustomerCode,
                CustomerPhone = x.Customer.PrimaryPhone,
                CustomerEmail = x.Customer.Email,
                SportName = x.Sport.Name,
                FacilityName = x.Court.Facility.Name,
                CourtName = x.Court.Name,
                BookingDate = x.BookingDate,
                StartTime = x.StartTime,
                EndTime = x.EndTime,
                DurationMinutes = x.DurationMinutes,
                PlayerCount = x.PlayerCount,
                TotalAmount = x.TotalAmount,
                PaidAmount = x.PaidAmount,
                PaymentStatus = x.PaymentStatus,
                PaymentTransactionId = x.PaymentTransactionId,
                PaymentReference = x.PaymentReference,
                GatewayReference = x.PaymentTransaction != null ? x.PaymentTransaction.GatewayReference : null,
                GatewayTransactionId = x.PaymentTransaction != null ? x.PaymentTransaction.GatewayTransactionId : null,
                CreatedAt = x.CreatedAt
            }).ToListAsync();

            // Pre-compute values for simplified view
            var activeFilterKeys = new[] { "status", "method", "reason", "sportId", "facilityId", "courtId", "customerId", "dateFrom", "dateTo" };
            var queryValues = new Dictionary<string, string>();
            var requestQuery = HttpContext.Request.Query;
            foreach (var key in activeFilterKeys)
            {
                var val = requestQuery[key].ToString();
                if (!string.IsNullOrWhiteSpace(val)) queryValues[key] = val;
            }
            if (!string.IsNullOrWhiteSpace(search)) queryValues["search"] = search;
            if (!string.IsNullOrWhiteSpace(sortBy) && sortBy != "date_asc") queryValues["sortBy"] = sortBy;
            if (pageSize != 25) queryValues["pageSize"] = pageSize.ToString();

            var activeFilterCount = queryValues.Count;

            var startRecord = total == 0 ? 0 : ((page - 1) * pageSize) + 1;
            var endRecord = Math.Min(page * pageSize, total);

            var pageStart = Math.Max(1, page - 2);
            var pageEnd = Math.Min(totalPages, pageStart + 4);
            pageStart = Math.Max(1, pageEnd - 4);

            var paginationLinks = new List<PageLink>();
            // Previous
            paginationLinks.Add(new PageLink
            {
                PageNumber = page - 1,
                Url = page <= 1 ? "#" : BuildPageUrl(requestQuery, page - 1),
                IsActive = false,
                IsDisabled = page <= 1,
                Label = "Previous"
            });
            // Page numbers
            for (int p = pageStart; p <= pageEnd; p++)
            {
                paginationLinks.Add(new PageLink
                {
                    PageNumber = p,
                    Url = BuildPageUrl(requestQuery, p),
                    IsActive = p == page,
                    IsDisabled = false,
                    Label = p.ToString()
                });
            }
            // Next
            paginationLinks.Add(new PageLink
            {
                PageNumber = page + 1,
                Url = page >= totalPages ? "#" : BuildPageUrl(requestQuery, page + 1),
                IsActive = false,
                IsDisabled = page >= totalPages,
                Label = "Next"
            });

            var statusOptions = Enum.GetValues<BookingStatus>()
                .Select(s => new SelectListItem(s.GetDisplayName(), s.ToString()))
                .ToList();

            var sortOptions = new List<SelectListItem>
            {
                new("Date Asc", "date_asc"),
                new("Date Desc", "date_desc"),
                new("Customer", "customer"),
                new("Court", "court"),
                new("Highest Amount", "amount")
            };

            var pageSizeOptions = new[] { 10, 25, 50 }
                .Select(s => new SelectListItem(s.ToString(), s.ToString()))
                .ToList();

            var model = new BookingApprovalsViewModel
            {
                Items = items,
                Search = search,
                SportId = sportId,
                FacilityId = facilityId,
                CourtId = courtId,
                CustomerId = customerId,
                DateFrom = dateFrom,
                DateTo = dateTo,
                SortBy = sortBy,
                CurrentPage = page,
                PageSize = pageSize,
                TotalRecords = total,
                TotalPages = totalPages,
                CurrencySymbol = settings.CurrencySymbol ?? "Rs",
                CanManage = CanManage(),
                Sports = await _dbContext.Sports.AsNoTracking().Where(x => x.IsActive).OrderBy(x => x.DisplayOrder).Select(x => new SelectListItem(x.Name, x.Id.ToString())).ToListAsync(),
                Facilities = await _dbContext.Facilities.AsNoTracking().Where(x => x.IsActive).OrderBy(x => x.DisplayOrder).Select(x => new SelectListItem(x.Name, x.Id.ToString())).ToListAsync(),
                Courts = await _dbContext.Courts.AsNoTracking().Where(x => x.IsActive).OrderBy(x => x.DisplayOrder).Select(x => new SelectListItem(x.Name, x.Id.ToString())).ToListAsync(),
                Customers = await _dbContext.Customers.AsNoTracking().Where(x => x.IsActive).OrderBy(x => x.FirstName).Select(x => new SelectListItem(x.FirstName + " " + x.LastName + " - " + x.CustomerCode, x.Id.ToString())).ToListAsync(),
                ActiveFilterCount = activeFilterCount,
                StartRecord = startRecord,
                EndRecord = endRecord,
                PageStart = pageStart,
                PageEnd = pageEnd,
                StatusOptions = statusOptions,
                SortOptions = sortOptions,
                PageSizeOptions = pageSizeOptions,
                PaginationLinks = paginationLinks,
                CurrentQueryValues = queryValues
            };

            return View(model);
        }

        private string BuildPageUrl(Microsoft.AspNetCore.Http.IQueryCollection query, int page)
        {
            var pairs = new List<string>();
            foreach (var item in query)
            {
                if (item.Key.Equals("page", StringComparison.OrdinalIgnoreCase)) continue;
                foreach (var value in item.Value)
                {
                    if (!string.IsNullOrWhiteSpace(value)) pairs.Add($"{System.Net.WebUtility.UrlEncode(item.Key)}={System.Net.WebUtility.UrlEncode(value)}");
                }
            }
            pairs.Add($"page={page}");
            return $"{HttpContext.Request.Path}?{string.Join("&", pairs)}";
        }

        [HttpGet]
        public async Task<IActionResult> Refunds(string? search = null, RefundStatus? status = null, RefundMethod? method = null, RefundReason? reason = null, DateOnly? dateFrom = null, DateOnly? dateTo = null, string sortBy = "newest", int page = 1, int pageSize = 25)
        {
            if (!CanManage()) return RedirectToAction("NotFound", "Error");

            page = Math.Max(1, page);
            pageSize = new[] { 10, 25, 50 }.Contains(pageSize) ? pageSize : 25;

            var settings = await GetSettingsAsync();
            var query = _dbContext.PaymentRefunds.AsNoTracking()
                .Include(x => x.Booking)
                .Include(x => x.PaymentTransaction)
                .Include(x => x.InitiatedByUser)
                .Include(x => x.ProcessedByUser)
                .Where(x => x.Booking != null);

            if (!string.IsNullOrWhiteSpace(search))
            {
                var term = search.Trim().ToLower();
                query = query.Where(x => x.RefundNumber.ToLower().Contains(term)
                    || x.Booking.BookingNumber.ToLower().Contains(term)
                    || x.Customer.FirstName.ToLower().Contains(term)
                    || x.Customer.LastName.ToLower().Contains(term)
                    || x.Customer.CustomerCode.ToLower().Contains(term)
                    || x.PaymentTransaction.TransactionNumber.ToLower().Contains(term));
            }

            if (status.HasValue) query = query.Where(x => x.Status == status.Value);
            if (method.HasValue) query = query.Where(x => x.Method == method.Value);
            if (reason.HasValue) query = query.Where(x => x.Reason == reason.Value);
            if (dateFrom.HasValue) query = query.Where(x => x.InitiatedAt >= dateFrom.Value.ToDateTime(TimeOnly.MinValue));
            if (dateTo.HasValue) query = query.Where(x => x.InitiatedAt <= dateTo.Value.ToDateTime(TimeOnly.MaxValue));

            query = sortBy switch
            {
                "oldest" => query.OrderBy(x => x.InitiatedAt),
                "amount" => query.OrderByDescending(x => x.Amount),
                "status" => query.OrderBy(x => x.Status),
                _ => query.OrderByDescending(x => x.InitiatedAt)
            };

            var total = await query.CountAsync();
            var totalPages = Math.Max(1, (int)Math.Ceiling(total / (double)pageSize));
            page = Math.Min(page, totalPages);

            var items = await query.Skip((page - 1) * pageSize).Take(pageSize).Select(x => new RefundListItemViewModel
            {
                Id = x.Id,
                RefundNumber = x.RefundNumber,
                BookingNumber = x.Booking.BookingNumber,
                CustomerName = x.Customer.FirstName + " " + x.Customer.LastName,
                CustomerCode = x.Customer.CustomerCode,
                PaymentTransactionNumber = x.PaymentTransaction.TransactionNumber,
                Reason = x.Reason,
                ReasonDetails = x.ReasonDetails ?? string.Empty,
                Method = x.Method,
                Status = x.Status,
                Amount = x.Amount,
                CurrencySymbol = settings.CurrencySymbol ?? "Rs",
                GatewayRefundId = x.GatewayRefundId ?? string.Empty,
                GatewayRefundReference = x.GatewayRefundReference ?? string.Empty,
                FailureReason = x.FailureReason ?? string.Empty,
                InitiatedByName = x.InitiatedByUser.FirstName + " " + x.InitiatedByUser.LastName,
                ProcessedByName = x.ProcessedByUser != null ? x.ProcessedByUser.FirstName + " " + x.ProcessedByUser.LastName : string.Empty,
                InitiatedAt = x.InitiatedAt,
                ProcessedAt = x.ProcessedAt,
                CompletedAt = x.CompletedAt,
                FailedAt = x.FailedAt
            }).ToListAsync();

            // Pre-compute values for simplified view
            var activeFilterKeys = new[] { "status", "method", "reason", "dateFrom", "dateTo" };
            var queryValues = new Dictionary<string, string>();
            var requestQuery = HttpContext.Request.Query;
            foreach (var key in activeFilterKeys)
            {
                var val = requestQuery[key].ToString();
                if (!string.IsNullOrWhiteSpace(val)) queryValues[key] = val;
            }
            if (!string.IsNullOrWhiteSpace(search)) queryValues["search"] = search;
            if (!string.IsNullOrWhiteSpace(sortBy) && sortBy != "newest") queryValues["sortBy"] = sortBy;
            if (pageSize != 25) queryValues["pageSize"] = pageSize.ToString();

            var activeFilterCount = queryValues.Count;

            var startRecord = total == 0 ? 0 : ((page - 1) * pageSize) + 1;
            var endRecord = Math.Min(page * pageSize, total);

            var pageStart = Math.Max(1, page - 2);
            var pageEnd = Math.Min(totalPages, pageStart + 4);
            pageStart = Math.Max(1, pageEnd - 4);

            var paginationLinks = new List<PageLink>();
            paginationLinks.Add(new PageLink
            {
                PageNumber = page - 1,
                Url = page <= 1 ? "#" : BuildPageUrl(requestQuery, page - 1),
                IsActive = false,
                IsDisabled = page <= 1,
                Label = "Previous"
            });
            for (int p = pageStart; p <= pageEnd; p++)
            {
                paginationLinks.Add(new PageLink
                {
                    PageNumber = p,
                    Url = BuildPageUrl(requestQuery, p),
                    IsActive = p == page,
                    IsDisabled = false,
                    Label = p.ToString()
                });
            }
            paginationLinks.Add(new PageLink
            {
                PageNumber = page + 1,
                Url = page >= totalPages ? "#" : BuildPageUrl(requestQuery, page + 1),
                IsActive = false,
                IsDisabled = page >= totalPages,
                Label = "Next"
            });

            var statusOptions = Enum.GetValues<RefundStatus>()
                .Select(s => new SelectListItem(s.GetDisplayName(), ((int)s).ToString()))
                .ToList();

            var methodOptions = Enum.GetValues<RefundMethod>()
                .Select(s => new SelectListItem(s.GetDisplayName(), ((int)s).ToString()))
                .ToList();

            var reasonOptions = Enum.GetValues<RefundReason>()
                .Select(s => new SelectListItem(s.GetDisplayName(), ((int)s).ToString()))
                .ToList();

            var sortOptions = new List<SelectListItem>
            {
                new("Newest", "newest"),
                new("Oldest", "oldest"),
                new("Amount", "amount"),
                new("Status", "status")
            };

            var pageSizeOptions = new[] { 10, 25, 50 }
                .Select(s => new SelectListItem(s.ToString(), s.ToString()))
                .ToList();

            var model = new RefundsViewModel
            {
                Items = items,
                Search = search,
                Status = status,
                Method = method,
                Reason = reason,
                DateFrom = dateFrom,
                DateTo = dateTo,
                SortBy = sortBy,
                CurrentPage = page,
                PageSize = pageSize,
                TotalRecords = total,
                TotalPages = totalPages,
                CurrencySymbol = settings.CurrencySymbol ?? "Rs",
                CanManage = CanManage(),
                StatusOptions = statusOptions,
                MethodOptions = methodOptions,
                ReasonOptions = reasonOptions,
                ActiveFilterCount = activeFilterCount,
                StartRecord = startRecord,
                EndRecord = endRecord,
                PageStart = pageStart,
                PageEnd = pageEnd,
                SortOptions = sortOptions,
                PageSizeOptions = pageSizeOptions,
                PaginationLinks = paginationLinks,
                CurrentQueryValues = queryValues
            };

            return View(model);
        }

        [HttpGet]
        public async Task<IActionResult> RefundDetails(int id)
        {
            if (!CanManage()) return RedirectToAction("NotFound", "Error");

            var refund = await _dbContext.PaymentRefunds
                .Include(x => x.Booking)
                    .ThenInclude(x => x.Court)
                        .ThenInclude(x => x.Facility)
                .Include(x => x.Booking)
                    .ThenInclude(x => x.Court)
                        .ThenInclude(x => x.Sport)
                .Include(x => x.Booking)
                    .ThenInclude(x => x.Sport)
                .Include(x => x.Booking)
                    .ThenInclude(x => x.Facility)
                .Include(x => x.Booking)
                    .ThenInclude(x => x.Customer)
                .Include(x => x.PaymentTransaction)
                .Include(x => x.Payment)
                    .ThenInclude(x => x.Invoice)
                .Include(x => x.InitiatedByUser)
                .Include(x => x.ProcessedByUser)
                .FirstOrDefaultAsync(x => x.Id == id);

            if (refund == null)
            {
                TempData.SetToast("error", "Not Found", "Refund not found.");
                return RedirectToAction(nameof(Refunds));
            }

            var approvalHistory = await _dbContext.DocumentApprovals
                .AsNoTracking()
                .Include(x => x.ActionByUser)
                .Where(x => x.DocumentType == DocumentType.Booking && x.DocumentId == refund.BookingId)
                .OrderBy(x => x.ActionAt)
                .ToListAsync();

            var activityHistory = await _dbContext.DocumentActivities
                .AsNoTracking()
                .Include(x => x.PerformedByUser)
                .Where(x => x.DocumentType == DocumentType.Booking && x.DocumentId == refund.BookingId)
                .OrderBy(x => x.PerformedAt)
                .ToListAsync();

            var settings = await GetSettingsAsync();
            var model = new RefundDetailsViewModel
            {
                Refund = refund,
                CurrencySymbol = settings.CurrencySymbol ?? "Rs",
                CanManage = CanManage(),
                Booking = refund.Booking,
                PaymentTransaction = refund.PaymentTransaction,
                Payment = refund.Payment,
                ApprovalHistory = approvalHistory,
                ActivityHistory = activityHistory
            };

            return View(model);
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> ProcessManualRefund(int id, string? gatewayRefundReference)
        {
            if (!CanManage()) return RedirectToAction("NotFound", "Error");

            try
            {
                var refund = await _refundService.CompleteManualRefundAsync(id, CurrentUserId(), gatewayRefundReference);
                TempData.SetToast("success", "Refund Completed", $"Manual refund {refund.RefundNumber} has been completed.");
            }
            catch (Exception ex)
            {
                TempData.SetToast("error", "Refund Failed", ex.Message);
            }

            return RedirectToAction(nameof(RefundDetails), new { id });
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> RetryRefund(int id)
        {
            if (!CanManage()) return RedirectToAction("NotFound", "Error");

            try
            {
                var refund = await _refundService.ProcessRefundAsync(id, CurrentUserId());
                if (refund.Status == RefundStatus.Completed)
                {
                    TempData.SetToast("success", "Refund Retried", $"Refund {refund.RefundNumber} completed successfully.");
                }
                else
                {
                    TempData.SetToast("warning", "Refund Failed", $"Refund {refund.RefundNumber} failed: {refund.FailureReason}");
                }
            }
            catch (Exception ex)
            {
                TempData.SetToast("error", "Refund Failed", ex.Message);
            }

            return RedirectToAction(nameof(RefundDetails), new { id });
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> ApproveBooking(int id, string? comments)
        {
            if (!CanManage()) return RedirectToAction("NotFound", "Error");

            var booking = await _dbContext.Bookings
                .Include(x => x.Customer)
                .Include(x => x.Court).ThenInclude(x => x.Facility)
                .Include(x => x.Sport)
                .Include(x => x.PaymentTransaction)
                .Include(x => x.CustomerMembership)
                .FirstOrDefaultAsync(x => x.Id == id && x.IsActive);

            if (booking == null)
            {
                TempData.SetToast("error", "Not Found", "Booking not found.");
                return RedirectToAction(nameof(Approvals));
            }

            if (booking.Status != BookingStatus.PendingApproval)
            {
                TempData.SetToast("warning", "Invalid State", "Only bookings pending approval can be approved.");
                return RedirectToAction(nameof(Approvals));
            }

            // Verify payment transaction status before proceeding
            if (booking.PaymentTransaction == null)
            {
                _logger.LogWarning("ApproveBooking: Booking {BookingId} has no PaymentTransaction", id);
                TempData.SetToast("error", "Payment Incomplete", "No payment transaction is linked to this booking.");
                return RedirectToAction(nameof(Approvals));
            }

            if (booking.PaymentTransaction.Status != PaymentTransactionStatus.Succeeded)
            {
                _logger.LogWarning("ApproveBooking: Booking {BookingId} PaymentTransaction status is {Status}, expected Succeeded", 
                    id, booking.PaymentTransaction.Status);
                TempData.SetToast("error", "Payment Incomplete", $"Payment transaction status is '{booking.PaymentTransaction.Status.GetDisplayName()}'. Only successful payments can be approved.");
                return RedirectToAction(nameof(Approvals));
            }

            // Verify payment status on booking (allow small decimal variance)
            if (booking.PaymentStatus != PaymentStatus.Paid)
            {
                _logger.LogWarning("ApproveBooking: Booking {BookingId} PaymentStatus is {Status}, expected Paid", 
                    id, booking.PaymentStatus);
                TempData.SetToast("error", "Payment Incomplete", $"Booking payment status is '{booking.PaymentStatus.GetDisplayName()}'. Only paid bookings can be approved.");
                return RedirectToAction(nameof(Approvals));
            }

            if (booking.PaidAmount + 0.005m < booking.TotalAmount) // Allow 0.5 paisa rounding difference
            {
                var currencySymbol = booking.PaymentTransaction?.CurrencySymbol ?? (await GetSettingsAsync()).CurrencySymbol ?? "Rs";
                _logger.LogWarning("ApproveBooking: Booking {BookingId} PaidAmount {PaidAmount} < TotalAmount {TotalAmount}", 
                    id, booking.PaidAmount, booking.TotalAmount);
                TempData.SetToast("error", "Payment Incomplete", $"Paid amount ({currencySymbol} {booking.PaidAmount:N2}) is less than total amount ({currencySymbol} {booking.TotalAmount:N2}).");
                return RedirectToAction(nameof(Approvals));
            }

            // Retry loop for unique constraint on SalesOrderNumber
            const int maxRetries = 2;
            for (int attempt = 0; attempt <= maxRetries; attempt++)
            {
                await using var dbTransaction = await _dbContext.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
                try
                {
                    // Re-check court availability within the transaction
                    var availability = await _validationService.CheckAvailabilityAsync(booking.CourtId, booking.BookingDate, booking.StartTime, booking.EndTime, booking.Id);
                    if (!availability.IsAvailable)
                    {
                        await dbTransaction.RollbackAsync();
                        TempData.SetToast("error", "Slot Unavailable", $"This time slot is no longer available: {availability.Message}");
                        return RedirectToAction(nameof(Approvals));
                    }

                    var previousStatus = booking.Status.ToString();
                    var now = DateTime.UtcNow;
                    var settings = await GetSettingsAsync();
                    var currency = settings.Currency;

                    booking.Status = BookingStatus.Confirmed;
                    booking.ConfirmedAt = now;
                    booking.UpdatedAt = now;
                    booking.UpdatedByUserId = CurrentUserId();

                    // Apply membership hours if applicable
                    if (booking.CustomerMembershipId.HasValue && !booking.MembershipHoursApplied)
                    {
                        var membership = await _dbContext.CustomerMemberships.FirstOrDefaultAsync(x => x.Id == booking.CustomerMembershipId.Value);
                        if (membership != null)
                        {
                            membership.UsedBookingHours += Math.Max(1, (int)Math.Ceiling(booking.DurationMinutes / 60m));
                            membership.UpdatedAt = now;
                            booking.MembershipHoursApplied = true;
                        }
                    }

                    // Create linked Sales Order, Invoice, and Payment records within the same transaction
                    var salesRecords = await _invoiceService.CreateSalesRecordsForApprovedBookingAsync(
                        booking.Id,
                        CurrentUserId(),
                        comments);

                    // Record booking approval audit trail (only on successful attempt)
                    _dbContext.DocumentApprovals.Add(new DocumentApproval
                    {
                        DocumentType = DocumentType.Booking,
                        DocumentId = booking.Id,
                        DocumentNumber = booking.BookingNumber,
                        RevisionNumber = 1,
                        Action = ApprovalAction.BookingApproved,
                        PreviousStatus = previousStatus,
                        NewStatus = booking.Status.ToString(),
                        SubmittedByUserId = CurrentUserId(),
                        ActionByUserId = CurrentUserId(),
                        ActionAt = now,
                        Comments = Clean(comments),
                        FinancialTotal = booking.TotalAmount,
                        Currency = currency,
                        IsFinal = true,
                        IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString(),
                        CreatedAt = now
                    });

                    _dbContext.DocumentActivities.Add(new DocumentActivity
                    {
                        DocumentType = DocumentType.Booking,
                        DocumentId = booking.Id,
                        ActivityType = ApprovalAction.BookingApproved,
                        Description = $"Booking {booking.BookingNumber} approved by admin.",
                        PerformedByUserId = CurrentUserId(),
                        PerformedAt = now
                    });

                    // Notify customer
                    _dbContext.CustomerNotifications.Add(new CustomerNotification
                    {
                        CustomerId = booking.CustomerId,
                        BookingId = booking.Id,
                        Type = CustomerNotificationType.BookingConfirmed,
                        Title = "Booking Confirmed",
                        Message = $"Your booking {booking.BookingNumber} has been approved and confirmed.",
                        ActionUrl = $"/MyBookings/Details/{booking.Id}",
                        CreatedAt = now
                    });

                    await _dbContext.SaveChangesAsync();
                    await dbTransaction.CommitAsync();

                    await _bookingApprovalNotifier.BroadcastCountAsync();

                    if (salesRecords.HasValue)
                    {
                        TempData.SetToast("success", "Booking Approved & Sales Records Created", 
                            $"Booking {booking.BookingNumber} confirmed. Sales Order {salesRecords.Value.SalesOrder.SalesOrderNumber}, Invoice {salesRecords.Value.Invoice.InvoiceNumber}, Payment {salesRecords.Value.Payment.PaymentNumber} created.");
                    }
                    else
                    {
                        TempData.SetToast("success", "Booking Approved", $"Booking {booking.BookingNumber} has been confirmed.");
                    }

                    return RedirectToAction(nameof(Approvals));
                }
                catch (Microsoft.EntityFrameworkCore.DbUpdateException dbEx)
                {
                    await dbTransaction.RollbackAsync();
                    var innerMessage = dbEx.InnerException?.Message ?? dbEx.Message;
                    _logger.LogError(dbEx, "ApproveBooking: DbUpdateException for booking {BookingId} (attempt {Attempt}/{MaxRetries}): {Message}", 
                        id, attempt + 1, maxRetries + 1, innerMessage);

                    // Check if it's a unique constraint violation on SalesOrderNumber (race condition)
                    var isSalesOrderNumberConflict = innerMessage.Contains("IX_SalesOrders_SalesOrderNumber", StringComparison.OrdinalIgnoreCase)
                        || innerMessage.Contains("duplicate key", StringComparison.OrdinalIgnoreCase)
                        || (innerMessage.Contains("UNIQUE", StringComparison.OrdinalIgnoreCase) && innerMessage.Contains("SalesOrderNumber", StringComparison.OrdinalIgnoreCase));

                    if (isSalesOrderNumberConflict && attempt < maxRetries)
                    {
                        // Clear change tracker to remove entities from failed SaveChanges attempt
                        _dbContext.ChangeTracker.Clear();
                        
                        // Reload booking to get clean state
                        booking = await _dbContext.Bookings
                            .Include(x => x.Customer)
                            .Include(x => x.Court).ThenInclude(x => x.Facility)
                            .Include(x => x.Sport)
                            .Include(x => x.PaymentTransaction)
                            .Include(x => x.CustomerMembership)
                            .FirstOrDefaultAsync(x => x.Id == id && x.IsActive);
                        
                        if (booking == null)
                        {
                            TempData.SetToast("error", "Not Found", "Booking not found during retry.");
                            return RedirectToAction(nameof(Approvals));
                        }
                        
                        // Reset booking status for retry
                        booking.Status = BookingStatus.PendingApproval;
                        booking.ConfirmedAt = null;
                        booking.SalesOrderId = null;
                        booking.UpdatedAt = DateTime.UtcNow;
                        booking.UpdatedByUserId = CurrentUserId();
                        
                        // Brief delay before retry
                        await Task.Delay(50);
                        continue; // Retry the approval
                    }

                    // Only treat as a slot conflict when another booking truly occupies the slot.
                    // Re-check availability after rollback so unique/concurrency errors from
                    // Sales/Invoice/Payment records are not misreported as booking conflicts.
                    var availability = await _validationService.CheckAvailabilityAsync(
                        booking.CourtId, booking.BookingDate, booking.StartTime, booking.EndTime, booking.Id);

                    if (!availability.IsAvailable)
                    {
                        _logger.LogWarning("ApproveBooking: Genuine slot conflict for booking {BookingId}: {Message} ({ConflictDetail})",
                            id, availability.Message, availability.ConflictDetail);
                        TempData.SetToast("error", "Slot Conflict", availability.Message);
                    }
                    else
                    {
                        _logger.LogError(dbEx, "ApproveBooking: Database error for booking {BookingId} (attempt {Attempt}/{MaxRetries}): {Message}",
                            id, attempt + 1, maxRetries + 1, innerMessage);
                        TempData.SetToast("error", "Approval Failed", "A database error occurred while saving. Please try again or contact support.");
                    }
                    return RedirectToAction(nameof(Approvals));
                }
                catch (InvalidOperationException invEx) when (invEx.Message.Contains("Payment transaction not found or not succeeded", StringComparison.OrdinalIgnoreCase)
                    || invEx.Message.Contains("Booking must be in Confirmed status", StringComparison.OrdinalIgnoreCase))
                {
                    await dbTransaction.RollbackAsync();
                    _logger.LogWarning(invEx, "ApproveBooking: InvalidOperationException for booking {BookingId}: {Message}", id, invEx.Message);
                    TempData.SetToast("error", "Payment Incomplete", invEx.Message);
                    return RedirectToAction(nameof(Approvals));
                }
                catch (Exception ex)
                {
                    await dbTransaction.RollbackAsync();
                    _logger.LogError(ex, "ApproveBooking: Unexpected error for booking {BookingId} (attempt {Attempt}/{MaxRetries})", id, attempt + 1, maxRetries + 1);
                    if (attempt < maxRetries)
                    {
                        await Task.Delay(50);
                        continue;
                    }
                    TempData.SetToast("error", "Approval Failed", "An unexpected error occurred. Please try again or contact support.");
                    return RedirectToAction(nameof(Approvals));
                }
            }

            // If we exhausted retries
            TempData.SetToast("error", "Approval Failed", "Unable to complete approval after multiple attempts. Please try again.");
            return RedirectToAction(nameof(Approvals));
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> RejectBooking(int id, string rejectionReason)
        {
            if (!CanManage()) return RedirectToAction("NotFound", "Error");

            if (string.IsNullOrWhiteSpace(rejectionReason) || rejectionReason.Trim().Length < 10)
            {
                TempData.SetToast("warning", "Reason Required", "Rejection reason must be at least 10 characters.");
                return RedirectToAction(nameof(Approvals));
            }

            var booking = await _dbContext.Bookings
                .Include(x => x.Customer)
                .Include(x => x.Court).ThenInclude(x => x.Facility)
                .Include(x => x.Sport)
                .Include(x => x.PaymentTransaction)
                .FirstOrDefaultAsync(x => x.Id == id && x.IsActive);

            if (booking == null)
            {
                TempData.SetToast("error", "Not Found", "Booking not found.");
                return RedirectToAction(nameof(Approvals));
            }

            if (booking.Status != BookingStatus.PendingApproval)
            {
                TempData.SetToast("warning", "Invalid State", "Only bookings pending approval can be rejected.");
                return RedirectToAction(nameof(Approvals));
            }

            var previousStatus = booking.Status.ToString();
            var now = DateTime.UtcNow;
            var settings = await GetSettingsAsync();
            var currency = settings.Currency;

            // Check if payment was made and refund is needed
            var paymentMade = booking.PaymentStatus == PaymentStatus.Paid && booking.PaidAmount > 0 && booking.PaymentTransaction != null;
            var refundableAmount = paymentMade ? await _refundService.GetRefundableAmountAsync(booking.Id) : 0;

            booking.Status = BookingStatus.Cancelled;
            booking.IsActive = false;
            booking.CancelledAt = now;
            booking.CancellationReason = $"Admin rejection: {rejectionReason.Trim()}";
            booking.UpdatedAt = now;
            booking.UpdatedByUserId = CurrentUserId();

            // Record rejection audit trail
            _dbContext.DocumentApprovals.Add(new DocumentApproval
            {
                DocumentType = DocumentType.Booking,
                DocumentId = booking.Id,
                DocumentNumber = booking.BookingNumber,
                RevisionNumber = 1,
                Action = ApprovalAction.BookingRejected,
                PreviousStatus = previousStatus,
                NewStatus = booking.Status.ToString(),
                SubmittedByUserId = CurrentUserId(),
                ActionByUserId = CurrentUserId(),
                ActionAt = now,
                Comments = Clean(null),
                RejectionReason = rejectionReason.Trim(),
                FinancialTotal = booking.TotalAmount,
                Currency = currency,
                IsFinal = true,
                IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString(),
                CreatedAt = now
            });

            _dbContext.DocumentActivities.Add(new DocumentActivity
            {
                DocumentType = DocumentType.Booking,
                DocumentId = booking.Id,
                ActivityType = ApprovalAction.BookingRejected,
                Description = $"Booking {booking.BookingNumber} rejected by admin. Reason: {rejectionReason.Trim()}",
                PerformedByUserId = CurrentUserId(),
                PerformedAt = now
            });

            // Notify customer of rejection
            _dbContext.CustomerNotifications.Add(new CustomerNotification
            {
                CustomerId = booking.CustomerId,
                BookingId = booking.Id,
                Type = CustomerNotificationType.BookingCancelled,
                Title = "Booking Rejected",
                Message = $"Your booking {booking.BookingNumber} was not approved. Reason: {rejectionReason.Trim()}. Please contact the arena if you believe this is an error.",
                ActionUrl = $"/MyBookings/Details/{booking.Id}",
                CreatedAt = now
            });

            // Disable reminders
            var reminders = await _dbContext.BookingReminders.Where(x => x.BookingId == booking.Id && x.IsActive).ToListAsync();
            foreach (var reminder in reminders)
            {
                reminder.IsActive = false;
                reminder.UpdatedAt = now;
            }

            // Initiate refund if payment was made
            PaymentRefund? refund = null;
            if (paymentMade && refundableAmount > 0 && booking.PaymentTransactionId.HasValue)
            {
                try
                {
                    refund = await _refundService.InitiateRefundAsync(
                        booking.Id,
                        booking.PaymentTransactionId.Value,
                        booking.SalesOrderId.HasValue
                            ? (await _dbContext.Payments.FirstOrDefaultAsync(x => x.InvoiceId == booking.SalesOrder!.InvoiceId))?.Id
                            : null,
                        CurrentUserId(),
                        RefundReason.BookingRejected,
                        $"Admin rejection: {rejectionReason.Trim()}",
                        refundableAmount,
                        RefundMethod.GatewayRefund
                    );
                }
                catch (Exception ex)
                {
                    // Log but don't fail the rejection
                    _dbContext.DocumentActivities.Add(new DocumentActivity
                    {
                        DocumentType = DocumentType.Booking,
                        DocumentId = booking.Id,
                        ActivityType = ApprovalAction.RefundFailed,
                        Description = $"Failed to initiate refund for booking {booking.BookingNumber}: {ex.Message}",
                        PerformedByUserId = CurrentUserId(),
                        PerformedAt = now
                    });
                }
            }

            await _dbContext.SaveChangesAsync();

            await _bookingApprovalNotifier.BroadcastCountAsync();

            var message = $"Booking {booking.BookingNumber} has been rejected and cancelled.";
            if (refund != null)
            {
                message += $" Refund {refund.RefundNumber} for {currency} {refund.Amount:N2} initiated.";
            }
            else if (paymentMade && refundableAmount <= 0)
            {
                message += " No refundable amount remaining.";
            }
            else if (paymentMade)
            {
                message += " Payment detected but refund could not be initiated automatically.";
            }

            TempData.SetToast("warning", "Booking Rejected", message);
            return RedirectToAction(nameof(Approvals));
        }

        [HttpGet]
        public async Task<IActionResult> Reschedule(int id)
        {
            if (!CanManage()) return RedirectToAction("NotFound", "Error");
            var booking = await _dbContext.Bookings.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
            if (booking == null) return NotFound();
            if (!CanEdit(booking.Status)) return RedirectToAction(nameof(Details), new { id });
            var model = MapForm(booking);
            return View(await AddFormOptionsAsync(model));
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Reschedule(int id, BookingFormViewModel model, string rescheduleReason)
        {
            if (!CanManage()) return RedirectToAction("NotFound", "Error");
            if (string.IsNullOrWhiteSpace(rescheduleReason) || rescheduleReason.Trim().Length < 10) ModelState.AddModelError(nameof(rescheduleReason), "Reschedule reason must be at least 10 characters.");
            model.Id = id;
            await ValidateBookingAsync(model, id);
            var booking = await _dbContext.Bookings.FirstOrDefaultAsync(x => x.Id == id);
            if (booking == null) return NotFound();
            if (!ModelState.IsValid) return View(await AddFormOptionsAsync(model));
            if (!CanEdit(booking.Status)) return RedirectToAction(nameof(Details), new { id });

            await using var dbTransaction = await _dbContext.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
            try
            {
                var recheck = await _validationService.CheckAvailabilityAsync(model.CourtId, model.BookingDate, model.StartTime, model.EndTime, id);
                if (!recheck.IsAvailable)
                {
                    await dbTransaction.RollbackAsync();
                    ModelState.AddModelError(string.Empty, recheck.Message);
                    return View(await AddFormOptionsAsync(model));
                }
                if (booking.MembershipHoursApplied) await RestoreMembershipHoursAsync(booking);
                await MapBookingAsync(model, booking);
                booking.Status = booking.Status == BookingStatus.Confirmed ? BookingStatus.Confirmed : BookingStatus.Pending;
                booking.RescheduleReason = rescheduleReason.Trim();
                booking.UpdatedAt = DateTime.UtcNow;
                booking.UpdatedByUserId = CurrentUserId();
                await _dbContext.SaveChangesAsync();
                await ApplyMembershipHoursIfNeededAsync(booking);
                await dbTransaction.CommitAsync();
            }
            catch (Microsoft.EntityFrameworkCore.DbUpdateException)
            {
                await dbTransaction.RollbackAsync();
                ModelState.AddModelError(string.Empty, "A conflicting booking was created at the same time. Please select another slot.");
                return View(await AddFormOptionsAsync(model));
            }
            catch (Exception)
            {
                await dbTransaction.RollbackAsync();
                ModelState.AddModelError(string.Empty, "The booking could not be rescheduled. Please try again.");
                return View(await AddFormOptionsAsync(model));
            }
            TempData.SetToast("success", "Success", "Booking rescheduled successfully.");
            return RedirectToAction(nameof(Details), new { id });
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Confirm(int id)
        {
            var booking = await LoadBookingForActionAsync(id);
            if (booking == null) return JsonFail("Booking not found.");
            if (!CanManage()) return JsonFail("You do not have permission.");
            if (booking.Status is not (BookingStatus.Pending or BookingStatus.Draft)) return JsonFail("Only pending or draft bookings can be confirmed.");
            var availability = await _validationService.CheckAvailabilityAsync(booking.CourtId, booking.BookingDate, booking.StartTime, booking.EndTime, booking.Id);
            if (!availability.IsAvailable) return JsonFail(availability.Message);
            booking.Status = BookingStatus.Confirmed; booking.ConfirmedAt = DateTime.UtcNow; booking.UpdatedAt = DateTime.UtcNow; booking.UpdatedByUserId = CurrentUserId();
            await _dbContext.SaveChangesAsync();
            await ApplyMembershipHoursIfNeededAsync(booking);
            return JsonOk("Booking confirmed successfully.");
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Cancel(int id, string reason)
        {
            var booking = await LoadBookingForActionAsync(id);
            if (booking == null) return JsonFail("Booking not found.");
            if (!CanManage()) return JsonFail("You do not have permission.");
            if (booking.Status is BookingStatus.Completed or BookingStatus.Cancelled or BookingStatus.NoShow) return JsonFail("This booking is already final.");
            if (string.IsNullOrWhiteSpace(reason) || reason.Trim().Length < 10) return JsonFail("Cancellation reason must be at least 10 characters.");
            if (booking.MembershipHoursApplied && booking.BookingDate.ToDateTime(booking.StartTime) > DateTime.Now) await RestoreMembershipHoursAsync(booking);
            booking.Status = BookingStatus.Cancelled; booking.IsActive = false; booking.CancelledAt = DateTime.UtcNow; booking.CancellationReason = reason.Trim(); booking.UpdatedAt = DateTime.UtcNow; booking.UpdatedByUserId = CurrentUserId();
            await _dbContext.SaveChangesAsync();
            return JsonOk("Booking cancelled successfully.");
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> CheckIn(int id) => await ChangeStatusAsync(id, BookingStatus.Confirmed, BookingStatus.CheckedIn, "Booking checked in successfully.", requireToday: true, setCheckedIn: true);

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> StartBooking(int id) => await ChangeStatusAsync(id, BookingStatus.CheckedIn, BookingStatus.InProgress, "Booking marked in progress.");

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Complete(int id)
        {
            var booking = await LoadBookingForActionAsync(id);
            if (booking == null) return JsonFail("Booking not found.");
            if (!CanManage()) return JsonFail("You do not have permission.");
            if (booking.Status is not (BookingStatus.CheckedIn or BookingStatus.InProgress)) return JsonFail("Only checked-in or in-progress bookings can be completed.");
            booking.Status = BookingStatus.Completed; booking.CompletedAt = DateTime.UtcNow; booking.CheckedOutAt = DateTime.UtcNow; booking.UpdatedAt = DateTime.UtcNow; booking.UpdatedByUserId = CurrentUserId();
            var customer = await _dbContext.Customers.FindAsync(booking.CustomerId);
            if (customer != null) customer.LastVisitAt = DateTime.UtcNow;
            await _dbContext.SaveChangesAsync();
            await ApplyMembershipHoursIfNeededAsync(booking);
            return JsonOk("Booking completed successfully.");
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> MarkNoShow(int id)
        {
            var booking = await LoadBookingForActionAsync(id);
            if (booking == null) return JsonFail("Booking not found.");
            if (!CanManage()) return JsonFail("You do not have permission.");
            if (booking.Status != BookingStatus.Confirmed) return JsonFail("Only confirmed bookings can be marked no-show.");
            if (booking.BookingDate.ToDateTime(booking.StartTime) > DateTime.Now) return JsonFail("Booking start time has not passed yet.");
            if (booking.MembershipHoursApplied) await RestoreMembershipHoursAsync(booking);
            booking.Status = BookingStatus.NoShow; booking.IsActive = false; booking.UpdatedAt = DateTime.UtcNow; booking.UpdatedByUserId = CurrentUserId();
            await _dbContext.SaveChangesAsync();
            return JsonOk("Booking marked as no-show.");
        }

        [HttpGet]
        public async Task<IActionResult> GetCustomerSummary(int customerId)
        {
            var customer = await _dbContext.Customers.AsNoTracking().FirstOrDefaultAsync(x => x.Id == customerId);
            if (customer == null) return Json(new { success = false, message = "Customer not found." });
            var membership = await ActiveMembershipQuery(customerId, DateOnly.FromDateTime(DateTime.UtcNow)).FirstOrDefaultAsync();
            return Json(new { success = true, customer = new { customer.Id, customer.FullName, customer.CustomerCode, customer.PrimaryPhone, customer.CustomerType, customer.LoyaltyPoints, customer.OutstandingBalance, customer.IsBlacklisted, customer.BlacklistReason, customer.LastVisitAt, membership = membership == null ? null : new { membership.Id, membership.MembershipNumber, plan = membership.MembershipPlan.Name, membership.DiscountPercentage, membership.IncludedBookingHours, membership.UsedBookingHours } } });
        }

        [HttpGet]
        public async Task<IActionResult> GetMembershipSummary(int customerId, int sportId, bool isPeakRate = false)
        {
            var membership = await ActiveMembershipQuery(customerId, DateOnly.FromDateTime(DateTime.UtcNow)).FirstOrDefaultAsync();
            if (membership == null) return Json(new { success = true, applies = false, message = "No active membership benefits available." });
            var sport = await _dbContext.Sports.AsNoTracking().FirstOrDefaultAsync(x => x.Id == sportId);
            var validation = ValidateMembershipBenefit(membership, sport?.Code, isPeakRate);
            return Json(new { success = true, applies = validation.Valid, message = validation.Message, membership = new { membership.Id, membership.MembershipNumber, plan = membership.MembershipPlan.Name, membership.DiscountPercentage, remainingHours = Math.Max(0, membership.IncludedBookingHours - membership.UsedBookingHours) } });
        }

        [HttpGet]
        public async Task<IActionResult> GetFacilitiesBySport(int sportId)
        {
            var facilities = await _dbContext.Courts.AsNoTracking().Where(x => x.SportId == sportId && x.IsActive && x.Facility.IsActive).Select(x => new { x.Facility.Id, x.Facility.Name }).Distinct().OrderBy(x => x.Name).ToListAsync();
            return Json(new { success = true, facilities });
        }

        [HttpGet]
        public async Task<IActionResult> GetCourts(int sportId, int facilityId)
        {
            var courts = await _dbContext.Courts.AsNoTracking().Where(x => x.SportId == sportId && x.FacilityId == facilityId && x.IsActive).OrderBy(x => x.DisplayOrder).Select(x => new { x.Id, x.Name, x.Capacity, status = x.Status.ToString() }).ToListAsync();
            return Json(new { success = true, courts });
        }

        [HttpGet]
        public async Task<IActionResult> GetAvailableSlots(int courtId, DateOnly bookingDate, int durationMinutes, int? bookingId = null)
        {
            var slots = await GenerateSlotsAsync(courtId, bookingDate, durationMinutes, bookingId);
            return Json(new { success = true, slots });
        }

        [HttpGet]
        public async Task<IActionResult> CalculatePrice(int customerId, int sportId, int courtId, DateOnly bookingDate, TimeOnly startTime, int durationMinutes, decimal manualDiscountAmount = 0, decimal paidAmount = 0, int? bookingId = null)
        {
            var endTime = startTime.AddMinutes(durationMinutes);
            var summary = await CalculatePriceAsync(customerId, sportId, courtId, bookingDate, startTime, endTime, durationMinutes, manualDiscountAmount, paidAmount, bookingId);
            return Json(new { success = string.IsNullOrWhiteSpace(summary.Message), summary });
        }

        private IQueryable<Booking> BuildBookingQuery(BookingIndexViewModel model)
        {
            var query = _dbContext.Bookings.AsNoTracking().Include(x => x.Customer).Include(x => x.Court).ThenInclude(x => x.Facility).Include(x => x.Sport).AsQueryable();
            if (!string.IsNullOrWhiteSpace(model.Search))
            {
                var search = model.Search.ToLower();
                query = query.Where(x => x.BookingNumber.ToLower().Contains(search) || x.Customer.FirstName.ToLower().Contains(search) || x.Customer.LastName.ToLower().Contains(search) || x.Customer.CustomerCode.ToLower().Contains(search) || x.Customer.PrimaryPhone.Contains(search) || x.Court.Name.ToLower().Contains(search) || x.Sport.Name.ToLower().Contains(search) || (x.ReferenceNumber != null && x.ReferenceNumber.ToLower().Contains(search)));
            }
            if (model.SportId.HasValue) query = query.Where(x => x.SportId == model.SportId);
            if (model.FacilityId.HasValue) query = query.Where(x => x.FacilityId == model.FacilityId);
            if (model.CourtId.HasValue) query = query.Where(x => x.CourtId == model.CourtId);
            if (model.CustomerId.HasValue) query = query.Where(x => x.CustomerId == model.CustomerId);
            if (model.Status.HasValue) query = query.Where(x => x.Status == model.Status);
            if (model.PaymentStatus.HasValue) query = query.Where(x => x.PaymentStatus == model.PaymentStatus);
            if (model.Source.HasValue) query = query.Where(x => x.Source == model.Source);
            if (model.DateFrom.HasValue) query = query.Where(x => x.BookingDate >= model.DateFrom);
            if (model.DateTo.HasValue) query = query.Where(x => x.BookingDate <= model.DateTo);
            return model.SortBy switch
            {
                "date_asc" => query.OrderBy(x => x.BookingDate).ThenBy(x => x.StartTime),
                "newest" => query.OrderByDescending(x => x.CreatedAt),
                "oldest" => query.OrderBy(x => x.CreatedAt),
                "customer" => query.OrderBy(x => x.Customer.FirstName).ThenBy(x => x.Customer.LastName),
                "court" => query.OrderBy(x => x.Court.Name),
                "amount" => query.OrderByDescending(x => x.TotalAmount),
                "status" => query.OrderBy(x => x.Status == BookingStatus.Pending || x.Status == BookingStatus.PendingApproval ? 0 : x.Status == BookingStatus.Confirmed ? 1 : x.Status == BookingStatus.Cancelled ? 3 : 2).ThenByDescending(x => x.CreatedAt),
                _ => query.OrderByDescending(x => x.BookingDate).ThenBy(x => x.StartTime)
            };
        }

        private static BookingListItemViewModel MapListItem(Booking x) => new()
        {
            Id = x.Id, BookingNumber = x.BookingNumber, CustomerName = x.Customer?.FullName ?? "Unknown Customer", CustomerInitials = x.Customer?.Initials ?? "GH", CustomerPhone = x.Customer?.PrimaryPhone ?? string.Empty,
            IsMemberBooking = x.IsMemberBooking, BookingDate = x.BookingDate, StartTime = x.StartTime, EndTime = x.EndTime, DurationMinutes = x.DurationMinutes,
            CourtName = x.Court?.Name ?? "Unknown Court", FacilityName = x.Facility?.Name ?? x.Court?.Facility?.Name ?? "Unknown Facility", SportName = x.Sport?.Name ?? "Unknown Sport", TotalAmount = x.TotalAmount, BalanceAmount = x.BalanceAmount,
            Status = x.Status, PaymentStatus = x.PaymentStatus, Source = x.Source, IsWalkIn = x.IsWalkIn, CreatedAt = x.CreatedAt
        };

        private async Task<BookingDetailsViewModel> MapDetailsAsync(Booking x)
        {
            var createdBy = await _dbContext.Users.AsNoTracking().Where(u => u.Id == x.CreatedByUserId).Select(u => u.FirstName + " " + u.LastName).FirstOrDefaultAsync() ?? "System";
            var item = MapListItem(x);
            return new BookingDetailsViewModel
            {
                Id = item.Id, BookingNumber = item.BookingNumber, CustomerName = item.CustomerName, CustomerInitials = item.CustomerInitials, CustomerPhone = item.CustomerPhone,
                CustomerId = x.CustomerId, CustomerCode = x.Customer.CustomerCode, CustomerEmail = x.Customer.Email, CustomerType = x.Customer.CustomerType.GetDisplayName(), LoyaltyPoints = x.Customer.LoyaltyPoints,
                OutstandingBalance = x.Customer.OutstandingBalance, IsMemberBooking = x.IsMemberBooking, BookingDate = item.BookingDate, StartTime = item.StartTime, EndTime = item.EndTime,
                DurationMinutes = item.DurationMinutes, CourtName = item.CourtName, FacilityName = item.FacilityName, SportName = item.SportName, PlayerCount = x.PlayerCount,
                Source = x.Source, TotalAmount = item.TotalAmount, BalanceAmount = item.BalanceAmount, BaseAmount = x.BaseAmount, MembershipDiscountAmount = x.MembershipDiscountAmount,
                ManualDiscountAmount = x.ManualDiscountAmount, TaxAmount = x.TaxAmount, PaidAmount = x.PaidAmount, Status = item.Status, PaymentStatus = item.PaymentStatus,
                CustomerNotes = x.CustomerNotes, InternalNotes = x.InternalNotes, CancellationReason = x.CancellationReason, RescheduleReason = x.RescheduleReason, SpecialRequest = x.SpecialRequest,
                ReferenceNumber = x.ReferenceNumber, MembershipNumber = x.CustomerMembership?.MembershipNumber, MembershipPlanName = x.CustomerMembership?.MembershipPlan.Name,
                MembershipHoursApplied = x.MembershipHoursApplied, CreatedAt = x.CreatedAt, CreatedByName = createdBy, UpdatedAt = x.UpdatedAt, ConfirmedAt = x.ConfirmedAt,
                CancelledAt = x.CancelledAt, CompletedAt = x.CompletedAt, CheckedInAt = x.CheckedInAt, CheckedOutAt = x.CheckedOutAt, CanManage = CanManage(), CanOperate = CanOperate(),
                CurrencySymbol = (await GetSettingsAsync()).CurrencySymbol ?? "Rs"
            };
        }

        private BookingFormViewModel MapForm(Booking x) => new()
        {
            Id = x.Id, BookingNumber = x.BookingNumber, CustomerId = x.CustomerId, SportId = x.SportId, FacilityId = x.FacilityId, CourtId = x.CourtId,
            CustomerMembershipId = x.CustomerMembershipId, BookingDate = x.BookingDate, StartTime = x.StartTime, EndTime = x.EndTime, DurationMinutes = x.DurationMinutes,
            PlayerCount = x.PlayerCount, Source = x.Source, Status = x.Status, IsWalkIn = x.IsWalkIn, CustomerNotes = x.CustomerNotes, InternalNotes = x.InternalNotes,
            SpecialRequest = x.SpecialRequest, ReferenceNumber = x.ReferenceNumber, ManualDiscountAmount = x.ManualDiscountAmount, PaidAmount = x.PaidAmount,
            PriceSummary = new BookingPriceSummaryViewModel { BaseAmount = x.BaseAmount, MembershipDiscountAmount = x.MembershipDiscountAmount, ManualDiscountAmount = x.ManualDiscountAmount, TaxAmount = x.TaxAmount, TotalAmount = x.TotalAmount, PaidAmount = x.PaidAmount, BalanceAmount = x.BalanceAmount, IsPeakRate = x.IsPeakRate }
        };

        private async Task MapBookingAsync(BookingFormViewModel model, Booking booking)
        {
            var price = await CalculatePriceAsync(model.CustomerId, model.SportId, model.CourtId, model.BookingDate, model.StartTime, model.EndTime, model.DurationMinutes, model.ManualDiscountAmount, model.PaidAmount, model.Id);
            booking.CustomerId = model.CustomerId; booking.SportId = model.SportId; booking.FacilityId = model.FacilityId; booking.CourtId = model.CourtId; booking.CustomerMembershipId = model.CustomerMembershipId;
            booking.BookingDate = model.BookingDate; booking.StartTime = model.StartTime; booking.EndTime = model.EndTime; booking.DurationMinutes = model.DurationMinutes; booking.PlayerCount = model.PlayerCount;
            booking.Source = model.Source; booking.Status = model.Status; booking.IsWalkIn = model.IsWalkIn || model.Source == BookingSource.WalkIn; booking.CustomerNotes = Clean(model.CustomerNotes);
            booking.InternalNotes = Clean(model.InternalNotes); booking.SpecialRequest = Clean(model.SpecialRequest); booking.ReferenceNumber = Clean(model.ReferenceNumber);
            booking.BaseAmount = price.BaseAmount; booking.MembershipDiscountAmount = price.MembershipDiscountAmount; booking.ManualDiscountAmount = price.ManualDiscountAmount; booking.TaxAmount = price.TaxAmount;
            booking.TotalAmount = price.TotalAmount; booking.PaidAmount = price.PaidAmount; booking.BalanceAmount = price.BalanceAmount; booking.PaymentStatus = booking.PaidAmount <= 0 ? PaymentStatus.Unpaid : booking.PaidAmount >= booking.TotalAmount ? PaymentStatus.Paid : PaymentStatus.PartiallyPaid;
            booking.IsPeakRate = price.IsPeakRate; booking.IsMemberBooking = booking.CustomerMembershipId.HasValue && booking.MembershipDiscountAmount > 0; booking.IsActive = booking.Status is not (BookingStatus.Cancelled or BookingStatus.NoShow);
        }

        private async Task ValidateBookingAsync(BookingFormViewModel model, int? existingId = null)
        {
            if (model.ManualDiscountAmount > 0 && !CanManualDiscount())
                ModelState.AddModelError(nameof(model.ManualDiscountAmount), "Only Super Admin and Admin can apply manual discounts.");

            var result = await _validationService.ValidateBookingAsync(
                model.CustomerId, model.SportId, model.FacilityId, model.CourtId,
                model.BookingDate, model.StartTime, model.EndTime,
                model.DurationMinutes, model.PlayerCount, model.ManualDiscountAmount,
                model.Source, model.IsWalkIn, existingId);

            foreach (var error in result.Errors)
                ModelState.AddModelError(string.Empty, error);

            var price = await CalculatePriceAsync(model.CustomerId, model.SportId, model.CourtId, model.BookingDate, model.StartTime, model.EndTime, model.DurationMinutes, model.ManualDiscountAmount, model.PaidAmount, existingId);
            if (!string.IsNullOrWhiteSpace(price.Message)) ModelState.AddModelError(string.Empty, price.Message);
            model.PriceSummary = price;
            model.CustomerMembershipId = await GetApplicableMembershipIdAsync(model.CustomerId, model.SportId, model.BookingDate, price.IsPeakRate);
        }

        private async Task<BookingPriceSummaryViewModel> CalculatePriceAsync(int customerId, int sportId, int courtId, DateOnly bookingDate, TimeOnly startTime, TimeOnly endTime, int durationMinutes, decimal manualDiscount, decimal paidAmount, int? bookingId = null)
        {
            var settings = await GetSettingsAsync();
            var rule = await FindPricingRuleAsync(courtId, bookingDate.DayOfWeek, startTime, endTime, durationMinutes);
            if (rule == null) return new BookingPriceSummaryViewModel { CurrencySymbol = settings.CurrencySymbol ?? "Rs", Message = "No active pricing rule is configured for the selected court and time." };
            var baseAmount = Math.Round(rule.Price * Math.Max(1, durationMinutes) / Math.Max(1, rule.DurationMinutes), 2);
            var membershipDiscount = 0m;
            var membership = await ActiveMembershipQuery(customerId, bookingDate).FirstOrDefaultAsync();
            if (membership != null)
            {
                var sportCode = await _dbContext.Sports.AsNoTracking().Where(x => x.Id == sportId).Select(x => x.Code).FirstOrDefaultAsync();
                var check = ValidateMembershipBenefit(membership, sportCode, rule.IsPeakRate);
                if (check.Valid) membershipDiscount = Math.Min(baseAmount, Math.Round(baseAmount * membership.DiscountPercentage / 100, 2));
            }
            var subtotal = Math.Max(0, baseAmount - membershipDiscount);
            manualDiscount = CanManualDiscount() ? Math.Min(Math.Max(0, manualDiscount), subtotal) : 0;
            var taxable = Math.Max(0, subtotal - manualDiscount);
            var tax = Math.Round(taxable * settings.TaxPercentage / 100, 2);
            var total = Math.Max(0, taxable + tax);
            paidAmount = Math.Min(Math.Max(0, paidAmount), total);
            return new BookingPriceSummaryViewModel { BaseAmount = baseAmount, MembershipDiscountAmount = membershipDiscount, ManualDiscountAmount = manualDiscount, TaxAmount = tax, TotalAmount = total, PaidAmount = paidAmount, BalanceAmount = total - paidAmount, IsPeakRate = rule.IsPeakRate, CurrencySymbol = settings.CurrencySymbol ?? "Rs" };
        }

        private async Task<CourtPricing?> FindPricingRuleAsync(int courtId, DayOfWeek day, TimeOnly start, TimeOnly end, int duration)
        {
            var rules = await _dbContext.CourtPricings.AsNoTracking().Where(x => x.CourtId == courtId && x.IsActive && x.StartTime <= start && x.EndTime >= end && x.DurationMinutes > 0).ToListAsync();
            return rules.Where(x => x.DayOfWeek == day).OrderByDescending(x => x.IsPeakRate).ThenBy(x => x.Price).FirstOrDefault()
                ?? rules.Where(x => x.DayOfWeek == null && x.IsPeakRate).OrderBy(x => x.Price).FirstOrDefault()
                ?? rules.Where(x => x.DayOfWeek == null).OrderBy(x => x.Price).FirstOrDefault()
                ?? rules.OrderBy(x => x.Price).FirstOrDefault();
        }

        private async Task<(bool Available, string Message)> IsAvailableAsync(int courtId, DateOnly date, TimeOnly start, TimeOnly end, int? durationMinutes, int? bookingId = null)
        {
            var result = await _validationService.CheckAvailabilityAsync(courtId, date, start, end, bookingId);
            return (result.IsAvailable, result.Message);
        }

        private async Task<List<AvailableSlotViewModel>> GenerateSlotsAsync(int courtId, DateOnly date, int durationMinutes, int? bookingId)
        {
            var slots = new List<AvailableSlotViewModel>();
            var schedule = await _dbContext.CourtSchedules.AsNoTracking().FirstOrDefaultAsync(x => x.CourtId == courtId && x.DayOfWeek == date.DayOfWeek && x.IsActive);
            if (schedule == null || schedule.IsClosed) return slots;
            var step = schedule.SlotDurationMinutes > 0 ? schedule.SlotDurationMinutes : Math.Max(durationMinutes, 30);
            var openMinutes = schedule.OpeningTime.Hour * 60 + schedule.OpeningTime.Minute;
            var closeMinutes = schedule.ClosingTime.Hour * 60 + schedule.ClosingTime.Minute;
            for (var minute = openMinutes; minute + durationMinutes <= closeMinutes; minute += step + schedule.BufferMinutes)
            {
                var start = new TimeOnly(minute / 60, minute % 60);
                var end = start.AddMinutes(durationMinutes);
                var rule = await FindPricingRuleAsync(courtId, date.DayOfWeek, start, end, durationMinutes);
                var available = await IsAvailableAsync(courtId, date, start, end, durationMinutes, bookingId);
                slots.Add(new AvailableSlotViewModel { StartTime = start.ToString("HH:mm"), EndTime = end.ToString("HH:mm"), DisplayText = $"{start:HH\\:mm} - {end:HH\\:mm}", IsAvailable = available.Available && rule != null, PricingRuleId = rule?.Id, BasePrice = rule?.Price ?? 0, IsPeakRate = rule?.IsPeakRate ?? false, Message = rule == null ? "No pricing configured" : available.Message });
            }
            return slots;
        }

        private IQueryable<CustomerMembership> ActiveMembershipQuery(int customerId, DateOnly date) => _dbContext.CustomerMemberships.AsNoTracking().Include(x => x.MembershipPlan).Where(x => x.CustomerId == customerId && x.IsActive && x.Status == MembershipStatus.Active && x.StartDate <= date && x.ExpiryDate >= date);

        private (bool Valid, string Message) ValidateMembershipBenefit(CustomerMembership membership, string? sportCode, bool isPeak)
        {
            var allowed = membership.MembershipPlan.AllowedSportCodes;
            if (!string.IsNullOrWhiteSpace(allowed) && allowed != "ALL" && !string.IsNullOrWhiteSpace(sportCode) && !allowed.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).Contains(sportCode)) return (false, "Membership does not allow this sport.");
            if (isPeak && !membership.MembershipPlan.AllowPeakHours) return (false, "Membership does not allow peak-hour bookings.");
            if (!isPeak && !membership.MembershipPlan.AllowOffPeakHours) return (false, "Membership does not allow off-peak bookings.");
            return (true, "Membership benefits applied.");
        }

        private async Task<int?> GetApplicableMembershipIdAsync(int customerId, int sportId, DateOnly date, bool isPeak)
        {
            var membership = await ActiveMembershipQuery(customerId, date).FirstOrDefaultAsync();
            if (membership == null) return null;
            var sportCode = await _dbContext.Sports.AsNoTracking().Where(x => x.Id == sportId).Select(x => x.Code).FirstOrDefaultAsync();
            return ValidateMembershipBenefit(membership, sportCode, isPeak).Valid ? membership.Id : null;
        }

        private async Task ApplyMembershipHoursIfNeededAsync(Booking booking)
        {
            if (booking.MembershipHoursApplied || booking.CustomerMembershipId == null || booking.Status is not (BookingStatus.Confirmed or BookingStatus.Completed)) return;
            var membership = await _dbContext.CustomerMemberships.FirstOrDefaultAsync(x => x.Id == booking.CustomerMembershipId);
            if (membership == null) return;
            membership.UsedBookingHours += Math.Max(1, (int)Math.Ceiling(booking.DurationMinutes / 60m));
            membership.UpdatedAt = DateTime.UtcNow;
            booking.MembershipHoursApplied = true;
            await _dbContext.SaveChangesAsync();
        }

        private async Task RestoreMembershipHoursAsync(Booking booking)
        {
            if (!booking.MembershipHoursApplied || booking.CustomerMembershipId == null) return;
            var membership = await _dbContext.CustomerMemberships.FirstOrDefaultAsync(x => x.Id == booking.CustomerMembershipId);
            if (membership == null) return;
            membership.UsedBookingHours = Math.Max(0, membership.UsedBookingHours - Math.Max(1, (int)Math.Ceiling(booking.DurationMinutes / 60m)));
            membership.UpdatedAt = DateTime.UtcNow;
            booking.MembershipHoursApplied = false;
        }

        private async Task DisableRemindersAsync(int bookingId)
        {
            var reminders = await _dbContext.BookingReminders.Where(x => x.BookingId == bookingId && x.IsActive).ToListAsync();
            foreach (var reminder in reminders)
            {
                reminder.IsActive = false;
                reminder.UpdatedAt = DateTime.UtcNow;
            }
        }

        private async Task<Booking?> LoadBookingForActionAsync(int id) => await _dbContext.Bookings.Include(x => x.CustomerMembership).FirstOrDefaultAsync(x => x.Id == id);

        private async Task<IActionResult> ChangeStatusAsync(int id, BookingStatus from, BookingStatus to, string message, bool requireToday = false, bool setCheckedIn = false)
        {
            var booking = await LoadBookingForActionAsync(id);
            if (booking == null) return JsonFail("Booking not found.");
            if (!CanManage()) return JsonFail("You do not have permission.");
            if (booking.Status != from) return JsonFail($"Only {from.GetDisplayName()} bookings can use this action.");
            if (requireToday && booking.BookingDate != DateOnly.FromDateTime(DateTime.UtcNow)) return JsonFail("Booking date must be today.");
            booking.Status = to; booking.UpdatedAt = DateTime.UtcNow; booking.UpdatedByUserId = CurrentUserId();
            if (setCheckedIn) booking.CheckedInAt = DateTime.UtcNow;
            await _dbContext.SaveChangesAsync();
            return JsonOk(message);
        }

        private async Task<BookingFormViewModel> AddFormOptionsAsync(BookingFormViewModel model)
        {
            var settings = await GetSettingsAsync();
            model.CurrencySymbol = settings.CurrencySymbol ?? "Rs";
            model.ArenaName = settings.ArenaName;
            model.CanManualDiscount = CanManualDiscount();
            model.Customers = await _dbContext.Customers.AsNoTracking().Where(x => x.IsActive).OrderBy(x => x.FirstName).Select(x => new SelectListItem(x.FirstName + " " + x.LastName + " - " + x.PrimaryPhone, x.Id.ToString(), x.Id == model.CustomerId)).ToListAsync();
            model.Sports = await _dbContext.Sports.AsNoTracking().Where(x => x.IsActive).OrderBy(x => x.DisplayOrder).Select(x => new SelectListItem(x.Name, x.Id.ToString(), x.Id == model.SportId)).ToListAsync();
            model.Facilities = await _dbContext.Facilities.AsNoTracking().Where(x => x.IsActive).OrderBy(x => x.DisplayOrder).Select(x => new SelectListItem(x.Name, x.Id.ToString(), x.Id == model.FacilityId)).ToListAsync();
            model.Courts = await _dbContext.Courts.AsNoTracking().Where(x => x.IsActive).OrderBy(x => x.DisplayOrder).Select(x => new SelectListItem(x.Name, x.Id.ToString(), x.Id == model.CourtId)).ToListAsync();
            return model;
        }

        private async Task<SystemSettings> GetSettingsAsync() => await _dbContext.SystemSettings.AsNoTracking().FirstOrDefaultAsync() ?? new SystemSettings { ArenaName = "GameHub Arena", CurrencySymbol = "Rs", TaxPercentage = 0, OpeningTime = new TimeOnly(6, 0), ClosingTime = new TimeOnly(23, 0), SlotDurationMinutes = 60, BufferMinutes = 10, AdvanceBookingDays = 14, AllowWalkIn = true, AllowOnlineBooking = true, BookingPrefix = "BKG", Currency = "PKR", Phone = "", Email = "", InvoicePrefix = "INV", ReceiptPrefix = "RCPT" };
        private void NormalizeForm(BookingFormViewModel model) { model.EndTime = model.StartTime.AddMinutes(model.DurationMinutes); model.CustomerNotes = Clean(model.CustomerNotes); model.InternalNotes = Clean(model.InternalNotes); model.SpecialRequest = Clean(model.SpecialRequest); model.ReferenceNumber = Clean(model.ReferenceNumber); }
        private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        private static bool CanEdit(BookingStatus status) => status is BookingStatus.Draft or BookingStatus.Pending or BookingStatus.Confirmed;
        private bool CanManage() => RolePermissions.TryGetCurrentRole(User, out var role) && role is UserRole.SuperAdmin or UserRole.Admin or UserRole.BookingManager or UserRole.Receptionist;
        private bool CanOperate() => RolePermissions.TryGetCurrentRole(User, out var role) && role is UserRole.SuperAdmin or UserRole.Admin or UserRole.BookingManager or UserRole.Receptionist or UserRole.CourtManager or UserRole.FinanceManager;
        private bool CanManualDiscount() => RolePermissions.TryGetCurrentRole(User, out var role) && role is UserRole.SuperAdmin or UserRole.Admin;
        private int CurrentUserId() => int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : 0;
        private JsonResult JsonOk(string message) => Json(new { success = true, message });
        private JsonResult JsonFail(string message) => Json(new { success = false, message });
    }
}
