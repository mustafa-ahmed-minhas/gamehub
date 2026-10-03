using GameHub.Data;
using GameHub.Filters;
using GameHub.Helpers;
using GameHub.Models.Entities;
using GameHub.Models.Enums;
using GameHub.Models.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace GameHub.Controllers
{
    [GameHubAuthorize(UserRole.SuperAdmin, UserRole.Admin, UserRole.CourtManager, UserRole.Viewer)]
    [ValidateActiveUser]
    public class ArenaSetupController : Controller
    {
        private static readonly int[] PageSizes = { 10, 25, 50 };
        private readonly ApplicationDbContext _dbContext;

        public ArenaSetupController(ApplicationDbContext dbContext) => _dbContext = dbContext;

        [HttpGet]
        public async Task<IActionResult> Index(string tab = "sports", string? search = null, string? statusFilter = null, string? sortBy = null, int page = 1, int pageSize = 10, int? sportId = null, int? facilityId = null, int? courtId = null, string? day = null, string? type = null, string? operationalStatus = null)
        {
            tab = NormalizeTab(tab);
            page = Math.Max(page, 1);
            pageSize = PageSizes.Contains(pageSize) ? pageSize : 10;
            var model = await BaseModelAsync(tab, search?.Trim(), statusFilter, sortBy, page, pageSize, sportId, facilityId, courtId, day, type, operationalStatus);

            switch (tab)
            {
                case "facilities":
                    await LoadFacilitiesAsync(model);
                    break;
                case "courts":
                    await LoadCourtsAsync(model);
                    break;
                case "pricing":
                    await LoadPricingAsync(model);
                    break;
                case "schedules":
                    await LoadSchedulesAsync(model);
                    break;
                default:
                    await LoadSportsAsync(model);
                    break;
            }

            return View(model);
        }

        [HttpGet]
        public IActionResult CreateSport() => EnsureCanManage() ?? View("SportForm", new SportFormViewModel());

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateSport(SportFormViewModel model)
        {
            if (EnsureCanManage() is IActionResult blocked) return blocked;
            NormalizeSport(model);
            if (await _dbContext.Sports.AnyAsync(x => x.Code == model.Code))
            {
                ModelState.AddModelError(nameof(model.Code), "Sport code already exists.");
                ModelState.AddModelError(string.Empty, "Sport code already exists.");
            }
            if (!ModelState.IsValid) return View("SportForm", model);
            _dbContext.Sports.Add(new Sport { Name = model.Name, Code = model.Code, Description = model.Description, IconClass = model.IconClass, ImageUrl = model.ImageUrl, AccentColor = model.AccentColor, DefaultDurationMinutes = model.DefaultDurationMinutes, DisplayOrder = model.DisplayOrder, IsActive = model.IsActive, CreatedAt = DateTime.UtcNow });
            await _dbContext.SaveChangesAsync();
            TempData.SetToast("success", "Success", "Sport created successfully.");
            return RedirectToAction(nameof(Index), new { tab = "sports" });
        }

        [HttpGet]
        public async Task<IActionResult> EditSport(int id)
        {
            if (EnsureCanManage() is IActionResult blocked) return blocked;
            var item = await _dbContext.Sports.FindAsync(id);
            if (item == null) return NotFound();
            return View("SportForm", new SportFormViewModel { Id = item.Id, Name = item.Name, Code = item.Code, Description = item.Description, IconClass = item.IconClass, ImageUrl = item.ImageUrl, AccentColor = item.AccentColor, DefaultDurationMinutes = item.DefaultDurationMinutes, DisplayOrder = item.DisplayOrder, IsActive = item.IsActive });
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> EditSport(int id, SportFormViewModel model)
        {
            if (EnsureCanManage() is IActionResult blocked) return blocked;
            if (model.Id != id) return NotFound();
            NormalizeSport(model);
            if (await _dbContext.Sports.AnyAsync(x => x.Code == model.Code && x.Id != id))
            {
                ModelState.AddModelError(nameof(model.Code), "Sport code already exists.");
                ModelState.AddModelError(string.Empty, "Sport code already exists.");
            }
            var item = await _dbContext.Sports.FindAsync(id);
            if (item == null) return NotFound();
            if (item.IsActive && !model.IsActive && await _dbContext.Courts.AnyAsync(x => x.SportId == id && x.IsActive)) ModelState.AddModelError(string.Empty, "Cannot deactivate a sport while active courts depend on it.");
            if (!ModelState.IsValid) return View("SportForm", model);
            item.Name = model.Name; item.Code = model.Code; item.Description = model.Description; item.IconClass = model.IconClass; item.ImageUrl = model.ImageUrl; item.AccentColor = model.AccentColor; item.DefaultDurationMinutes = model.DefaultDurationMinutes; item.DisplayOrder = model.DisplayOrder; item.IsActive = model.IsActive; item.UpdatedAt = DateTime.UtcNow;
            await _dbContext.SaveChangesAsync();
            TempData.SetToast("success", "Success", "Sport updated successfully.");
            return RedirectToAction(nameof(SportDetails), new { id });
        }

        [HttpGet]
        public async Task<IActionResult> SportDetails(int id)
        {
            var item = await _dbContext.Sports.AsNoTracking().Include(x => x.Courts).FirstOrDefaultAsync(x => x.Id == id);
            return item == null ? NotFound() : View("SportDetails", item);
        }

        [HttpGet]
        public IActionResult CreateFacility() => EnsureCanManage() ?? View("FacilityForm", new FacilityFormViewModel());

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateFacility(FacilityFormViewModel model)
        {
            if (EnsureCanManage() is IActionResult blocked) return blocked;
            NormalizeFacility(model);
            if (await _dbContext.Facilities.AnyAsync(x => x.Code == model.Code))
            {
                ModelState.AddModelError(nameof(model.Code), "Facility code already exists.");
                ModelState.AddModelError(string.Empty, "Facility code already exists.");
            }
            if (!ModelState.IsValid) return View("FacilityForm", model);
            _dbContext.Facilities.Add(new Facility { Name = model.Name, Code = model.Code, Description = model.Description, Type = model.Type, LocationLabel = model.LocationLabel, DisplayOrder = model.DisplayOrder, IsActive = model.IsActive, CreatedAt = DateTime.UtcNow });
            await _dbContext.SaveChangesAsync();
            TempData.SetToast("success", "Success", "Facility created successfully.");
            return RedirectToAction(nameof(Index), new { tab = "facilities" });
        }

        [HttpGet]
        public async Task<IActionResult> EditFacility(int id)
        {
            if (EnsureCanManage() is IActionResult blocked) return blocked;
            var item = await _dbContext.Facilities.FindAsync(id);
            if (item == null) return NotFound();
            return View("FacilityForm", new FacilityFormViewModel { Id = item.Id, Name = item.Name, Code = item.Code, Description = item.Description, Type = item.Type, LocationLabel = item.LocationLabel, DisplayOrder = item.DisplayOrder, IsActive = item.IsActive });
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> EditFacility(int id, FacilityFormViewModel model)
        {
            if (EnsureCanManage() is IActionResult blocked) return blocked;
            if (model.Id != id) return NotFound();
            NormalizeFacility(model);
            if (await _dbContext.Facilities.AnyAsync(x => x.Code == model.Code && x.Id != id))
            {
                ModelState.AddModelError(nameof(model.Code), "Facility code already exists.");
                ModelState.AddModelError(string.Empty, "Facility code already exists.");
            }
            var item = await _dbContext.Facilities.FindAsync(id);
            if (item == null) return NotFound();
            if (item.IsActive && !model.IsActive && await _dbContext.Courts.AnyAsync(x => x.FacilityId == id && x.IsActive)) ModelState.AddModelError(string.Empty, "Cannot deactivate a facility while active courts depend on it.");
            if (!ModelState.IsValid) return View("FacilityForm", model);
            item.Name = model.Name; item.Code = model.Code; item.Description = model.Description; item.Type = model.Type; item.LocationLabel = model.LocationLabel; item.DisplayOrder = model.DisplayOrder; item.IsActive = model.IsActive; item.UpdatedAt = DateTime.UtcNow;
            await _dbContext.SaveChangesAsync();
            TempData.SetToast("success", "Success", "Facility updated successfully.");
            return RedirectToAction(nameof(FacilityDetails), new { id });
        }

        [HttpGet]
        public async Task<IActionResult> FacilityDetails(int id)
        {
            var item = await _dbContext.Facilities.AsNoTracking().Include(x => x.Courts).FirstOrDefaultAsync(x => x.Id == id);
            return item == null ? NotFound() : View("FacilityDetails", item);
        }

        [HttpGet]
        public async Task<IActionResult> CreateCourt()
        {
            if (EnsureCanManage() is IActionResult blocked) return blocked;
            return View("CourtForm", await AddCourtOptionsAsync(new CourtFormViewModel()));
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateCourt(CourtFormViewModel model)
        {
            if (EnsureCanManage() is IActionResult blocked) return blocked;
            NormalizeCourt(model);
            if (await _dbContext.Courts.AnyAsync(x => x.Code == model.Code))
            {
                ModelState.AddModelError(nameof(model.Code), "Court code already exists.");
                ModelState.AddModelError(string.Empty, "Court code already exists.");
            }
            if (!ModelState.IsValid) return View("CourtForm", await AddCourtOptionsAsync(model));
            _dbContext.Courts.Add(new Court { Name = model.Name, Code = model.Code, SportId = model.SportId, FacilityId = model.FacilityId, Status = model.Status, Description = model.Description, Capacity = model.Capacity, ImageUrl = model.ImageUrl, Notes = model.Notes, DisplayOrder = model.DisplayOrder, IsActive = model.IsActive, CreatedAt = DateTime.UtcNow });
            await _dbContext.SaveChangesAsync();
            TempData.SetToast("success", "Success", "Court created successfully.");
            return RedirectToAction(nameof(Index), new { tab = "courts" });
        }

        [HttpGet]
        public async Task<IActionResult> EditCourt(int id)
        {
            if (EnsureCanManage() is IActionResult blocked) return blocked;
            var item = await _dbContext.Courts.FindAsync(id);
            if (item == null) return NotFound();
            return View("CourtForm", await AddCourtOptionsAsync(new CourtFormViewModel { Id = item.Id, Name = item.Name, Code = item.Code, SportId = item.SportId, FacilityId = item.FacilityId, Status = item.Status, Description = item.Description, Capacity = item.Capacity, ImageUrl = item.ImageUrl, Notes = item.Notes, DisplayOrder = item.DisplayOrder, IsActive = item.IsActive }));
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> EditCourt(int id, CourtFormViewModel model)
        {
            if (EnsureCanManage() is IActionResult blocked) return blocked;
            if (model.Id != id) return NotFound();
            NormalizeCourt(model);
            if (await _dbContext.Courts.AnyAsync(x => x.Code == model.Code && x.Id != id))
            {
                ModelState.AddModelError(nameof(model.Code), "Court code already exists.");
                ModelState.AddModelError(string.Empty, "Court code already exists.");
            }
            var item = await _dbContext.Courts.FindAsync(id);
            if (item == null) return NotFound();
            if (!ModelState.IsValid) return View("CourtForm", await AddCourtOptionsAsync(model));
            item.Name = model.Name; item.Code = model.Code; item.SportId = model.SportId; item.FacilityId = model.FacilityId; item.Status = model.Status; item.Description = model.Description; item.Capacity = model.Capacity; item.ImageUrl = model.ImageUrl; item.Notes = model.Notes; item.DisplayOrder = model.DisplayOrder; item.IsActive = model.IsActive; item.UpdatedAt = DateTime.UtcNow;
            await _dbContext.SaveChangesAsync();
            TempData.SetToast("success", "Success", "Court updated successfully.");
            return RedirectToAction(nameof(CourtDetails), new { id });
        }

        [HttpGet]
        public async Task<IActionResult> CourtDetails(int id)
        {
            var item = await _dbContext.Courts.AsNoTracking().Include(x => x.Sport).Include(x => x.Facility).Include(x => x.Pricings).Include(x => x.Schedules).FirstOrDefaultAsync(x => x.Id == id);
            return item == null ? NotFound() : View("CourtDetails", item);
        }

        [HttpGet] public async Task<IActionResult> CreatePricing() => EnsureCanManage() ?? View("PricingForm", await AddCourtOptionsAsync(new CourtPricingFormViewModel()));

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> CreatePricing(CourtPricingFormViewModel model)
        {
            if (EnsureCanManage() is IActionResult blocked) return blocked;
            NormalizePricing(model);
            if (await PricingDuplicateAsync(model)) ModelState.AddModelError(string.Empty, "Duplicate pricing rule already exists for this court and time range.");
            if (!ModelState.IsValid) return View("PricingForm", await AddCourtOptionsAsync(model));
            _dbContext.CourtPricings.Add(new CourtPricing { CourtId = model.CourtId, Name = model.Name, DayOfWeek = model.DayOfWeek, StartTime = model.StartTime, EndTime = model.EndTime, DurationMinutes = model.DurationMinutes, Price = model.Price, IsPeakRate = model.IsPeakRate, IsActive = model.IsActive, CreatedAt = DateTime.UtcNow });
            await _dbContext.SaveChangesAsync();
            TempData.SetToast("success", "Success", "Pricing rule created successfully.");
            return RedirectToAction(nameof(Index), new { tab = "pricing" });
        }

        [HttpGet]
        public async Task<IActionResult> EditPricing(int id)
        {
            if (EnsureCanManage() is IActionResult blocked) return blocked;
            var item = await _dbContext.CourtPricings.FindAsync(id);
            if (item == null) return NotFound();
            return View("PricingForm", await AddCourtOptionsAsync(new CourtPricingFormViewModel { Id = item.Id, CourtId = item.CourtId, Name = item.Name, DayOfWeek = item.DayOfWeek, StartTime = item.StartTime, EndTime = item.EndTime, DurationMinutes = item.DurationMinutes, Price = item.Price, IsPeakRate = item.IsPeakRate, IsActive = item.IsActive }));
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> EditPricing(int id, CourtPricingFormViewModel model)
        {
            if (EnsureCanManage() is IActionResult blocked) return blocked;
            if (model.Id != id) return NotFound();
            NormalizePricing(model);
            if (await PricingDuplicateAsync(model, id)) ModelState.AddModelError(string.Empty, "Duplicate pricing rule already exists for this court and time range.");
            var item = await _dbContext.CourtPricings.FindAsync(id);
            if (item == null) return NotFound();
            if (!ModelState.IsValid) return View("PricingForm", await AddCourtOptionsAsync(model));
            item.CourtId = model.CourtId; item.Name = model.Name; item.DayOfWeek = model.DayOfWeek; item.StartTime = model.StartTime; item.EndTime = model.EndTime; item.DurationMinutes = model.DurationMinutes; item.Price = model.Price; item.IsPeakRate = model.IsPeakRate; item.IsActive = model.IsActive; item.UpdatedAt = DateTime.UtcNow;
            await _dbContext.SaveChangesAsync();
            TempData.SetToast("success", "Success", "Pricing rule updated successfully.");
            return RedirectToAction(nameof(PricingDetails), new { id });
        }

        [HttpGet]
        public async Task<IActionResult> PricingDetails(int id)
        {
            var item = await _dbContext.CourtPricings.AsNoTracking().Include(x => x.Court).ThenInclude(x => x.Sport).FirstOrDefaultAsync(x => x.Id == id);
            return item == null ? NotFound() : View("PricingDetails", item);
        }

        [HttpGet] public async Task<IActionResult> CreateSchedule() => EnsureCanManage() ?? View("ScheduleForm", await AddCourtOptionsAsync(new CourtScheduleFormViewModel()));

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateSchedule(CourtScheduleFormViewModel model)
        {
            if (EnsureCanManage() is IActionResult blocked) return blocked;
            if (await ScheduleDuplicateAsync(model)) ModelState.AddModelError(string.Empty, "One active schedule already exists for this court and day.");
            if (!ModelState.IsValid) return View("ScheduleForm", await AddCourtOptionsAsync(model));
            _dbContext.CourtSchedules.Add(new CourtSchedule { CourtId = model.CourtId, DayOfWeek = model.DayOfWeek, OpeningTime = model.OpeningTime, ClosingTime = model.ClosingTime, IsClosed = model.IsClosed, SlotDurationMinutes = model.SlotDurationMinutes, BufferMinutes = model.BufferMinutes, IsActive = model.IsActive, CreatedAt = DateTime.UtcNow });
            await _dbContext.SaveChangesAsync();
            TempData.SetToast("success", "Success", "Schedule created successfully.");
            return RedirectToAction(nameof(Index), new { tab = "schedules" });
        }

        [HttpGet]
        public async Task<IActionResult> EditSchedule(int id)
        {
            if (EnsureCanManage() is IActionResult blocked) return blocked;
            var item = await _dbContext.CourtSchedules.FindAsync(id);
            if (item == null) return NotFound();
            return View("ScheduleForm", await AddCourtOptionsAsync(new CourtScheduleFormViewModel { Id = item.Id, CourtId = item.CourtId, DayOfWeek = item.DayOfWeek, OpeningTime = item.OpeningTime, ClosingTime = item.ClosingTime, IsClosed = item.IsClosed, SlotDurationMinutes = item.SlotDurationMinutes, BufferMinutes = item.BufferMinutes, IsActive = item.IsActive }));
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> EditSchedule(int id, CourtScheduleFormViewModel model)
        {
            if (EnsureCanManage() is IActionResult blocked) return blocked;
            if (model.Id != id) return NotFound();
            if (await ScheduleDuplicateAsync(model, id)) ModelState.AddModelError(string.Empty, "One active schedule already exists for this court and day.");
            var item = await _dbContext.CourtSchedules.FindAsync(id);
            if (item == null) return NotFound();
            if (!ModelState.IsValid) return View("ScheduleForm", await AddCourtOptionsAsync(model));
            item.CourtId = model.CourtId; item.DayOfWeek = model.DayOfWeek; item.OpeningTime = model.OpeningTime; item.ClosingTime = model.ClosingTime; item.IsClosed = model.IsClosed; item.SlotDurationMinutes = model.SlotDurationMinutes; item.BufferMinutes = model.BufferMinutes; item.IsActive = model.IsActive; item.UpdatedAt = DateTime.UtcNow;
            await _dbContext.SaveChangesAsync();
            TempData.SetToast("success", "Success", "Schedule updated successfully.");
            return RedirectToAction(nameof(ScheduleDetails), new { id });
        }

        [HttpGet]
        public async Task<IActionResult> ScheduleDetails(int id)
        {
            var item = await _dbContext.CourtSchedules.AsNoTracking().Include(x => x.Court).ThenInclude(x => x.Sport).FirstOrDefaultAsync(x => x.Id == id);
            return item == null ? NotFound() : View("ScheduleDetails", item);
        }

        [HttpPost, ValidateAntiForgeryToken] public Task<IActionResult> ToggleSportStatus(int id) => ToggleAsync(_dbContext.Sports, id, "Sport");
        [HttpPost, ValidateAntiForgeryToken] public Task<IActionResult> ToggleFacilityStatus(int id) => ToggleAsync(_dbContext.Facilities, id, "Facility");
        [HttpPost, ValidateAntiForgeryToken] public Task<IActionResult> ToggleCourtStatus(int id) => ToggleAsync(_dbContext.Courts, id, "Court");
        [HttpPost, ValidateAntiForgeryToken] public Task<IActionResult> TogglePricingStatus(int id) => ToggleAsync(_dbContext.CourtPricings, id, "Pricing rule");
        [HttpPost, ValidateAntiForgeryToken] public Task<IActionResult> ToggleScheduleStatus(int id) => ToggleAsync(_dbContext.CourtSchedules, id, "Schedule");

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateCourtOperationalStatus(int id, CourtStatus status)
        {
            if (EnsureCanManage() is IActionResult blocked) return blocked;
            var item = await _dbContext.Courts.FindAsync(id);
            if (item == null) return Json(new { success = false, message = "Court not found." });
            item.Status = status; item.UpdatedAt = DateTime.UtcNow;
            await _dbContext.SaveChangesAsync();
            return Json(new { success = true, message = "Court operational status updated.", statusText = status.ToString() });
        }

        private async Task<IActionResult> ToggleAsync<T>(DbSet<T> set, int id, string label) where T : class
        {
            if (EnsureCanManage() is IActionResult blocked) return blocked;
            var item = await set.FindAsync(id);
            if (item == null) return Json(new { success = false, message = $"{label} not found." });
            var activeProp = typeof(T).GetProperty("IsActive")!;
            var updatedProp = typeof(T).GetProperty("UpdatedAt");
            var newValue = !(bool)activeProp.GetValue(item)!;
            if (typeof(T) == typeof(Sport) && !newValue && await _dbContext.Courts.AnyAsync(x => x.SportId == id && x.IsActive)) return Json(new { success = false, message = "Cannot deactivate sport while active courts depend on it." });
            if (typeof(T) == typeof(Facility) && !newValue && await _dbContext.Courts.AnyAsync(x => x.FacilityId == id && x.IsActive)) return Json(new { success = false, message = "Cannot deactivate facility while active courts depend on it." });
            activeProp.SetValue(item, newValue);
            updatedProp?.SetValue(item, DateTime.UtcNow);
            await _dbContext.SaveChangesAsync();
            return Json(new { success = true, isActive = newValue, statusText = newValue ? "Active" : "Inactive", message = $"{label} {(newValue ? "activated" : "deactivated")} successfully." });
        }

        private async Task<ArenaSetupViewModel> BaseModelAsync(string tab, string? search, string? status, string? sort, int page, int pageSize, int? sportId, int? facilityId, int? courtId, string? day, string? type, string? operationalStatus) => new()
        {
            ActiveTab = tab, Search = search, StatusFilter = status, SortBy = sort, CurrentPage = page, PageSize = pageSize, SportId = sportId, FacilityId = facilityId, CourtId = courtId, Day = day, Type = type, OperationalStatus = operationalStatus,
            CanManage = CanManage(),
            ActiveSports = await _dbContext.Sports.CountAsync(x => x.IsActive),
            ActiveFacilities = await _dbContext.Facilities.CountAsync(x => x.IsActive),
            ActiveCourts = await _dbContext.Courts.CountAsync(x => x.IsActive),
            PricingRules = await _dbContext.CourtPricings.CountAsync(x => x.IsActive),
            WeeklySchedules = await _dbContext.CourtSchedules.CountAsync(x => x.IsActive),
            SportOptions = await SportOptionsAsync(),
            FacilityOptions = await FacilityOptionsAsync(),
            CourtOptions = await CourtOptionsAsync()
        };

        private async Task LoadSportsAsync(ArenaSetupViewModel model)
        {
            var q = _dbContext.Sports.AsNoTracking().Include(x => x.Courts).AsQueryable();
            if (!string.IsNullOrWhiteSpace(model.Search)) q = q.Where(x => x.Name.Contains(model.Search) || x.Code.Contains(model.Search));
            q = ApplyActiveFilter(q, model.StatusFilter);
            q = model.SortBy == "name" ? q.OrderBy(x => x.Name) : q.OrderBy(x => x.DisplayOrder).ThenBy(x => x.Name);
            model.TotalRecords = await q.CountAsync(); model.TotalPages = Pages(model.TotalRecords, model.PageSize);
            model.Sports = await q.Skip((model.CurrentPage - 1) * model.PageSize).Take(model.PageSize).Select(x => new SportListItemViewModel { Id = x.Id, Name = x.Name, Code = x.Code, IconClass = x.IconClass, AccentColor = x.AccentColor, DefaultDurationMinutes = x.DefaultDurationMinutes, DisplayOrder = x.DisplayOrder, CourtCount = x.Courts.Count, IsActive = x.IsActive }).ToListAsync();
        }

        private async Task LoadFacilitiesAsync(ArenaSetupViewModel model)
        {
            var q = _dbContext.Facilities.AsNoTracking().Include(x => x.Courts).AsQueryable();
            if (!string.IsNullOrWhiteSpace(model.Search)) q = q.Where(x => x.Name.Contains(model.Search) || x.Code.Contains(model.Search) || (x.LocationLabel != null && x.LocationLabel.Contains(model.Search)));
            if (Enum.TryParse<FacilityType>(model.Type, out var type)) q = q.Where(x => x.Type == type);
            q = ApplyActiveFilter(q, model.StatusFilter).OrderBy(x => x.DisplayOrder).ThenBy(x => x.Name);
            model.TotalRecords = await q.CountAsync(); model.TotalPages = Pages(model.TotalRecords, model.PageSize);
            model.Facilities = await q.Skip((model.CurrentPage - 1) * model.PageSize).Take(model.PageSize).Select(x => new FacilityListItemViewModel { Id = x.Id, Name = x.Name, Code = x.Code, Type = x.Type, LocationLabel = x.LocationLabel, CourtCount = x.Courts.Count, IsActive = x.IsActive }).ToListAsync();
        }

        private async Task LoadCourtsAsync(ArenaSetupViewModel model)
        {
            var q = _dbContext.Courts.AsNoTracking().Include(x => x.Sport).Include(x => x.Facility).Include(x => x.Pricings).AsQueryable();
            if (!string.IsNullOrWhiteSpace(model.Search)) q = q.Where(x => x.Name.Contains(model.Search) || x.Code.Contains(model.Search));
            if (model.SportId.HasValue) q = q.Where(x => x.SportId == model.SportId);
            if (model.FacilityId.HasValue) q = q.Where(x => x.FacilityId == model.FacilityId);
            if (Enum.TryParse<CourtStatus>(model.OperationalStatus, out var status)) q = q.Where(x => x.Status == status);
            q = ApplyActiveFilter(q, model.StatusFilter).OrderBy(x => x.DisplayOrder).ThenBy(x => x.Name);
            model.TotalRecords = await q.CountAsync(); model.TotalPages = Pages(model.TotalRecords, model.PageSize);
            model.Courts = await q.Skip((model.CurrentPage - 1) * model.PageSize).Take(model.PageSize).Select(x => new CourtListItemViewModel { Id = x.Id, Name = x.Name, Code = x.Code, SportName = x.Sport.Name, FacilityName = x.Facility.Name, Status = x.Status, Capacity = x.Capacity, PricingCount = x.Pricings.Count, IsActive = x.IsActive }).ToListAsync();
        }

        private async Task LoadPricingAsync(ArenaSetupViewModel model)
        {
            var q = _dbContext.CourtPricings.AsNoTracking().Include(x => x.Court).ThenInclude(x => x.Sport).AsQueryable();
            if (!string.IsNullOrWhiteSpace(model.Search)) q = q.Where(x => x.Name.Contains(model.Search) || x.Court.Name.Contains(model.Search));
            if (model.CourtId.HasValue) q = q.Where(x => x.CourtId == model.CourtId);
            if (model.SportId.HasValue) q = q.Where(x => x.Court.SportId == model.SportId);
            if (Enum.TryParse<DayOfWeek>(model.Day, out var day)) q = q.Where(x => x.DayOfWeek == day);
            if (model.Type == "peak") q = q.Where(x => x.IsPeakRate); else if (model.Type == "standard") q = q.Where(x => !x.IsPeakRate);
            q = ApplyActiveFilter(q, model.StatusFilter).OrderBy(x => x.Court.Name).ThenBy(x => x.StartTime);
            model.TotalRecords = await q.CountAsync(); model.TotalPages = Pages(model.TotalRecords, model.PageSize);
            model.Pricings = await q.Skip((model.CurrentPage - 1) * model.PageSize).Take(model.PageSize).Select(x => new PricingListItemViewModel { Id = x.Id, Name = x.Name, CourtName = x.Court.Name, SportName = x.Court.Sport.Name, DayOfWeek = x.DayOfWeek, StartTime = x.StartTime, EndTime = x.EndTime, DurationMinutes = x.DurationMinutes, Price = x.Price, IsPeakRate = x.IsPeakRate, IsActive = x.IsActive }).ToListAsync();
        }

        private async Task LoadSchedulesAsync(ArenaSetupViewModel model)
        {
            var q = _dbContext.CourtSchedules.AsNoTracking().Include(x => x.Court).ThenInclude(x => x.Sport).AsQueryable();
            if (model.CourtId.HasValue) q = q.Where(x => x.CourtId == model.CourtId);
            if (model.SportId.HasValue) q = q.Where(x => x.Court.SportId == model.SportId);
            if (Enum.TryParse<DayOfWeek>(model.Day, out var day)) q = q.Where(x => x.DayOfWeek == day);
            q = ApplyActiveFilter(q, model.StatusFilter).OrderBy(x => x.Court.Name).ThenBy(x => x.DayOfWeek);
            model.TotalRecords = await q.CountAsync(); model.TotalPages = Pages(model.TotalRecords, model.PageSize);
            model.Schedules = await q.Skip((model.CurrentPage - 1) * model.PageSize).Take(model.PageSize).Select(x => new ScheduleListItemViewModel { Id = x.Id, CourtName = x.Court.Name, SportName = x.Court.Sport.Name, DayOfWeek = x.DayOfWeek, OpeningTime = x.OpeningTime, ClosingTime = x.ClosingTime, IsClosed = x.IsClosed, SlotDurationMinutes = x.SlotDurationMinutes, BufferMinutes = x.BufferMinutes, IsActive = x.IsActive }).ToListAsync();
        }

        private IQueryable<T> ApplyActiveFilter<T>(IQueryable<T> q, string? status) where T : class
        {
            if (status != "active" && status != "inactive") return q;
            var active = status == "active";
            return q.Where(x => EF.Property<bool>(x, "IsActive") == active);
        }

        private bool CanManage() => RolePermissions.TryGetCurrentRole(User, out var role) && role != UserRole.Viewer;
        private IActionResult? EnsureCanManage() => CanManage() ? null : RedirectToAction("NotFound", "Error");
        private static int Pages(int total, int size) => total == 0 ? 1 : (int)Math.Ceiling(total / (double)size);
        private static string NormalizeTab(string? tab)
        {
            var normalized = tab?.ToLowerInvariant();
            return new[] { "sports", "facilities", "courts", "pricing", "schedules" }.Contains(normalized) ? normalized! : "sports";
        }
        private async Task<IReadOnlyList<SelectListItem>> SportOptionsAsync() => await _dbContext.Sports.AsNoTracking().Where(x => x.IsActive).OrderBy(x => x.Name).Select(x => new SelectListItem(x.Name, x.Id.ToString())).ToListAsync();
        private async Task<IReadOnlyList<SelectListItem>> FacilityOptionsAsync() => await _dbContext.Facilities.AsNoTracking().Where(x => x.IsActive).OrderBy(x => x.Name).Select(x => new SelectListItem(x.Name, x.Id.ToString())).ToListAsync();
        private async Task<IReadOnlyList<SelectListItem>> CourtOptionsAsync() => await _dbContext.Courts.AsNoTracking().Where(x => x.IsActive).OrderBy(x => x.Name).Select(x => new SelectListItem(x.Name, x.Id.ToString())).ToListAsync();
        private async Task<CourtFormViewModel> AddCourtOptionsAsync(CourtFormViewModel model) { model.SportOptions = await SportOptionsAsync(); model.FacilityOptions = await FacilityOptionsAsync(); return model; }
        private async Task<CourtPricingFormViewModel> AddCourtOptionsAsync(CourtPricingFormViewModel model) { model.CourtOptions = await CourtOptionsAsync(); return model; }
        private async Task<CourtScheduleFormViewModel> AddCourtOptionsAsync(CourtScheduleFormViewModel model) { model.CourtOptions = await CourtOptionsAsync(); return model; }
        private static string? Clean(string? value, bool upper = false) { value = string.IsNullOrWhiteSpace(value) ? null : value.Trim(); return upper ? value?.ToUpperInvariant() : value; }
        private static void NormalizeSport(SportFormViewModel m) { m.Name = Clean(m.Name)!; m.Code = Clean(m.Code, true)!; m.Description = Clean(m.Description); m.IconClass = Clean(m.IconClass); m.ImageUrl = Clean(m.ImageUrl); m.AccentColor = Clean(m.AccentColor); }
        private static void NormalizeFacility(FacilityFormViewModel m) { m.Name = Clean(m.Name)!; m.Code = Clean(m.Code, true)!; m.Description = Clean(m.Description); m.LocationLabel = Clean(m.LocationLabel); }
        private static void NormalizeCourt(CourtFormViewModel m) { m.Name = Clean(m.Name)!; m.Code = Clean(m.Code, true)!; m.Description = Clean(m.Description); m.ImageUrl = Clean(m.ImageUrl); m.Notes = Clean(m.Notes); }
        private static void NormalizePricing(CourtPricingFormViewModel m) { m.Name = Clean(m.Name)!; }
        private Task<bool> PricingDuplicateAsync(CourtPricingFormViewModel m, int? exceptId = null) => _dbContext.CourtPricings.AnyAsync(x => x.CourtId == m.CourtId && x.DayOfWeek == m.DayOfWeek && x.StartTime == m.StartTime && x.EndTime == m.EndTime && x.DurationMinutes == m.DurationMinutes && (!exceptId.HasValue || x.Id != exceptId));
        private Task<bool> ScheduleDuplicateAsync(CourtScheduleFormViewModel m, int? exceptId = null) => m.IsActive ? _dbContext.CourtSchedules.AnyAsync(x => x.CourtId == m.CourtId && x.DayOfWeek == m.DayOfWeek && x.IsActive && (!exceptId.HasValue || x.Id != exceptId)) : Task.FromResult(false);
    }
}
