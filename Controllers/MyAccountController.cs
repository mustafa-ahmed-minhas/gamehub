using System.Security.Claims;
using System.Text.Json;
using GameHub.Data;
using GameHub.Filters;
using GameHub.Helpers;
using GameHub.Models.Checkout;
using GameHub.Models.Entities;
using GameHub.Models.Enums;
using GameHub.Models.ViewModels;
using GameHub.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GameHub.Controllers
{
    [CustomerAuthorize]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public class MyAccountController : Controller
    {
        private static readonly string[] AllowedImageExtensions = { ".jpg", ".jpeg", ".png", ".webp" };
        private static readonly string[] AllowedImageContentTypes = { "image/jpeg", "image/png", "image/webp" };
        private const long MaxImageSize = 3 * 1024 * 1024;
        private const string CheckoutSessionKey = "GameHub.OnlineBooking.Checkout";

        private readonly ApplicationDbContext _dbContext;
        private readonly IWebHostEnvironment _environment;
        private readonly IBookingLifecycleService _lifecycleService;
        private readonly ICustomerMembershipService _membershipService;

        public MyAccountController(
            ApplicationDbContext dbContext,
            IWebHostEnvironment environment,
            IBookingLifecycleService lifecycleService,
            ICustomerMembershipService membershipService)
        {
            _dbContext = dbContext;
            _environment = environment;
            _lifecycleService = lifecycleService;
            _membershipService = membershipService;
        }

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var customer = await CurrentCustomerQuery().FirstOrDefaultAsync();
            if (customer == null) return RedirectToAction("Login", "Account");

            await _lifecycleService.SynchronizeAsync(customer.Id);
            await _membershipService.SyncExpiredMembershipsAsync();

            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            var upcoming = await _dbContext.Bookings.CountAsync(item => item.CustomerId == customer.Id && item.BookingDate >= today && item.IsActive && item.Status == BookingStatus.Confirmed);
            var completed = await _dbContext.Bookings.CountAsync(item => item.CustomerId == customer.Id && item.Status == BookingStatus.Completed);
            var cancelled = await _dbContext.Bookings.CountAsync(item => item.CustomerId == customer.Id && item.Status == BookingStatus.Cancelled);
            var checkout = GetActiveCheckout(customer.Id);

            var activeMembership = await _dbContext.CustomerMemberships
                .AsNoTracking()
                .Include(x => x.MembershipPlan)
                .Where(x => x.CustomerId == customer.Id && x.IsActive && x.Status == MembershipStatus.Active && x.ExpiryDate >= today)
                .OrderByDescending(x => x.ExpiryDate)
                .FirstOrDefaultAsync();

            return View(new MyAccountViewModel
            {
                FirstName = customer.FirstName,
                FullName = customer.FullName,
                Initials = customer.Initials,
                CustomerCode = customer.CustomerCode,
                Email = customer.Email,
                EmailVerified = customer.EmailVerified,
                MembershipStatus = customer.MembershipStatus,
                LoyaltyPoints = customer.LoyaltyPoints,
                PreferredSports = customer.PreferredSportCodes,
                ProfileImageUrl = customer.ProfileImageUrl,
                UpcomingBookings = upcoming,
                CompletedBookings = completed,
                CancelledBookings = cancelled,
                HasActiveMembership = activeMembership != null,
                MembershipNumber = activeMembership?.MembershipNumber,
                MembershipPlanName = activeMembership?.MembershipPlan.Name,
                MembershipExpiryDate = activeMembership?.ExpiryDate,
                MembershipDaysRemaining = activeMembership == null ? 0 : Math.Max(0, activeMembership.ExpiryDate.DayNumber - today.DayNumber),
                HasActiveCheckout = checkout != null,
                CheckoutCourtId = checkout?.CourtId,
                CheckoutCourtName = checkout?.CourtName,
                CheckoutDateTime = checkout == null ? null : $"{checkout.BookingDate:MMM dd, yyyy} · {checkout.StartTime:hh\\:mm tt} - {checkout.EndTime:hh\\:mm tt}",
                CheckoutExpiresAtUtc = checkout?.ExpiresAtUtc
            });
        }

        [HttpGet]
        public async Task<IActionResult> Profile()
        {
            var customer = await CurrentCustomerQuery().FirstOrDefaultAsync();
            if (customer == null) return RedirectToAction("Login", "Account");
            return View(MapProfile(customer));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Profile(CustomerProfileViewModel model)
        {
            NormalizeProfile(model);
            ValidateProfile(model);

            var customer = await CurrentCustomerQuery(tracked: true).FirstOrDefaultAsync();
            if (customer == null) return RedirectToAction("Login", "Account");

            var duplicatePhone = await _dbContext.Customers.AnyAsync(item => item.Id != customer.Id && item.PrimaryPhone == model.PrimaryPhone);
            if (duplicatePhone)
            {
                ModelState.AddModelError(nameof(model.PrimaryPhone), "This phone number is already in use.");
            }

            if (!ModelState.IsValid)
            {
                model.ProfileImageUrl = customer.ProfileImageUrl;
                return View(model);
            }

            var imageResult = await SaveProfileImageAsync(model.ProfileImage);
            if (!imageResult.Success)
            {
                ModelState.AddModelError(nameof(model.ProfileImage), imageResult.Message);
                model.ProfileImageUrl = customer.ProfileImageUrl;
                return View(model);
            }

            customer.FirstName = model.FirstName;
            customer.LastName = model.LastName;
            customer.PreferredName = model.PreferredName;
            customer.PrimaryPhone = model.PrimaryPhone;
            customer.WhatsAppNumber = model.WhatsAppNumber;
            customer.DateOfBirth = model.DateOfBirth;
            customer.Gender = model.Gender;
            customer.AddressLine1 = model.AddressLine1;
            customer.AddressLine2 = model.AddressLine2;
            customer.City = model.City;
            customer.StateProvince = model.StateProvince;
            customer.PostalCode = model.PostalCode;
            customer.Country = model.Country;
            customer.PreferredContactMethod = model.PreferredContactMethod;
            customer.ReceiveBookingReminders = model.ReceiveBookingReminders;
            customer.ReceiveMarketingMessages = model.ReceiveMarketingMessages;
            customer.UpdatedAt = DateTime.UtcNow;
            if (!string.IsNullOrWhiteSpace(imageResult.Url))
            {
                customer.ProfileImageUrl = imageResult.Url;
            }

            await _dbContext.SaveChangesAsync();
            TempData.SetToast("success", "Profile Updated", "Your profile has been updated successfully.");
            return RedirectToAction(nameof(Profile));
        }

        [HttpGet]
        public IActionResult ChangePassword()
        {
            return View(new CustomerChangePasswordViewModel());
        }

        [HttpGet]
        public async Task<IActionResult> Payments()
        {
            var customer = await CurrentCustomerQuery().FirstOrDefaultAsync();
            if (customer == null) return RedirectToAction("Login", "Account");
            var payments = await _dbContext.PaymentTransactions.AsNoTracking()
                .Include(x => x.Booking)
                .Where(x => x.CustomerId == customer.Id)
                .OrderByDescending(x => x.CreatedAt)
                .ToListAsync();
            return View(payments);
        }

        [HttpGet]
        public async Task<IActionResult> Invoices(string tab = "all")
        {
            var customer = await CurrentCustomerQuery().FirstOrDefaultAsync();
            if (customer == null) return RedirectToAction("Login", "Account");
            return RedirectToAction("Index", "MyInvoices", new { tab });
        }

        [HttpGet]
        public async Task<IActionResult> History(string tab = "all")
        {
            var customer = await CurrentCustomerQuery().FirstOrDefaultAsync();
            if (customer == null) return RedirectToAction("Login", "Account");
            return RedirectToAction("Index", "CustomerHistory", new { tab });
        }

        [HttpGet]
        public async Task<IActionResult> Inquiries()
        {
            var customer = await CurrentCustomerQuery().FirstOrDefaultAsync();
            if (customer == null) return RedirectToAction("Login", "Account");
            var inquiries = await _dbContext.Leads.AsNoTracking()
                .Where(x => x.CustomerId == customer.Id || x.SubmittedByCustomerId == customer.Id)
                .OrderByDescending(x => x.CreatedAt)
                .ToListAsync();
            return View(inquiries);
        }

        [HttpGet]
        public async Task<IActionResult> Quotations()
        {
            var customer = await CurrentCustomerQuery().FirstOrDefaultAsync();
            if (customer == null) return RedirectToAction("Login", "Account");
            await SynchronizeCustomerExpiredQuotationsAsync(customer.Id);
            var quotations = await _dbContext.SalesQuotations.AsNoTracking()
                .Include(x => x.Opportunity)
                .Where(x => x.CustomerId == customer.Id && x.IsActive)
                .OrderByDescending(x => x.CreatedAt)
                .ToListAsync();
            return View(quotations);
        }

        [HttpGet]
        public async Task<IActionResult> QuotationDetails(int id)
        {
            var customer = await CurrentCustomerQuery().FirstOrDefaultAsync();
            if (customer == null) return RedirectToAction("Login", "Account");
            await SynchronizeCustomerExpiredQuotationsAsync(customer.Id, id);
            var quotation = await _dbContext.SalesQuotations.AsNoTracking()
                .Include(x => x.Customer)
                .Include(x => x.Opportunity)
                .Include(x => x.Items)
                .FirstOrDefaultAsync(x => x.Id == id && x.CustomerId == customer.Id && x.IsActive);
            if (quotation == null) return RedirectToAction("NotFound", "Error");
            return View(quotation);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AcceptQuotation(int id, bool acceptTerms)
        {
            var customer = await CurrentCustomerQuery().FirstOrDefaultAsync();
            if (customer == null) return RedirectToAction("Login", "Account");
            if (!acceptTerms)
            {
                TempData.SetToast("warning", "Confirmation Required", "Please confirm that you reviewed and accept the quotation terms.");
                return RedirectToAction(nameof(QuotationDetails), new { id });
            }

            var quotation = await _dbContext.SalesQuotations.FirstOrDefaultAsync(x => x.Id == id && x.CustomerId == customer.Id && x.IsActive);
            if (quotation == null) return RedirectToAction("NotFound", "Error");
            if (quotation.Status is not (SalesQuotationStatus.Sent or SalesQuotationStatus.Viewed)) return RedirectToAction("NotFound", "Error");
            quotation.Status = SalesQuotationStatus.Accepted;
            quotation.AcceptedAt = DateTime.UtcNow;
            quotation.UpdatedAt = DateTime.UtcNow;
            _dbContext.OpportunityActivities.Add(new OpportunityActivity { OpportunityId = quotation.OpportunityId, ActivityType = OpportunityActivityType.Proposal, Subject = "Quotation accepted by customer", Description = $"{quotation.QuotationNumber} accepted through customer portal.", ActivityDate = DateTime.UtcNow, CreatedAt = DateTime.UtcNow, CreatedByUserId = quotation.CreatedByUserId, IsCompleted = true });
            await _dbContext.SaveChangesAsync();
            TempData.SetToast("success", "Quotation Accepted", "Your quotation has been accepted.");
            return RedirectToAction(nameof(QuotationDetails), new { id });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RejectQuotation(int id, string reason)
        {
            var customer = await CurrentCustomerQuery().FirstOrDefaultAsync();
            if (customer == null) return RedirectToAction("Login", "Account");
            var quotation = await _dbContext.SalesQuotations.FirstOrDefaultAsync(x => x.Id == id && x.CustomerId == customer.Id && x.IsActive);
            if (quotation == null) return RedirectToAction("NotFound", "Error");
            if (quotation.Status is not (SalesQuotationStatus.Sent or SalesQuotationStatus.Viewed)) return RedirectToAction("NotFound", "Error");
            if (string.IsNullOrWhiteSpace(reason))
            {
                TempData.SetToast("warning", "Reason Required", "Please share a short rejection reason.");
                return RedirectToAction(nameof(QuotationDetails), new { id });
            }

            quotation.Status = SalesQuotationStatus.Rejected;
            quotation.RejectedAt = DateTime.UtcNow;
            quotation.RejectionReason = reason.Trim();
            quotation.UpdatedAt = DateTime.UtcNow;
            _dbContext.OpportunityActivities.Add(new OpportunityActivity { OpportunityId = quotation.OpportunityId, ActivityType = OpportunityActivityType.Proposal, Subject = "Quotation rejected by customer", Description = $"{quotation.QuotationNumber} rejected. Reason: {quotation.RejectionReason}", ActivityDate = DateTime.UtcNow, CreatedAt = DateTime.UtcNow, CreatedByUserId = quotation.CreatedByUserId, IsCompleted = true });
            await _dbContext.SaveChangesAsync();
            TempData.SetToast("info", "Quotation Rejected", "Your response has been recorded.");
            return RedirectToAction(nameof(QuotationDetails), new { id });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ChangePassword(CustomerChangePasswordViewModel model)
        {
            var customer = await CurrentCustomerQuery(tracked: true).FirstOrDefaultAsync();
            if (customer == null || string.IsNullOrWhiteSpace(customer.PasswordHash)) return RedirectToAction("Login", "Account");

            await ValidatePasswordPolicyAsync(model.NewPassword, nameof(model.NewPassword));

            if (!CustomerPasswordHelper.VerifyPassword(customer, customer.PasswordHash, model.CurrentPassword))
            {
                ModelState.AddModelError(nameof(model.CurrentPassword), "Current password is incorrect.");
            }

            if (!ModelState.IsValid)
            {
                model.CurrentPassword = string.Empty;
                model.NewPassword = string.Empty;
                model.ConfirmNewPassword = string.Empty;
                return View(model);
            }

            customer.PasswordHash = CustomerPasswordHelper.HashPassword(customer, model.NewPassword);
            customer.PasswordChangedAt = DateTime.UtcNow;
            customer.UpdatedAt = DateTime.UtcNow;
            await _dbContext.SaveChangesAsync();

            TempData.SetToast("success", "Password Changed", "Your password has been updated securely.");
            return RedirectToAction(nameof(Index));
        }

        private IQueryable<Customer> CurrentCustomerQuery(bool tracked = false)
        {
            var idValue = User.FindFirstValue(ClaimTypes.NameIdentifier);
            _ = int.TryParse(idValue, out var customerId);
            var query = _dbContext.Customers.Where(item => item.Id == customerId && item.HasOnlineAccount);
            return tracked ? query : query.AsNoTracking();
        }

        private static CustomerProfileViewModel MapProfile(Customer customer)
        {
            return new CustomerProfileViewModel
            {
                FirstName = customer.FirstName,
                LastName = customer.LastName,
                PreferredName = customer.PreferredName,
                PrimaryPhone = customer.PrimaryPhone,
                WhatsAppNumber = customer.WhatsAppNumber,
                DateOfBirth = customer.DateOfBirth,
                Gender = customer.Gender,
                AddressLine1 = customer.AddressLine1,
                AddressLine2 = customer.AddressLine2,
                City = customer.City,
                StateProvince = customer.StateProvince,
                PostalCode = customer.PostalCode,
                Country = customer.Country,
                PreferredContactMethod = customer.PreferredContactMethod,
                ReceiveBookingReminders = customer.ReceiveBookingReminders,
                ReceiveMarketingMessages = customer.ReceiveMarketingMessages,
                ProfileImageUrl = customer.ProfileImageUrl
            };
        }

        private void ValidateProfile(CustomerProfileViewModel model)
        {
            if (model.DateOfBirth.HasValue && model.DateOfBirth.Value > DateOnly.FromDateTime(DateTime.UtcNow))
            {
                ModelState.AddModelError(nameof(model.DateOfBirth), "Date of birth cannot be in the future.");
            }
        }

        private async Task ValidatePasswordPolicyAsync(string password, string fieldName)
        {
            var settings = await _dbContext.SystemSettings.AsNoTracking().FirstOrDefaultAsync();
            var minimumLength = Math.Max(settings?.PasswordMinLength ?? 8, 8);
            if (password.Length < minimumLength)
            {
                ModelState.AddModelError(fieldName, $"Password must be at least {minimumLength} characters.");
            }

            if (settings?.RequireStrongPassword != false &&
                (!password.Any(char.IsUpper) ||
                 !password.Any(char.IsLower) ||
                 !password.Any(char.IsDigit) ||
                 !password.Any(ch => !char.IsLetterOrDigit(ch))))
            {
                ModelState.AddModelError(fieldName, "Password must include uppercase, lowercase, number and special character.");
            }
        }

        private async Task<(bool Success, string? Url, string Message)> SaveProfileImageAsync(IFormFile? file)
        {
            if (file == null || file.Length == 0) return (true, null, string.Empty);
            if (file.Length > MaxImageSize) return (false, null, "Image must be 3 MB or smaller.");

            var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (!AllowedImageExtensions.Contains(extension) || !AllowedImageContentTypes.Contains(file.ContentType))
            {
                return (false, null, "Use a JPG, PNG or WEBP image.");
            }

            var folder = Path.Combine(_environment.WebRootPath, "uploads", "customers");
            Directory.CreateDirectory(folder);
            var fileName = $"{Guid.NewGuid():N}{extension}";
            var path = Path.Combine(folder, fileName);
            await using var stream = System.IO.File.Create(path);
            await file.CopyToAsync(stream);
            return (true, $"/uploads/customers/{fileName}", string.Empty);
        }

        private static void NormalizeProfile(CustomerProfileViewModel model)
        {
            model.FirstName = model.FirstName?.Trim() ?? string.Empty;
            model.LastName = model.LastName?.Trim() ?? string.Empty;
            model.PreferredName = string.IsNullOrWhiteSpace(model.PreferredName) ? null : model.PreferredName.Trim();
            model.PrimaryPhone = NormalizePhone(model.PrimaryPhone);
            model.WhatsAppNumber = string.IsNullOrWhiteSpace(model.WhatsAppNumber) ? null : NormalizePhone(model.WhatsAppNumber);
            model.AddressLine1 = string.IsNullOrWhiteSpace(model.AddressLine1) ? null : model.AddressLine1.Trim();
            model.AddressLine2 = string.IsNullOrWhiteSpace(model.AddressLine2) ? null : model.AddressLine2.Trim();
            model.City = string.IsNullOrWhiteSpace(model.City) ? null : model.City.Trim();
            model.StateProvince = string.IsNullOrWhiteSpace(model.StateProvince) ? null : model.StateProvince.Trim();
            model.PostalCode = string.IsNullOrWhiteSpace(model.PostalCode) ? null : model.PostalCode.Trim();
            model.Country = string.IsNullOrWhiteSpace(model.Country) ? "Pakistan" : model.Country.Trim();
        }

        private static string NormalizePhone(string? value)
        {
            if (string.IsNullOrWhiteSpace(value)) return string.Empty;
            var trimmed = value.Trim();
            var prefix = trimmed.StartsWith("+") ? "+" : string.Empty;
            return prefix + new string(trimmed.Where(char.IsDigit).ToArray());
        }

        private OnlineBookingCheckoutSession? GetActiveCheckout(int customerId)
        {
            var json = HttpContext.Session.GetString(CheckoutSessionKey);
            if (string.IsNullOrWhiteSpace(json)) return null;
            var checkout = JsonSerializer.Deserialize<OnlineBookingCheckoutSession>(json);
            return checkout != null && checkout.CustomerId == customerId && checkout.ExpiresAtUtc > DateTime.UtcNow ? checkout : null;
        }

        private async Task SynchronizeCustomerExpiredQuotationsAsync(int customerId, int? id = null)
        {
            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            var query = _dbContext.SalesQuotations.Where(x => x.CustomerId == customerId && (x.Status == SalesQuotationStatus.Sent || x.Status == SalesQuotationStatus.Viewed) && x.ValidUntil < today);
            if (id.HasValue) query = query.Where(x => x.Id == id.Value);
            var expired = await query.ToListAsync();
            if (!expired.Any()) return;
            foreach (var quotation in expired)
            {
                quotation.Status = SalesQuotationStatus.Expired;
                quotation.ExpiredAt = DateTime.UtcNow;
                quotation.UpdatedAt = DateTime.UtcNow;
            }
            await _dbContext.SaveChangesAsync();
        }
    }
}
