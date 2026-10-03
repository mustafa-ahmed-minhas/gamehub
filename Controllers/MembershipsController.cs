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

namespace GameHub.Controllers
{
    [GameHubAuthorize(UserRole.SuperAdmin, UserRole.Admin, UserRole.BookingManager, UserRole.Receptionist, UserRole.FinanceManager, UserRole.Viewer)]
    [ValidateActiveUser]
    public class MembershipsController : Controller
    {
        private static readonly int[] PageSizes = { 10, 25, 50 };
        private readonly ApplicationDbContext _dbContext;
        private readonly IMembershipNumberService _membershipNumberService;

        public MembershipsController(ApplicationDbContext dbContext, IMembershipNumberService membershipNumberService)
        {
            _dbContext = dbContext;
            _membershipNumberService = membershipNumberService;
        }

        [HttpGet]
        public async Task<IActionResult> Index(string tab = "plans", string? search = null, string? statusFilter = null, string? sortBy = null, int page = 1, int pageSize = 10, MembershipPlanType? planType = null, MembershipStatus? membershipStatus = null, int? planId = null, string? sportCode = null, string? expiryState = null)
        {
            await SyncExpiredMembershipsAsync();
            tab = tab?.Equals("customers", StringComparison.OrdinalIgnoreCase) == true ? "customers" : "plans";
            page = Math.Max(1, page);
            pageSize = PageSizes.Contains(pageSize) ? pageSize : 10;
            search = search?.Trim();
            statusFilter = statusFilter?.Trim().ToLowerInvariant();
            sortBy = sortBy?.Trim().ToLowerInvariant() ?? "newest";

            var model = await BaseIndexModelAsync(tab, search, statusFilter, sortBy, page, pageSize, planType, membershipStatus, planId, sportCode, expiryState);
            if (tab == "customers") await LoadCustomerMembershipsAsync(model);
            else await LoadPlansAsync(model);
            return View(model);
        }

        [HttpGet]
        public async Task<IActionResult> CreatePlan()
        {
            if (EnsureCanManage() is IActionResult blocked) return blocked;
            return View("PlanForm", await AddPlanOptionsAsync(new MembershipPlanFormViewModel()));
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> CreatePlan(MembershipPlanFormViewModel model)
        {
            if (EnsureCanManage() is IActionResult blocked) return blocked;
            NormalizePlan(model);
            await ValidatePlanAsync(model);
            if (!ModelState.IsValid) return View("PlanForm", await AddPlanOptionsAsync(model));
            var plan = new MembershipPlan();
            MapPlan(model, plan);
            plan.CreatedAt = DateTime.UtcNow;
            _dbContext.MembershipPlans.Add(plan);
            await _dbContext.SaveChangesAsync();
            TempData.SetToast("success", "Success", "Membership plan created successfully.");
            return RedirectToAction(nameof(Index), new { tab = "plans" });
        }

        [HttpGet]
        public async Task<IActionResult> EditPlan(int id)
        {
            if (EnsureCanManage() is IActionResult blocked) return blocked;
            var plan = await _dbContext.MembershipPlans.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
            if (plan == null) return NotFound();
            return View("PlanForm", await AddPlanOptionsAsync(MapPlanForm(plan)));
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> EditPlan(int id, MembershipPlanFormViewModel model)
        {
            if (EnsureCanManage() is IActionResult blocked) return blocked;
            if (model.Id != id) return NotFound();
            NormalizePlan(model);
            await ValidatePlanAsync(model, id);
            var plan = await _dbContext.MembershipPlans.FirstOrDefaultAsync(x => x.Id == id);
            if (plan == null) return NotFound();
            if (!ModelState.IsValid) return View("PlanForm", await AddPlanOptionsAsync(model));
            MapPlan(model, plan);
            plan.UpdatedAt = DateTime.UtcNow;
            await _dbContext.SaveChangesAsync();
            TempData.SetToast("success", "Success", "Membership plan updated successfully.");
            return RedirectToAction(nameof(PlanDetails), new { id });
        }

        [HttpGet]
        public async Task<IActionResult> PlanDetails(int id)
        {
            var settings = await GetSettingsAsync();
            var plan = await _dbContext.MembershipPlans.AsNoTracking()
                .Include(x => x.CustomerMemberships).ThenInclude(x => x.Customer)
                .FirstOrDefaultAsync(x => x.Id == id);
            if (plan == null) return NotFound();
            var assigned = plan.CustomerMemberships.OrderByDescending(x => x.CreatedAt).Take(8).Select(MapMembershipList).ToList();
            return View(new MembershipPlanDetailsViewModel
            {
                Id = plan.Id, Name = plan.Name, Code = plan.Code, Description = plan.Description, PlanType = plan.PlanType,
                DurationMonths = plan.DurationMonths, JoiningFee = plan.JoiningFee, RenewalFee = plan.RenewalFee,
                DiscountPercentage = plan.DiscountPercentage, IncludedBookingHours = plan.IncludedBookingHours,
                PriorityBookingDays = plan.PriorityBookingDays, AllowPeakHours = plan.AllowPeakHours, AllowOffPeakHours = plan.AllowOffPeakHours,
                AllowedSportCodes = plan.AllowedSportCodes, Benefits = plan.Benefits, DisplayOrder = plan.DisplayOrder,
                IsActive = plan.IsActive, CreatedAt = plan.CreatedAt, UpdatedAt = plan.UpdatedAt, MemberCount = plan.CustomerMemberships.Count,
                AssignedMembers = assigned, CurrencySymbol = settings.CurrencySymbol ?? "PKR", ArenaName = settings.ArenaName ?? "GameHub Arena", CanManage = CanManage()
            });
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> TogglePlanStatus(int id)
        {
            if (EnsureCanManage() is IActionResult blocked) return blocked;
            var plan = await _dbContext.MembershipPlans.FindAsync(id);
            if (plan == null) return Json(new { success = false, message = "Membership plan not found." });
            plan.IsActive = !plan.IsActive;
            plan.UpdatedAt = DateTime.UtcNow;
            await _dbContext.SaveChangesAsync();
            return Json(new { success = true, isActive = plan.IsActive, message = plan.IsActive ? "Membership plan activated successfully." : "Membership plan deactivated successfully." });
        }

        [HttpGet]
        public async Task<IActionResult> Assign()
        {
            if (EnsureCanManage() is IActionResult blocked) return blocked;
            return View("MembershipForm", await AddMembershipOptionsAsync(new CustomerMembershipFormViewModel()));
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Assign(CustomerMembershipFormViewModel model)
        {
            if (EnsureCanManage() is IActionResult blocked) return blocked;
            await PrepareMembershipAsync(model);
            await ValidateMembershipAsync(model);
            if (!ModelState.IsValid) return View("MembershipForm", await AddMembershipOptionsAsync(model));
            var membership = new CustomerMembership { MembershipNumber = await _membershipNumberService.GenerateNextNumberAsync(), CreatedAt = DateTime.UtcNow };
            MapMembership(model, membership);
            _dbContext.CustomerMemberships.Add(membership);
            await _dbContext.SaveChangesAsync();
            await SyncCustomerSummaryAsync(membership.CustomerId);
            TempData.SetToast("success", "Success", "Membership assigned successfully.");
            return RedirectToAction(nameof(MembershipDetails), new { id = membership.Id });
        }

        [HttpGet]
        public async Task<IActionResult> EditMembership(int id)
        {
            if (EnsureCanManage() is IActionResult blocked) return blocked;
            var membership = await _dbContext.CustomerMemberships.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
            if (membership == null) return NotFound();
            return View("MembershipForm", await AddMembershipOptionsAsync(MapMembershipForm(membership)));
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> EditMembership(int id, CustomerMembershipFormViewModel model)
        {
            if (EnsureCanManage() is IActionResult blocked) return blocked;
            if (model.Id != id) return NotFound();
            await PrepareMembershipAsync(model, false);
            await ValidateMembershipAsync(model, id);
            var membership = await _dbContext.CustomerMemberships.FirstOrDefaultAsync(x => x.Id == id);
            if (membership == null) return NotFound();
            if (!ModelState.IsValid) return View("MembershipForm", await AddMembershipOptionsAsync(model));
            MapMembership(model, membership);
            membership.UpdatedAt = DateTime.UtcNow;
            await _dbContext.SaveChangesAsync();
            await SyncCustomerSummaryAsync(membership.CustomerId);
            TempData.SetToast("success", "Success", "Membership updated successfully.");
            return RedirectToAction(nameof(MembershipDetails), new { id });
        }

        [HttpGet]
        public async Task<IActionResult> MembershipDetails(int id)
        {
            await SyncExpiredMembershipsAsync();
            var settings = await GetSettingsAsync();
            var membership = await _dbContext.CustomerMemberships.AsNoTracking().Include(x => x.Customer).Include(x => x.MembershipPlan).FirstOrDefaultAsync(x => x.Id == id);
            if (membership == null) return NotFound();
            var model = MapMembershipDetails(membership);
            model.CurrencySymbol = settings.CurrencySymbol ?? "PKR";
            model.ArenaName = settings.ArenaName ?? "GameHub Arena";
            model.CanManage = CanManage();
            return View(model);
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Renew(int id)
        {
            if (EnsureCanManage() is IActionResult blocked) return blocked;
            var item = await _dbContext.CustomerMemberships.Include(x => x.MembershipPlan).FirstOrDefaultAsync(x => x.Id == id);
            if (item == null) return Json(new { success = false, message = "Membership not found." });
            item.ExpiryDate = (item.ExpiryDate < DateOnly.FromDateTime(DateTime.UtcNow) ? DateOnly.FromDateTime(DateTime.UtcNow) : item.ExpiryDate).AddMonths(item.MembershipPlan.DurationMonths);
            item.RenewalFee = item.MembershipPlan.RenewalFee;
            item.Status = MembershipStatus.Active;
            item.IsActive = true;
            item.UpdatedAt = DateTime.UtcNow;
            await _dbContext.SaveChangesAsync();
            await SyncCustomerSummaryAsync(item.CustomerId);
            return Json(new { success = true, message = "Membership renewed successfully." });
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Freeze(int id, DateOnly frozenFrom, DateOnly frozenUntil)
        {
            if (EnsureCanManage() is IActionResult blocked) return blocked;
            if (frozenUntil < frozenFrom) return Json(new { success = false, message = "Freeze end date must be after start date." });
            var item = await _dbContext.CustomerMemberships.FindAsync(id);
            if (item == null) return Json(new { success = false, message = "Membership not found." });
            item.FrozenFrom = frozenFrom; item.FrozenUntil = frozenUntil; item.Status = MembershipStatus.Frozen; item.UpdatedAt = DateTime.UtcNow;
            await _dbContext.SaveChangesAsync();
            await SyncCustomerSummaryAsync(item.CustomerId);
            return Json(new { success = true, message = "Membership frozen successfully." });
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Unfreeze(int id) => await SetMembershipStatusAsync(id, MembershipStatus.Active, true, "Membership unfrozen successfully.");

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Suspend(int id, string? reason) => await SetMembershipStatusAsync(id, MembershipStatus.Suspended, true, "Membership suspended successfully.", reason, requireReason: true);

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Reactivate(int id) => await SetMembershipStatusAsync(id, MembershipStatus.Active, true, "Membership reactivated successfully.");

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Cancel(int id, string? reason)
        {
            if (EnsureCanManage() is IActionResult blocked) return blocked;
            if (string.IsNullOrWhiteSpace(reason)) return Json(new { success = false, message = "Cancellation reason is required." });
            var item = await _dbContext.CustomerMemberships.FindAsync(id);
            if (item == null) return Json(new { success = false, message = "Membership not found." });
            item.Status = MembershipStatus.Cancelled; item.CancelledAt = DateOnly.FromDateTime(DateTime.UtcNow); item.CancellationReason = reason.Trim(); item.IsActive = false; item.UpdatedAt = DateTime.UtcNow;
            await _dbContext.SaveChangesAsync();
            await SyncCustomerSummaryAsync(item.CustomerId);
            return Json(new { success = true, message = "Membership cancelled successfully." });
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleMembershipStatus(int id)
        {
            if (EnsureCanManage() is IActionResult blocked) return blocked;
            var item = await _dbContext.CustomerMemberships.FindAsync(id);
            if (item == null) return Json(new { success = false, message = "Membership not found." });
            item.IsActive = !item.IsActive; item.UpdatedAt = DateTime.UtcNow;
            await _dbContext.SaveChangesAsync();
            await SyncCustomerSummaryAsync(item.CustomerId);
            return Json(new { success = true, isActive = item.IsActive, message = item.IsActive ? "Membership activated successfully." : "Membership deactivated successfully." });
        }

        private async Task<IActionResult> SetMembershipStatusAsync(int id, MembershipStatus status, bool isActive, string message, string? reason = null, bool requireReason = false)
        {
            if (EnsureCanManage() is IActionResult blocked) return blocked;
            if (requireReason && string.IsNullOrWhiteSpace(reason)) return Json(new { success = false, message = "Reason is required." });
            var item = await _dbContext.CustomerMemberships.FindAsync(id);
            if (item == null) return Json(new { success = false, message = "Membership not found." });
            if (status == MembershipStatus.Active && item.ExpiryDate < DateOnly.FromDateTime(DateTime.UtcNow)) return Json(new { success = false, message = "Expired membership cannot be reactivated without renewal." });
            item.Status = status; item.IsActive = isActive; item.Notes = string.IsNullOrWhiteSpace(reason) ? item.Notes : reason.Trim(); item.UpdatedAt = DateTime.UtcNow;
            if (status == MembershipStatus.Active) { item.FrozenFrom = null; item.FrozenUntil = null; }
            await _dbContext.SaveChangesAsync();
            await SyncCustomerSummaryAsync(item.CustomerId);
            return Json(new { success = true, message });
        }

        private async Task<MembershipIndexViewModel> BaseIndexModelAsync(string tab, string? search, string? statusFilter, string sortBy, int page, int pageSize, MembershipPlanType? planType, MembershipStatus? membershipStatus, int? planId, string? sportCode, string? expiryState)
        {
            var settings = await GetSettingsAsync();
            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            return new MembershipIndexViewModel
            {
                ActiveTab = tab, Search = search, StatusFilter = statusFilter, SortBy = sortBy, CurrentPage = page, PageSize = pageSize,
                PlanType = planType, MembershipStatus = membershipStatus, PlanId = planId, SportCode = sportCode, ExpiryState = expiryState,
                ActivePlans = await _dbContext.MembershipPlans.CountAsync(x => x.IsActive),
                ActiveMembers = await _dbContext.CustomerMemberships.CountAsync(x => x.Status == MembershipStatus.Active && x.IsActive),
                ExpiringSoon = await _dbContext.CustomerMemberships.CountAsync(x => x.ExpiryDate >= today && x.ExpiryDate <= today.AddDays(30) && x.IsActive),
                FrozenMemberships = await _dbContext.CustomerMemberships.CountAsync(x => x.Status == MembershipStatus.Frozen),
                ExpiredMemberships = await _dbContext.CustomerMemberships.CountAsync(x => x.Status == MembershipStatus.Expired || x.ExpiryDate < today),
                EstimatedValue = await _dbContext.CustomerMemberships.Where(x => x.IsActive).SumAsync(x => x.JoiningFee + x.RenewalFee),
                CurrencySymbol = settings.CurrencySymbol ?? "PKR", CanManage = CanManage(),
                PlanOptions = await _dbContext.MembershipPlans.AsNoTracking().OrderBy(x => x.Name).Select(x => new SelectListItem(x.Name, x.Id.ToString())).ToListAsync(),
                SportOptions = await _dbContext.Sports.AsNoTracking().Where(x => x.IsActive).OrderBy(x => x.DisplayOrder).Select(x => new SelectListItem(x.Name, x.Code ?? string.Empty)).ToListAsync()
            };
        }

        private async Task LoadPlansAsync(MembershipIndexViewModel model)
        {
            var query = _dbContext.MembershipPlans.AsNoTracking().Select(x => new MembershipPlanListItemViewModel { Id = x.Id, Name = x.Name, Code = x.Code, PlanType = x.PlanType, DurationMonths = x.DurationMonths, JoiningFee = x.JoiningFee, RenewalFee = x.RenewalFee, DiscountPercentage = x.DiscountPercentage, IncludedBookingHours = x.IncludedBookingHours, AllowedSportCodes = x.AllowedSportCodes, IsActive = x.IsActive, MemberCount = x.CustomerMemberships.Count });
            if (!string.IsNullOrWhiteSpace(model.Search)) query = query.Where(x => x.Name.ToLower().Contains(model.Search.ToLower()) || x.Code.ToLower().Contains(model.Search.ToLower()));
            if (model.PlanType.HasValue) query = query.Where(x => x.PlanType == model.PlanType);
            if (model.StatusFilter == "active") query = query.Where(x => x.IsActive);
            if (model.StatusFilter == "inactive") query = query.Where(x => !x.IsActive);
            if (!string.IsNullOrWhiteSpace(model.SportCode)) query = query.Where(x => x.AllowedSportCodes == "ALL" || (x.AllowedSportCodes != null && x.AllowedSportCodes.Contains(model.SportCode)));
            query = model.SortBy switch { "name" => query.OrderBy(x => x.Name), "fee" => query.OrderByDescending(x => x.JoiningFee), "members" => query.OrderByDescending(x => x.MemberCount), _ => query.OrderBy(x => x.Code) };
            await PagePlansAsync(model, query);
        }

        private async Task PagePlansAsync(MembershipIndexViewModel model, IQueryable<MembershipPlanListItemViewModel> query)
        {
            model.TotalRecords = await query.CountAsync(); model.TotalPages = model.TotalRecords == 0 ? 1 : (int)Math.Ceiling(model.TotalRecords / (double)model.PageSize); model.CurrentPage = Math.Min(model.CurrentPage, model.TotalPages);
            model.Plans = await query.Skip((model.CurrentPage - 1) * model.PageSize).Take(model.PageSize).ToListAsync();
        }

        private async Task LoadCustomerMembershipsAsync(MembershipIndexViewModel model)
        {
            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            var query = _dbContext.CustomerMemberships.AsNoTracking().Include(x => x.Customer).Include(x => x.MembershipPlan).AsQueryable();
            if (!string.IsNullOrWhiteSpace(model.Search))
            {
                var search = model.Search.ToLower();
                query = query.Where(x => x.MembershipNumber.ToLower().Contains(search) || x.Customer.FirstName.ToLower().Contains(search) || x.Customer.LastName.ToLower().Contains(search) || x.Customer.PrimaryPhone.Contains(search) || (x.Customer.Email != null && x.Customer.Email.ToLower().Contains(search)));
            }
            if (model.PlanId.HasValue) query = query.Where(x => x.MembershipPlanId == model.PlanId);
            if (model.MembershipStatus.HasValue) query = query.Where(x => x.Status == model.MembershipStatus);
            if (model.StatusFilter == "active") query = query.Where(x => x.IsActive);
            if (model.StatusFilter == "inactive") query = query.Where(x => !x.IsActive);
            if (model.ExpiryState == "soon") query = query.Where(x => x.ExpiryDate >= today && x.ExpiryDate <= today.AddDays(30));
            if (model.ExpiryState == "expired") query = query.Where(x => x.ExpiryDate < today);
            query = model.SortBy switch { "customer" => query.OrderBy(x => x.Customer.FirstName), "expiry" => query.OrderBy(x => x.ExpiryDate), "status" => query.OrderBy(x => x.Status), _ => query.OrderByDescending(x => x.CreatedAt) };
            model.TotalRecords = await query.CountAsync(); model.TotalPages = model.TotalRecords == 0 ? 1 : (int)Math.Ceiling(model.TotalRecords / (double)model.PageSize); model.CurrentPage = Math.Min(model.CurrentPage, model.TotalPages);
            var rows = await query.Skip((model.CurrentPage - 1) * model.PageSize).Take(model.PageSize).ToListAsync();
            model.CustomerMemberships = rows.Select(MapMembershipList).ToList();
        }

        private static CustomerMembershipListItemViewModel MapMembershipList(CustomerMembership x)
        {
            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            return new CustomerMembershipListItemViewModel { Id = x.Id, CustomerId = x.CustomerId, CustomerName = x.Customer.FirstName + " " + x.Customer.LastName, CustomerInitials = x.Customer.Initials, CustomerPhone = x.Customer.PrimaryPhone, MembershipNumber = x.MembershipNumber, PlanName = x.MembershipPlan.Name, StartDate = x.StartDate, ExpiryDate = x.ExpiryDate, RemainingDays = x.ExpiryDate.DayNumber - today.DayNumber, IncludedBookingHours = x.IncludedBookingHours, UsedBookingHours = x.UsedBookingHours, DiscountPercentage = x.DiscountPercentage, Status = x.ExpiryDate < today && x.Status == MembershipStatus.Active ? MembershipStatus.Expired : x.Status, AutoRenew = x.AutoRenew, IsActive = x.IsActive };
        }

        private CustomerMembershipDetailsViewModel MapMembershipDetails(CustomerMembership x)
        {
            var list = MapMembershipList(x);
            return new CustomerMembershipDetailsViewModel { Id = list.Id, CustomerId = list.CustomerId, CustomerName = list.CustomerName, CustomerInitials = list.CustomerInitials, CustomerPhone = list.CustomerPhone, MembershipNumber = list.MembershipNumber, PlanName = list.PlanName, StartDate = list.StartDate, ExpiryDate = list.ExpiryDate, RemainingDays = list.RemainingDays, IncludedBookingHours = list.IncludedBookingHours, UsedBookingHours = list.UsedBookingHours, DiscountPercentage = list.DiscountPercentage, Status = list.Status, AutoRenew = list.AutoRenew, IsActive = list.IsActive, CustomerCode = x.Customer.CustomerCode, CustomerEmail = x.Customer.Email, PlanCode = x.MembershipPlan.Code, PlanType = x.MembershipPlan.PlanType, JoiningFee = x.JoiningFee, RenewalFee = x.RenewalFee, AllowedSportCodes = x.MembershipPlan.AllowedSportCodes, Benefits = x.MembershipPlan.Benefits, FrozenFrom = x.FrozenFrom, FrozenUntil = x.FrozenUntil, CancelledAt = x.CancelledAt, CancellationReason = x.CancellationReason, Notes = x.Notes, CreatedAt = x.CreatedAt, UpdatedAt = x.UpdatedAt };
        }

        private async Task ValidatePlanAsync(MembershipPlanFormViewModel model, int? id = null)
        {
            if (await _dbContext.MembershipPlans.AnyAsync(x => x.Code == model.Code && (!id.HasValue || x.Id != id.Value))) ModelState.AddModelError(nameof(model.Code), "Plan code already exists.");
            var settings = await GetSettingsAsync();
            if (model.PriorityBookingDays > settings.AdvanceBookingDays && !User.IsInRole(UserRole.SuperAdmin.ToString())) ModelState.AddModelError(nameof(model.PriorityBookingDays), "Priority booking days cannot exceed system advance booking days.");
        }

        private async Task ValidateMembershipAsync(CustomerMembershipFormViewModel model, int? id = null)
        {
            var customer = await _dbContext.Customers.AsNoTracking().FirstOrDefaultAsync(x => x.Id == model.CustomerId);
            if (customer == null) ModelState.AddModelError(nameof(model.CustomerId), "Customer is required.");
            else if (!customer.IsActive) ModelState.AddModelError(nameof(model.CustomerId), "Inactive customers cannot receive memberships.");
            else if (customer.IsBlacklisted) ModelState.AddModelError(nameof(model.CustomerId), "Blacklisted customers cannot receive memberships.");
            if (await _dbContext.CustomerMemberships.AnyAsync(x => x.CustomerId == model.CustomerId && x.IsActive && x.Status != MembershipStatus.Cancelled && x.Status != MembershipStatus.Expired && x.ExpiryDate >= model.StartDate && x.StartDate <= model.ExpiryDate && (!id.HasValue || x.Id != id.Value))) ModelState.AddModelError(string.Empty, "Customer already has an overlapping active membership.");
        }

        private async Task PrepareMembershipAsync(CustomerMembershipFormViewModel model, bool fromPlan = true)
        {
            model.Notes = NormalizeOptional(model.Notes); model.CancellationReason = NormalizeOptional(model.CancellationReason);
            if (fromPlan)
            {
                var plan = await _dbContext.MembershipPlans.AsNoTracking().FirstOrDefaultAsync(x => x.Id == model.MembershipPlanId);
                if (plan != null)
                {
                    model.ExpiryDate = model.StartDate.AddMonths(plan.DurationMonths); model.JoiningFee = plan.JoiningFee; model.RenewalFee = plan.RenewalFee; model.DiscountPercentage = plan.DiscountPercentage; model.IncludedBookingHours = plan.IncludedBookingHours;
                }
            }
        }

        private void MapPlan(MembershipPlanFormViewModel model, MembershipPlan plan) { plan.Name = model.Name.Trim(); plan.Code = model.Code.Trim().ToUpperInvariant(); plan.Description = NormalizeOptional(model.Description); plan.PlanType = model.PlanType; plan.DurationMonths = model.DurationMonths; plan.JoiningFee = model.JoiningFee; plan.RenewalFee = model.RenewalFee; plan.DiscountPercentage = model.DiscountPercentage; plan.IncludedBookingHours = model.IncludedBookingHours; plan.PriorityBookingDays = model.PriorityBookingDays; plan.AllowPeakHours = model.AllowPeakHours; plan.AllowOffPeakHours = model.AllowOffPeakHours; plan.AllowedSportCodes = model.AllSports ? "ALL" : string.Join(",", model.SelectedSportCodes.Select(x => x.Trim().ToUpperInvariant()).Distinct()); plan.Benefits = NormalizeOptional(model.Benefits); plan.DisplayOrder = model.DisplayOrder; plan.IsActive = model.IsActive; }
        private MembershipPlanFormViewModel MapPlanForm(MembershipPlan plan) => new() { Id = plan.Id, Name = plan.Name, Code = plan.Code, Description = plan.Description, PlanType = plan.PlanType, DurationMonths = plan.DurationMonths, JoiningFee = plan.JoiningFee, RenewalFee = plan.RenewalFee, DiscountPercentage = plan.DiscountPercentage, IncludedBookingHours = plan.IncludedBookingHours, PriorityBookingDays = plan.PriorityBookingDays, AllowPeakHours = plan.AllowPeakHours, AllowOffPeakHours = plan.AllowOffPeakHours, AllSports = plan.AllowedSportCodes == "ALL", SelectedSportCodes = SplitCsv(plan.AllowedSportCodes == "ALL" ? null : plan.AllowedSportCodes), Benefits = plan.Benefits, DisplayOrder = plan.DisplayOrder, IsActive = plan.IsActive };
        private void MapMembership(CustomerMembershipFormViewModel model, CustomerMembership item) { item.CustomerId = model.CustomerId; item.MembershipPlanId = model.MembershipPlanId; item.StartDate = model.StartDate; item.ExpiryDate = model.ExpiryDate; item.Status = model.Status; item.JoiningFee = model.JoiningFee; item.RenewalFee = model.RenewalFee; item.DiscountPercentage = model.DiscountPercentage; item.IncludedBookingHours = model.IncludedBookingHours; item.UsedBookingHours = model.UsedBookingHours; item.FrozenFrom = model.FrozenFrom; item.FrozenUntil = model.FrozenUntil; item.CancellationReason = NormalizeOptional(model.CancellationReason); item.Notes = NormalizeOptional(model.Notes); item.AutoRenew = model.AutoRenew; item.IsActive = model.IsActive; }
        private CustomerMembershipFormViewModel MapMembershipForm(CustomerMembership item) => new() { Id = item.Id, MembershipNumber = item.MembershipNumber, CustomerId = item.CustomerId, MembershipPlanId = item.MembershipPlanId, StartDate = item.StartDate, ExpiryDate = item.ExpiryDate, Status = item.Status, JoiningFee = item.JoiningFee, RenewalFee = item.RenewalFee, DiscountPercentage = item.DiscountPercentage, IncludedBookingHours = item.IncludedBookingHours, UsedBookingHours = item.UsedBookingHours, FrozenFrom = item.FrozenFrom, FrozenUntil = item.FrozenUntil, CancellationReason = item.CancellationReason, Notes = item.Notes, AutoRenew = item.AutoRenew, IsActive = item.IsActive };
        private void NormalizePlan(MembershipPlanFormViewModel model) { model.Name = model.Name.Trim(); model.Code = model.Code.Trim().ToUpperInvariant(); model.Description = NormalizeOptional(model.Description); model.Benefits = NormalizeOptional(model.Benefits); model.SelectedSportCodes = model.SelectedSportCodes.Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x.Trim().ToUpperInvariant()).Distinct().ToList(); }

        private async Task<MembershipPlanFormViewModel> AddPlanOptionsAsync(MembershipPlanFormViewModel model) { var settings = await GetSettingsAsync(); model.CurrencySymbol = settings.CurrencySymbol ?? "PKR"; model.ArenaName = settings.ArenaName ?? "GameHub Arena"; model.AvailableSports = await _dbContext.Sports.AsNoTracking().Where(x => x.IsActive).OrderBy(x => x.DisplayOrder).Select(x => new SelectListItem(x.Name, x.Code ?? string.Empty, model.SelectedSportCodes.Contains(x.Code ?? string.Empty))).ToListAsync(); return model; }
        private async Task<CustomerMembershipFormViewModel> AddMembershipOptionsAsync(CustomerMembershipFormViewModel model) { var settings = await GetSettingsAsync(); model.CurrencySymbol = settings.CurrencySymbol ?? "PKR"; model.Customers = await _dbContext.Customers.AsNoTracking().OrderBy(x => x.FirstName).Select(x => new SelectListItem(x.FirstName + " " + x.LastName + " - " + x.PrimaryPhone, x.Id.ToString(), x.Id == model.CustomerId)).ToListAsync(); model.Plans = await _dbContext.MembershipPlans.AsNoTracking().Where(x => x.IsActive || x.Id == model.MembershipPlanId).OrderBy(x => x.DisplayOrder).Select(x => new SelectListItem(x.Name, x.Id.ToString(), x.Id == model.MembershipPlanId)).ToListAsync(); return model; }
        private async Task<SystemSettings> GetSettingsAsync() => await _dbContext.SystemSettings.AsNoTracking().FirstOrDefaultAsync() ?? new SystemSettings { ArenaName = "GameHub Arena", CurrencySymbol = "PKR", AdvanceBookingDays = 14 };
        private async Task SyncExpiredMembershipsAsync() { var today = DateOnly.FromDateTime(DateTime.UtcNow); var expired = await _dbContext.CustomerMemberships.Where(x => x.IsActive && x.Status == MembershipStatus.Active && x.ExpiryDate < today).ToListAsync(); foreach (var item in expired) { item.Status = MembershipStatus.Expired; item.IsActive = false; item.UpdatedAt = DateTime.UtcNow; } if (expired.Any()) { await _dbContext.SaveChangesAsync(); foreach (var customerId in expired.Select(x => x.CustomerId).Distinct()) await SyncCustomerSummaryAsync(customerId); } }
        private async Task SyncCustomerSummaryAsync(int customerId) { var customer = await _dbContext.Customers.FindAsync(customerId); if (customer == null) return; var active = await _dbContext.CustomerMemberships.Where(x => x.CustomerId == customerId && x.IsActive && x.Status == MembershipStatus.Active && x.ExpiryDate >= DateOnly.FromDateTime(DateTime.UtcNow)).OrderByDescending(x => x.ExpiryDate).FirstOrDefaultAsync(); customer.IsMember = active != null; customer.MembershipNumber = active?.MembershipNumber; customer.MembershipStartDate = active?.StartDate; customer.MembershipExpiryDate = active?.ExpiryDate; customer.UpdatedAt = DateTime.UtcNow; await _dbContext.SaveChangesAsync(); }
        private IActionResult? EnsureCanManage() => CanManage() ? null : RedirectToAction("NotFound", "Error");
        private bool CanManage() => RolePermissions.TryGetCurrentRole(User, out var role) && role is UserRole.SuperAdmin or UserRole.Admin or UserRole.BookingManager or UserRole.Receptionist or UserRole.FinanceManager;
        private static string? NormalizeOptional(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        private static List<string> SplitCsv(string? value) => string.IsNullOrWhiteSpace(value) ? new List<string>() : value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
    }
}
