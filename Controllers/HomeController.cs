using GameHub.Data;
using GameHub.Models.Enums;
using GameHub.Models.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GameHub.Controllers
{
    public class HomeController : Controller
    {
        private readonly ApplicationDbContext _dbContext;

        public HomeController(ApplicationDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public async Task<IActionResult> Index()
        {
            var settings = await _dbContext.SystemSettings.AsNoTracking().FirstOrDefaultAsync();

            var courts = await _dbContext.Courts
                .AsNoTracking()
                .Include(court => court.Sport)
                .Include(court => court.Facility)
                .Where(court =>
                    court.IsActive &&
                    court.Sport.IsActive &&
                    court.Facility.IsActive &&
                    court.Status != CourtStatus.Maintenance &&
                    court.Status != CourtStatus.Closed)
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

            ViewData["CourtsModel"] = new PublicCourtsIndexViewModel
            {
                Courts = courts,
                Sports = sports,
                Facilities = facilities,
                CurrencySymbol = string.IsNullOrWhiteSpace(settings?.CurrencySymbol) ? "Rs" : settings.CurrencySymbol!
            };

            ViewBag.SupportPhone = settings?.Phone ?? "+92 300 1234567";
            ViewBag.SupportEmail = settings?.Email ?? "info@gamehub.pk";

            return View();
        }

        public IActionResult Error()
        {
            return View();
        }
    }
}
