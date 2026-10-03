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
    [GameHubAuthorize(UserRole.SuperAdmin)]
    [ValidateActiveUser]
    public class SettingsController : Controller
    {
        private readonly ApplicationDbContext _dbContext;

        public SettingsController(ApplicationDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var settings = await GetOrCreateSettingsAsync();
            return View(MapToViewModel(settings));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Index(SystemSettingsViewModel model)
        {
            Normalize(model);

            if (!ModelState.IsValid)
            {
                TempData.SetToast("error", "Something Went Wrong", "Please fix the highlighted settings fields.");
                return View(model);
            }

            var settings = await GetOrCreateSettingsAsync();
            settings.ArenaName = model.ArenaName;
            settings.Phone = model.Phone;
            settings.Email = model.Email;
            settings.Address = model.Address;
            settings.Logo = model.Logo;
            settings.OpeningTime = model.OpeningTime!.Value;
            settings.ClosingTime = model.ClosingTime!.Value;
            settings.SlotDurationMinutes = model.SlotDurationMinutes;
            settings.BufferMinutes = model.BufferMinutes;
            settings.AdvanceBookingDays = model.AdvanceBookingDays;
            settings.AllowWalkIn = model.AllowWalkIn;
            settings.AllowOnlineBooking = model.AllowOnlineBooking;
            settings.Currency = model.Currency;
            settings.CurrencySymbol = model.CurrencySymbol;
            settings.TaxPercentage = model.TaxPercentage;
            settings.BookingPrefix = model.BookingPrefix;
            settings.InvoicePrefix = model.InvoicePrefix;
            settings.ReceiptPrefix = model.ReceiptPrefix;
            settings.SessionTimeoutMinutes = model.SessionTimeoutMinutes;
            settings.PasswordMinLength = model.PasswordMinLength;
            settings.RequireStrongPassword = model.RequireStrongPassword;
            settings.Version = model.Version;
            settings.BuildDate = model.BuildDate;

            await _dbContext.SaveChangesAsync();
            TempData.SetToast("success", "Success", "System settings updated successfully.");

            return RedirectToAction(nameof(Index));
        }

        private async Task<SystemSettings> GetOrCreateSettingsAsync()
        {
            var settings = await _dbContext.SystemSettings.OrderBy(item => item.Id).FirstOrDefaultAsync();
            if (settings != null)
            {
                return settings;
            }

            settings = new SystemSettings
            {
                ArenaName = "GameHub Arena",
                Phone = "+92 300 1234567",
                Email = "info@gamehub.pk",
                Address = "123 Sports Avenue, Lahore",
                OpeningTime = new TimeOnly(6, 0),
                ClosingTime = new TimeOnly(23, 59),
                SlotDurationMinutes = 60,
                BufferMinutes = 10,
                AdvanceBookingDays = 14,
                AllowWalkIn = true,
                AllowOnlineBooking = true,
                Currency = "PKR",
                CurrencySymbol = "Rs",
                TaxPercentage = 0,
                BookingPrefix = "BK",
                InvoicePrefix = "INV",
                ReceiptPrefix = "RCPT",
                SessionTimeoutMinutes = 480,
                PasswordMinLength = 8,
                RequireStrongPassword = true,
                Version = "1.0.0",
                BuildDate = DateTime.UtcNow
            };

            _dbContext.SystemSettings.Add(settings);
            await _dbContext.SaveChangesAsync();
            return settings;
        }

        private static SystemSettingsViewModel MapToViewModel(SystemSettings settings)
        {
            return new SystemSettingsViewModel
            {
                Id = settings.Id,
                ArenaName = settings.ArenaName,
                Phone = settings.Phone,
                Email = settings.Email,
                Address = settings.Address,
                Logo = settings.Logo,
                OpeningTime = settings.OpeningTime,
                ClosingTime = settings.ClosingTime,
                SlotDurationMinutes = settings.SlotDurationMinutes,
                BufferMinutes = settings.BufferMinutes,
                AdvanceBookingDays = settings.AdvanceBookingDays,
                AllowWalkIn = settings.AllowWalkIn,
                AllowOnlineBooking = settings.AllowOnlineBooking,
                Currency = settings.Currency,
                CurrencySymbol = settings.CurrencySymbol,
                TaxPercentage = settings.TaxPercentage,
                BookingPrefix = settings.BookingPrefix,
                InvoicePrefix = settings.InvoicePrefix,
                ReceiptPrefix = settings.ReceiptPrefix,
                SessionTimeoutMinutes = settings.SessionTimeoutMinutes,
                PasswordMinLength = settings.PasswordMinLength,
                RequireStrongPassword = settings.RequireStrongPassword,
                Version = settings.Version,
                BuildDate = settings.BuildDate
            };
        }

        private static void Normalize(SystemSettingsViewModel model)
        {
            model.ArenaName = model.ArenaName?.Trim() ?? string.Empty;
            model.Phone = model.Phone?.Trim() ?? string.Empty;
            model.Email = model.Email?.Trim().ToLowerInvariant() ?? string.Empty;
            model.Address = string.IsNullOrWhiteSpace(model.Address) ? null : model.Address.Trim();
            model.Logo = string.IsNullOrWhiteSpace(model.Logo) ? null : model.Logo.Trim();
            model.Currency = model.Currency?.Trim().ToUpperInvariant() ?? string.Empty;
            model.CurrencySymbol = string.IsNullOrWhiteSpace(model.CurrencySymbol) ? null : model.CurrencySymbol.Trim();
            model.BookingPrefix = model.BookingPrefix?.Trim().ToUpperInvariant() ?? string.Empty;
            model.InvoicePrefix = model.InvoicePrefix?.Trim().ToUpperInvariant() ?? string.Empty;
            model.ReceiptPrefix = model.ReceiptPrefix?.Trim().ToUpperInvariant() ?? string.Empty;
            model.Version = string.IsNullOrWhiteSpace(model.Version) ? null : model.Version.Trim();
        }
    }
}
