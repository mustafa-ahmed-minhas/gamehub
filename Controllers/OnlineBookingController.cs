using System.Security.Claims;
using System.Text.Json;
using GameHub.Data;
using GameHub.Helpers;
using GameHub.Models.Checkout;
using GameHub.Models.Entities;
using GameHub.Models.Enums;
using GameHub.Models.ViewModels;
using GameHub.Services.Interfaces;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GameHub.Controllers
{
    [AllowAnonymous]
    [Route("Book")]
    public class OnlineBookingController : Controller
    {
        private const string CheckoutSessionKey = "GameHub.OnlineBooking.Checkout";
        private readonly ApplicationDbContext _dbContext;
        private readonly IBookingValidationService _validationService;

        public OnlineBookingController(ApplicationDbContext dbContext, IBookingValidationService validationService)
        {
            _dbContext = dbContext;
            _validationService = validationService;
        }

        [HttpGet("Court/{id:int}")]
        public async Task<IActionResult> Start(int id)
        {
            var court = await LoadPublicCourtAsync(id);
            if (court == null) return RedirectToAction("NotFound", "Error");

            var settings = await GetSettingsAsync();
            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            var customer = await GetCurrentCustomerAsync();
            var schedules = await _dbContext.CourtSchedules.AsNoTracking()
                .Where(item => item.CourtId == id && item.IsActive && !item.IsClosed)
                .OrderBy(item => item.DayOfWeek)
                .ToListAsync();

            var durations = await GetDurationOptionsAsync(court, settings);
            var startingPrice = await _dbContext.CourtPricings.AsNoTracking()
                .Where(item => item.CourtId == id && item.IsActive)
                .OrderBy(item => item.Price)
                .Select(item => (decimal?)item.Price)
                .FirstOrDefaultAsync();

            return View(new OnlineBookingStartViewModel
            {
                CourtId = court.Id,
                SportId = court.SportId,
                ArenaName = settings.ArenaName,
                CurrencySymbol = settings.CurrencySymbol ?? "Rs",
                CourtName = court.Name,
                SportName = court.Sport.Name,
                SportCode = court.Sport.Code,
                FacilityName = court.Facility.Name,
                FacilityType = court.Facility.Type.ToString(),
                CourtImageUrl = court.ImageUrl,
                Description = court.Description,
                Capacity = court.Capacity,
                StartingPrice = startingPrice,
                DefaultDurationMinutes = court.Sport.DefaultDurationMinutes,
                MinDate = today,
                MaxDate = today.AddDays(Math.Max(0, settings.AdvanceBookingDays)),
                Tomorrow = today.AddDays(1),
                WeekendDate = NextWeekend(today),
                DurationOptions = durations,
                OperatingSummary = schedules.Select(item => $"{item.DayOfWeek}: {item.OpeningTime:hh\\:mm} - {item.ClosingTime:hh\\:mm}").ToList(),
                IsCustomerSignedIn = customer != null,
                EmailVerified = customer?.EmailVerified == true
            });
        }

        [HttpGet("Availability")]
        public async Task<IActionResult> Availability(int courtId, DateOnly bookingDate, int durationMinutes)
        {
            var settings = await GetSettingsAsync();
            var validation = await ValidateCourtDateDurationAsync(courtId, bookingDate, durationMinutes, settings);
            if (!validation.Valid)
            {
                return Json(new { success = false, message = validation.Message, slots = Array.Empty<PublicAvailableSlotViewModel>() });
            }

            var slots = await GenerateSlotsAsync(courtId, bookingDate, durationMinutes, settings.CurrencySymbol ?? "Rs");
            return Json(new { success = true, slots });
        }

        [HttpGet("CustomerSummary")]
        public async Task<IActionResult> CustomerSummary()
        {
            var customer = await GetCurrentCustomerAsync();
            if (customer == null) return Json(new { success = false, requiresLogin = true, message = "Please sign in to continue." });
            return Json(new
            {
                success = true,
                customer = new
                {
                    customer.FullName,
                    customer.CustomerCode,
                    customer.Email,
                    customer.PrimaryPhone,
                    customer.EmailVerified,
                    customer.MembershipStatus,
                    customer.LoyaltyPoints
                }
            });
        }

        [HttpGet("MembershipSummary")]
        public async Task<IActionResult> MembershipSummary(int sportId, DateOnly bookingDate, bool isPeakRate = false)
        {
            var customer = await GetCurrentCustomerAsync();
            if (customer == null) return Json(new { success = false, requiresLogin = true, message = "Please sign in to view membership benefits." });
            var summary = await GetMembershipBenefitAsync(customer.Id, sportId, bookingDate, isPeakRate);
            return Json(new { success = true, membership = summary });
        }

        [HttpPost("Calculate")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Calculate(OnlineBookingReviewViewModel model)
        {
            var customer = await GetCurrentCustomerAsync();
            if (customer == null) return Json(new { success = false, requiresLogin = true, message = "Please sign in before price review." });

            var result = await ValidateAndPriceAsync(model, customer, requireVerifiedEmail: false, requirePolicies: false);
            if (!result.Valid)
            {
                return Json(new { success = false, message = result.Message });
            }

            return Json(new
            {
                success = true,
                price = new
                {
                    result.Price!.CurrencySymbol,
                    result.Price.BaseAmount,
                    result.Price.MembershipDiscountAmount,
                    result.Price.TaxAmount,
                    result.Price.TotalAmount,
                    result.Price.IsPeakRate,
                    result.Price.MembershipMessage
                }
            });
        }

        [HttpPost("Review")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Review(OnlineBookingReviewViewModel model)
        {
            var customer = await GetCurrentCustomerAsync(tracked: true);
            if (customer == null)
            {
                return RedirectToAction("Login", "Account", new { returnUrl = Url.Action(nameof(Start), new { id = model.CourtId }) });
            }

            var result = await ValidateAndPriceAsync(model, customer, requireVerifiedEmail: true, requirePolicies: true);
            if (!result.Valid)
            {
                TempData.SetToast("warning", "Review Needed", result.Message);
                return RedirectToAction(nameof(Start), new { id = model.CourtId });
            }

            var checkout = CreateCheckoutSession(result.Court!, customer, model, result.Price!);
            SaveCheckoutSession(checkout);
            return View(ToCheckoutViewModel(checkout));
        }

        [HttpGet("Checkout")]
        public async Task<IActionResult> Checkout()
        {
            var customer = await GetCurrentCustomerAsync();
            if (customer == null)
            {
                return RedirectToAction("Login", "Account", new { returnUrl = Url.Action(nameof(Checkout)) });
            }

            if (!customer.EmailVerified)
            {
                TempData.SetToast("warning", "Verification Required", "Please verify your email before checkout.");
                return RedirectToAction(nameof(Start), new { id = GetCheckoutSession()?.CourtId ?? 0 });
            }

            var checkout = GetCheckoutSession();
            if (checkout == null || checkout.CustomerId != customer.Id || checkout.ExpiresAtUtc <= DateTime.UtcNow)
            {
                ClearCheckoutSession();
                return View(new OnlineBookingCheckoutViewModel { IsExpired = true });
            }

            if (!customer.IsActive || customer.IsBlacklisted || !customer.HasOnlineAccount)
            {
                ClearCheckoutSession();
                return RedirectToAction("Login", "Account");
            }

            return View(ToCheckoutViewModel(checkout));
        }

        [HttpPost("CancelCheckout")]
        [ValidateAntiForgeryToken]
        public IActionResult CancelCheckout()
        {
            var courtId = GetCheckoutSession()?.CourtId;
            ClearCheckoutSession();
            TempData.SetToast("info", "Checkout Cancelled", "Your unfinished checkout was cleared.");
            return RedirectToAction(nameof(Start), courtId.HasValue ? new { id = courtId.Value } : null);
        }

        private async Task<(bool Valid, string Message, Court? Court, PriceResult? Price)> ValidateAndPriceAsync(OnlineBookingReviewViewModel model, Customer customer, bool requireVerifiedEmail, bool requirePolicies)
        {
            if (requireVerifiedEmail && !customer.EmailVerified) return (false, "Please verify your email before checkout.", null, null);
            if (!customer.IsActive || customer.IsBlacklisted || !customer.HasOnlineAccount) return (false, "Your account cannot continue online checkout. Please contact support.", null, null);

            var settings = await GetSettingsAsync();
            var courtValidation = await ValidateCourtDateDurationAsync(model.CourtId, model.BookingDate, model.DurationMinutes, settings);
            if (!courtValidation.Valid) return (false, courtValidation.Message, null, null);

            if (!TimeOnly.TryParse(model.StartTime, out var start)) return (false, "Select a valid start time.", null, null);
            var end = start.AddMinutes(model.DurationMinutes);
            var court = courtValidation.Court!;
            if (model.PlayerCount < 1 || model.PlayerCount > court.Capacity) return (false, $"Player count must be between 1 and {court.Capacity}.", null, null);
            if (model.CustomerNotes?.Length > 500 || model.SpecialRequest?.Length > 500) return (false, "Notes and special requests must be 500 characters or fewer.", null, null);
            if (requirePolicies && (!model.AcceptBookingPolicy || !model.AcceptCancellationPolicy)) return (false, "Please accept the booking and cancellation policies.", null, null);

            var availability = await IsAvailableAsync(model.CourtId, model.BookingDate, start, end);
            if (!availability.Available) return (false, availability.Message, null, null);

            var price = await CalculatePriceAsync(customer.Id, court.SportId, court.Id, model.BookingDate, start, end, model.DurationMinutes, settings);
            if (!price.Valid) return (false, price.Message, null, null);
            return (true, "Ready for checkout.", court, price);
        }

        private OnlineBookingCheckoutSession CreateCheckoutSession(Court court, Customer customer, OnlineBookingReviewViewModel model, PriceResult price)
        {
            var start = TimeOnly.Parse(model.StartTime);
            return new OnlineBookingCheckoutSession
            {
                CheckoutToken = Guid.NewGuid().ToString("N"),
                CustomerId = customer.Id,
                CustomerName = customer.FullName,
                CustomerCode = customer.CustomerCode,
                SportId = court.SportId,
                FacilityId = court.FacilityId,
                CourtId = court.Id,
                ArenaName = price.ArenaName,
                SportName = court.Sport.Name,
                FacilityName = court.Facility.Name,
                CourtName = court.Name,
                CurrencySymbol = price.CurrencySymbol,
                BookingDate = model.BookingDate,
                StartTime = start,
                EndTime = start.AddMinutes(model.DurationMinutes),
                DurationMinutes = model.DurationMinutes,
                PlayerCount = model.PlayerCount,
                CustomerMembershipId = price.CustomerMembershipId,
                MembershipSummary = price.MembershipMessage,
                CustomerNotes = Clean(model.CustomerNotes),
                SpecialRequest = Clean(model.SpecialRequest),
                BaseAmount = price.BaseAmount,
                MembershipDiscountAmount = price.MembershipDiscountAmount,
                TaxAmount = price.TaxAmount,
                TotalAmount = price.TotalAmount,
                IsPeakRate = price.IsPeakRate,
                CreatedAtUtc = DateTime.UtcNow,
                ExpiresAtUtc = DateTime.UtcNow.AddMinutes(15)
            };
        }

        private async Task<List<PublicAvailableSlotViewModel>> GenerateSlotsAsync(int courtId, DateOnly date, int durationMinutes, string currencySymbol)
        {
            var slots = new List<PublicAvailableSlotViewModel>();
            var schedule = await _dbContext.CourtSchedules.AsNoTracking().FirstOrDefaultAsync(item => item.CourtId == courtId && item.DayOfWeek == date.DayOfWeek && item.IsActive);
            if (schedule == null || schedule.IsClosed) return slots;

            var step = schedule.SlotDurationMinutes > 0 ? schedule.SlotDurationMinutes : Math.Max(durationMinutes, 30);
            var openMinutes = schedule.OpeningTime.Hour * 60 + schedule.OpeningTime.Minute;
            var closeMinutes = schedule.ClosingTime.Hour * 60 + schedule.ClosingTime.Minute;
            for (var minute = openMinutes; minute + durationMinutes <= closeMinutes; minute += step + Math.Max(0, schedule.BufferMinutes))
            {
                var start = new TimeOnly(minute / 60, minute % 60);
                var end = start.AddMinutes(durationMinutes);
                var rule = await FindPricingRuleAsync(courtId, date.DayOfWeek, start, end, durationMinutes);
                var available = await IsAvailableAsync(courtId, date, start, end);
                var isAvailable = available.Available && rule != null;
                var baseAmount = rule == null ? 0 : Math.Round(rule.Price * durationMinutes / Math.Max(1, rule.DurationMinutes), 2);
                slots.Add(new PublicAvailableSlotViewModel
                {
                    StartTime = start.ToString("HH:mm"),
                    EndTime = end.ToString("HH:mm"),
                    DisplayTime = $"{start:hh\\:mm tt} - {end:hh\\:mm tt}",
                    IsAvailable = isAvailable,
                    BasePrice = baseAmount,
                    FormattedPrice = rule == null ? "Pricing unavailable" : $"{currencySymbol} {baseAmount:N0}",
                    IsPeakRate = rule?.IsPeakRate ?? false,
                    PricingLabel = rule?.IsPeakRate == true ? "Peak" : "Standard",
                    UnavailableReason = isAvailable ? null : rule == null ? "Pricing unavailable" : available.Message
                });
            }

            return slots;
        }

        private async Task<(bool Valid, string Message, Court? Court)> ValidateCourtDateDurationAsync(int courtId, DateOnly date, int durationMinutes, SystemSettings settings)
        {
            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            if (date < today) return (false, "Past dates cannot be booked.", null);
            if (date > today.AddDays(Math.Max(0, settings.AdvanceBookingDays))) return (false, $"Bookings are limited to {settings.AdvanceBookingDays} days in advance.", null);

            var court = await LoadPublicCourtAsync(courtId);
            if (court == null) return (false, "Selected court is unavailable.", null);
            var durations = await GetDurationOptionsAsync(court, settings);
            if (!durations.Contains(durationMinutes)) return (false, "Selected duration is not available for this court.", court);
            var schedule = await _dbContext.CourtSchedules.AsNoTracking().FirstOrDefaultAsync(item => item.CourtId == courtId && item.DayOfWeek == date.DayOfWeek && item.IsActive);
            if (schedule == null || schedule.IsClosed) return (false, "This court is closed on the selected day.", court);
            return (true, "Valid.", court);
        }

        private async Task<(bool Available, string Message)> IsAvailableAsync(int courtId, DateOnly date, TimeOnly start, TimeOnly end)
        {
            var result = await _validationService.CheckAvailabilityAsync(courtId, date, start, end);
            return (result.IsAvailable, result.Message);
        }

        private async Task<PriceResult> CalculatePriceAsync(int customerId, int sportId, int courtId, DateOnly date, TimeOnly start, TimeOnly end, int durationMinutes, SystemSettings settings)
        {
            var rule = await FindPricingRuleAsync(courtId, date.DayOfWeek, start, end, durationMinutes);
            if (rule == null)
            {
                return PriceResult.Fail(settings, "Pricing is not currently configured for this court and time. Please select another slot or contact GameHub.");
            }

            var baseAmount = Math.Round(rule.Price * Math.Max(1, durationMinutes) / Math.Max(1, rule.DurationMinutes), 2);
            var membershipDiscount = 0m;
            int? membershipId = null;
            var membershipMessage = "No active membership benefits available.";
            var membership = await ActiveMembershipQuery(customerId, date).FirstOrDefaultAsync();
            if (membership != null)
            {
                var sportCode = await _dbContext.Sports.AsNoTracking().Where(item => item.Id == sportId).Select(item => item.Code).FirstOrDefaultAsync();
                var check = ValidateMembershipBenefit(membership, sportCode, rule.IsPeakRate);
                membershipMessage = check.Message;
                if (check.Valid)
                {
                    membershipId = membership.Id;
                    membershipDiscount = Math.Min(baseAmount, Math.Round(baseAmount * membership.DiscountPercentage / 100, 2));
                    membershipMessage = $"{membership.MembershipPlan.Name} discount applied.";
                }
            }

            var taxable = Math.Max(0, baseAmount - membershipDiscount);
            var tax = Math.Round(taxable * settings.TaxPercentage / 100, 2);
            return new PriceResult
            {
                Valid = true,
                ArenaName = settings.ArenaName,
                CurrencySymbol = settings.CurrencySymbol ?? "Rs",
                BaseAmount = baseAmount,
                MembershipDiscountAmount = membershipDiscount,
                TaxAmount = tax,
                TotalAmount = taxable + tax,
                IsPeakRate = rule.IsPeakRate,
                CustomerMembershipId = membershipId,
                MembershipMessage = membershipMessage
            };
        }

        private async Task<CourtPricing?> FindPricingRuleAsync(int courtId, DayOfWeek day, TimeOnly start, TimeOnly end, int duration)
        {
            var rules = await _dbContext.CourtPricings.AsNoTracking()
                .Where(item => item.CourtId == courtId && item.IsActive && item.StartTime <= start && item.EndTime >= end && item.DurationMinutes == duration)
                .ToListAsync();
            return rules.Where(item => item.DayOfWeek == day).OrderByDescending(item => item.IsPeakRate).ThenBy(item => item.Price).FirstOrDefault()
                ?? rules.Where(item => item.DayOfWeek == null && item.IsPeakRate).OrderBy(item => item.Price).FirstOrDefault()
                ?? rules.Where(item => item.DayOfWeek == null).OrderBy(item => item.Price).FirstOrDefault()
                ?? rules.OrderBy(item => item.Price).FirstOrDefault();
        }

        private async Task<IReadOnlyList<int>> GetDurationOptionsAsync(Court court, SystemSettings settings)
        {
            var pricingDurations = await _dbContext.CourtPricings.AsNoTracking()
                .Where(item => item.CourtId == court.Id && item.IsActive && item.DurationMinutes > 0)
                .Select(item => item.DurationMinutes)
                .Distinct()
                .ToListAsync();
            var scheduleDurations = await _dbContext.CourtSchedules.AsNoTracking()
                .Where(item => item.CourtId == court.Id && item.IsActive && !item.IsClosed && item.SlotDurationMinutes > 0)
                .Select(item => item.SlotDurationMinutes)
                .Distinct()
                .ToListAsync();

            return pricingDurations.Concat(scheduleDurations)
                .Concat(new[] { court.Sport.DefaultDurationMinutes, settings.SlotDurationMinutes })
                .Where(item => item > 0 && item <= 240)
                .Distinct()
                .OrderBy(item => item)
                .ToList();
        }

        private IQueryable<CustomerMembership> ActiveMembershipQuery(int customerId, DateOnly date)
        {
            return _dbContext.CustomerMemberships.AsNoTracking()
                .Include(item => item.MembershipPlan)
                .Where(item => item.CustomerId == customerId &&
                    item.IsActive &&
                    item.Status == MembershipStatus.Active &&
                    item.StartDate <= date &&
                    item.ExpiryDate >= date &&
                    item.MembershipPlan.IsActive);
        }

        private async Task<object> GetMembershipBenefitAsync(int customerId, int sportId, DateOnly date, bool isPeakRate)
        {
            var membership = await ActiveMembershipQuery(customerId, date).FirstOrDefaultAsync();
            if (membership == null) return new { applies = false, message = "No active membership.", remainingHours = 0 };
            var sportCode = await _dbContext.Sports.AsNoTracking().Where(item => item.Id == sportId).Select(item => item.Code).FirstOrDefaultAsync();
            var check = ValidateMembershipBenefit(membership, sportCode, isPeakRate);
            return new
            {
                applies = check.Valid,
                message = check.Message,
                plan = membership.MembershipPlan.Name,
                membership.MembershipNumber,
                membership.DiscountPercentage,
                membership.IncludedBookingHours,
                membership.UsedBookingHours,
                remainingHours = Math.Max(0, membership.IncludedBookingHours - membership.UsedBookingHours),
                membership.ExpiryDate,
                allowedSports = membership.MembershipPlan.AllowedSportCodes,
                membership.MembershipPlan.AllowPeakHours,
                membership.MembershipPlan.AllowOffPeakHours
            };
        }

        private (bool Valid, string Message) ValidateMembershipBenefit(CustomerMembership membership, string? sportCode, bool isPeak)
        {
            var allowed = membership.MembershipPlan.AllowedSportCodes;
            if (!string.IsNullOrWhiteSpace(allowed) && allowed != "ALL" && !string.IsNullOrWhiteSpace(sportCode) && !allowed.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).Contains(sportCode))
            {
                return (false, "Membership does not include this sport.");
            }
            if (isPeak && !membership.MembershipPlan.AllowPeakHours) return (false, "Peak-hour benefits are not available for this plan.");
            if (!isPeak && !membership.MembershipPlan.AllowOffPeakHours) return (false, "Off-peak benefits are not available for this plan.");
            return (true, "Membership benefit applied.");
        }

        private async Task<Court?> LoadPublicCourtAsync(int id)
        {
            return await _dbContext.Courts
                .AsNoTracking()
                .Include(item => item.Sport)
                .Include(item => item.Facility)
                .FirstOrDefaultAsync(item =>
                    item.Id == id &&
                    item.IsActive &&
                    item.Sport.IsActive &&
                    item.Facility.IsActive &&
                    item.Status != CourtStatus.Maintenance &&
                    item.Status != CourtStatus.Closed);
        }

        private async Task<Customer?> GetCurrentCustomerAsync(bool tracked = false)
        {
            var auth = await HttpContext.AuthenticateAsync(AuthConstants.CustomerScheme);
            if (!auth.Succeeded || auth.Principal?.Identity?.IsAuthenticated != true) return null;
            var idValue = auth.Principal.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!int.TryParse(idValue, out var customerId)) return null;
            var query = _dbContext.Customers.Where(item => item.Id == customerId && item.HasOnlineAccount);
            return tracked ? await query.FirstOrDefaultAsync() : await query.AsNoTracking().FirstOrDefaultAsync();
        }

        private void SaveCheckoutSession(OnlineBookingCheckoutSession checkout)
        {
            HttpContext.Session.SetString(CheckoutSessionKey, JsonSerializer.Serialize(checkout));
        }

        private OnlineBookingCheckoutSession? GetCheckoutSession()
        {
            var json = HttpContext.Session.GetString(CheckoutSessionKey);
            return string.IsNullOrWhiteSpace(json) ? null : JsonSerializer.Deserialize<OnlineBookingCheckoutSession>(json);
        }

        private void ClearCheckoutSession()
        {
            HttpContext.Session.Remove(CheckoutSessionKey);
        }

        private OnlineBookingCheckoutViewModel ToCheckoutViewModel(OnlineBookingCheckoutSession checkout)
        {
            var seconds = Math.Max(0, (int)Math.Floor((checkout.ExpiresAtUtc - DateTime.UtcNow).TotalSeconds));
            return new OnlineBookingCheckoutViewModel
            {
                CheckoutToken = checkout.CheckoutToken,
                ArenaName = checkout.ArenaName,
                CustomerName = checkout.CustomerName,
                CustomerCode = checkout.CustomerCode,
                CourtId = checkout.CourtId,
                CourtName = checkout.CourtName,
                SportName = checkout.SportName,
                FacilityName = checkout.FacilityName,
                CurrencySymbol = checkout.CurrencySymbol,
                BookingDate = checkout.BookingDate,
                StartTime = checkout.StartTime,
                EndTime = checkout.EndTime,
                DurationMinutes = checkout.DurationMinutes,
                PlayerCount = checkout.PlayerCount,
                MembershipSummary = checkout.MembershipSummary,
                CustomerNotes = checkout.CustomerNotes,
                SpecialRequest = checkout.SpecialRequest,
                BaseAmount = checkout.BaseAmount,
                MembershipDiscountAmount = checkout.MembershipDiscountAmount,
                TaxAmount = checkout.TaxAmount,
                TotalAmount = checkout.TotalAmount,
                IsPeakRate = checkout.IsPeakRate,
                ExpiresAtUtc = checkout.ExpiresAtUtc,
                SecondsRemaining = seconds,
                IsExpired = seconds <= 0
            };
        }

        private async Task<SystemSettings> GetSettingsAsync()
        {
            return await _dbContext.SystemSettings.AsNoTracking().FirstOrDefaultAsync()
                ?? new SystemSettings { ArenaName = "GameHub Arena", CurrencySymbol = "Rs", TaxPercentage = 0, OpeningTime = new TimeOnly(6, 0), ClosingTime = new TimeOnly(23, 0), SlotDurationMinutes = 60, BufferMinutes = 10, AdvanceBookingDays = 14, AllowWalkIn = true, AllowOnlineBooking = true, BookingPrefix = "BKG", Currency = "PKR", Phone = "", Email = "", InvoicePrefix = "INV", ReceiptPrefix = "RCPT" };
        }

        private static DateOnly NextWeekend(DateOnly today)
        {
            var daysUntilSaturday = ((int)DayOfWeek.Saturday - (int)today.DayOfWeek + 7) % 7;
            return today.AddDays(daysUntilSaturday == 0 ? 7 : daysUntilSaturday);
        }

        private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

        private class PriceResult
        {
            public bool Valid { get; set; }
            public string Message { get; set; } = string.Empty;
            public string ArenaName { get; set; } = "GameHub Arena";
            public string CurrencySymbol { get; set; } = "Rs";
            public decimal BaseAmount { get; set; }
            public decimal MembershipDiscountAmount { get; set; }
            public decimal TaxAmount { get; set; }
            public decimal TotalAmount { get; set; }
            public bool IsPeakRate { get; set; }
            public int? CustomerMembershipId { get; set; }
            public string MembershipMessage { get; set; } = "No active membership.";

            public static PriceResult Fail(SystemSettings settings, string message) => new()
            {
                Valid = false,
                Message = message,
                ArenaName = settings.ArenaName,
                CurrencySymbol = settings.CurrencySymbol ?? "Rs"
            };
        }
    }
}
