using GameHub.Data;
using GameHub.Models.Enums;
using GameHub.Models.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GameHub.Controllers
{
    [AllowAnonymous]
    public class CourtsController : Controller
    {
        private readonly ApplicationDbContext _dbContext;

        public CourtsController(ApplicationDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        [HttpGet]
        public async Task<IActionResult> Index(string? search, string? sport, string? facility, string? facilityType)
        {
            search = search?.Trim();
            sport = string.IsNullOrWhiteSpace(sport) ? null : sport.Trim();
            facility = string.IsNullOrWhiteSpace(facility) ? null : facility.Trim();
            facilityType = string.IsNullOrWhiteSpace(facilityType) ? null : facilityType.Trim();

            var settings = await _dbContext.SystemSettings.AsNoTracking().FirstOrDefaultAsync();
            var query = PublicCourtQuery();

            if (!string.IsNullOrWhiteSpace(search))
            {
                var normalized = search.ToLower();
                query = query.Where(court =>
                    court.Name.ToLower().Contains(normalized) ||
                    court.Sport.Name.ToLower().Contains(normalized) ||
                    court.Facility.Name.ToLower().Contains(normalized));
            }

            if (!string.IsNullOrWhiteSpace(sport))
            {
                query = query.Where(court => court.Sport.Code == sport || court.Sport.Name == sport);
            }

            if (!string.IsNullOrWhiteSpace(facility))
            {
                query = query.Where(court => court.Facility.Name == facility);
            }

            if (!string.IsNullOrWhiteSpace(facilityType))
            {
                query = query.Where(court => court.Facility.Type.ToString() == facilityType);
            }

            var courts = await query
                .OrderBy(court => court.DisplayOrder)
                .ThenBy(court => court.Name)
                .Select(court => new PublicCourtListItemViewModel
                {
                    Id = court.Id,
                    Name = court.Name,
                    Code = court.Code,
                    SportName = court.Sport.Name,
                    SportCode = court.Sport.Code,
                    FacilityName = court.Facility.Name,
                    FacilityType = court.Facility.Type.ToString(),
                    Capacity = court.Capacity,
                    ImageUrl = court.ImageUrl,
                    Status = court.Status,
                    DefaultDurationMinutes = court.Sport.DefaultDurationMinutes,
                    StartingPrice = court.Pricings.Where(price => price.IsActive).OrderBy(price => price.Price).Select(price => (decimal?)price.Price).FirstOrDefault()
                })
                .ToListAsync();

            var sports = await _dbContext.Sports.AsNoTracking().Where(item => item.IsActive).OrderBy(item => item.DisplayOrder).Select(item => item.Name).ToListAsync();
            var facilities = await _dbContext.Facilities.AsNoTracking().Where(item => item.IsActive).OrderBy(item => item.DisplayOrder).Select(item => item.Name).ToListAsync();

            return View(new PublicCourtsIndexViewModel
            {
                Courts = courts,
                Sports = sports,
                Facilities = facilities,
                Search = search,
                Sport = sport,
                Facility = facility,
                FacilityType = facilityType,
                CurrencySymbol = string.IsNullOrWhiteSpace(settings?.CurrencySymbol) ? "Rs" : settings.CurrencySymbol!
            });
        }

        [HttpGet]
        public async Task<IActionResult> Details(int id)
        {
            var settings = await _dbContext.SystemSettings.AsNoTracking().FirstOrDefaultAsync();
            var court = await PublicCourtQuery()
                .Include(item => item.Pricings.Where(price => price.IsActive))
                .Include(item => item.Schedules.Where(schedule => schedule.IsActive && !schedule.IsClosed))
                .FirstOrDefaultAsync(item => item.Id == id);

            if (court == null)
            {
                return NotFound();
            }

            return View(new PublicCourtDetailsViewModel
            {
                Id = court.Id,
                Name = court.Name,
                Code = court.Code,
                SportName = court.Sport.Name,
                FacilityName = court.Facility.Name,
                FacilityType = court.Facility.Type.ToString(),
                Description = court.Description,
                ImageUrl = court.ImageUrl,
                Capacity = court.Capacity,
                Status = court.Status,
                DefaultDurationMinutes = court.Sport.DefaultDurationMinutes,
                CurrencySymbol = string.IsNullOrWhiteSpace(settings?.CurrencySymbol) ? "Rs" : settings.CurrencySymbol!,
                SupportPhone = settings?.Phone ?? "+92 300 1234567",
                SupportEmail = settings?.Email ?? "info@gamehub.pk",
                Pricings = court.Pricings.OrderBy(item => item.DayOfWeek).ThenBy(item => item.StartTime).Select(item => new PublicPricingItemViewModel
                {
                    Name = item.Name,
                    DayOfWeek = item.DayOfWeek,
                    StartTime = item.StartTime,
                    EndTime = item.EndTime,
                    DurationMinutes = item.DurationMinutes,
                    Price = item.Price,
                    IsPeakRate = item.IsPeakRate
                }).ToList(),
                Schedules = court.Schedules.OrderBy(item => item.DayOfWeek).ThenBy(item => item.OpeningTime).Select(item => new PublicScheduleItemViewModel
                {
                    DayOfWeek = item.DayOfWeek,
                    OpeningTime = item.OpeningTime,
                    ClosingTime = item.ClosingTime,
                    SlotDurationMinutes = item.SlotDurationMinutes,
                    BufferMinutes = item.BufferMinutes
                }).ToList()
            });
        }

        [HttpGet("/Sports/{code}")]
        public IActionResult Sport(string code)
        {
            return RedirectToAction(nameof(Index), new { sport = code });
        }

        [HttpGet("/HowBookingWorks")]
        public async Task<IActionResult> HowItWorks()
        {
            var settings = await _dbContext.SystemSettings.AsNoTracking().FirstOrDefaultAsync();
            ViewBag.SupportPhone = settings?.Phone ?? "+92 300 1234567";
            ViewBag.SupportEmail = settings?.Email ?? "info@gamehub.pk";
            ViewBag.AllowOnlineBooking = settings?.AllowOnlineBooking ?? true;
            return View();
        }

        private IQueryable<Models.Entities.Court> PublicCourtQuery()
        {
            return _dbContext.Courts
                .AsNoTracking()
                .Include(court => court.Sport)
                .Include(court => court.Facility)
                .Where(court =>
                    court.IsActive &&
                    court.Sport.IsActive &&
                    court.Facility.IsActive &&
                    court.Status != CourtStatus.Maintenance &&
                    court.Status != CourtStatus.Closed);
        }
    }
}
