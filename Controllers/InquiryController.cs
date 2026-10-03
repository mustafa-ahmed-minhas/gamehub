using System.Security.Claims;
using GameHub.Data;
using GameHub.Helpers;
using GameHub.Models.Entities;
using GameHub.Models.Enums;
using GameHub.Models.ViewModels;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace GameHub.Controllers
{
    [AllowAnonymous]
    public class InquiryController : Controller
    {
        private readonly ApplicationDbContext _dbContext;

        public InquiryController(ApplicationDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        [HttpGet("/Inquiry")]
        public async Task<IActionResult> Index()
        {
            var model = new PublicInquiryViewModel();
            var auth = await HttpContext.AuthenticateAsync(AuthConstants.CustomerScheme);
            if (auth.Succeeded && int.TryParse(auth.Principal?.FindFirstValue(ClaimTypes.NameIdentifier), out var customerId))
            {
                var customer = await _dbContext.Customers.AsNoTracking().FirstOrDefaultAsync(x => x.Id == customerId);
                if (customer != null)
                {
                    model.FullName = customer.FullName;
                    model.Email = customer.Email;
                    model.Phone = customer.PrimaryPhone;
                    model.WhatsAppNumber = customer.WhatsAppNumber;
                }
            }
            return View(await AddOptionsAsync(model));
        }

        [HttpPost("/Inquiry")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Index(PublicInquiryViewModel model)
        {
            Normalize(model);
            await ValidateInquiryAsync(model);
            if (!ModelState.IsValid) return View(await AddOptionsAsync(model));

            var auth = await HttpContext.AuthenticateAsync(AuthConstants.CustomerScheme);
            int? submittedBy = null;
            Customer? linkedCustomer = null;
            if (auth.Succeeded && int.TryParse(auth.Principal?.FindFirstValue(ClaimTypes.NameIdentifier), out var customerId))
            {
                submittedBy = customerId;
                linkedCustomer = await _dbContext.Customers.FirstOrDefaultAsync(x => x.Id == customerId);
            }
            linkedCustomer ??= await _dbContext.Customers.FirstOrDefaultAsync(x => (!string.IsNullOrWhiteSpace(model.Email) && x.Email == model.Email) || (!string.IsNullOrWhiteSpace(model.Phone) && x.PrimaryPhone == model.Phone));

            var lead = new Lead
            {
                LeadNumber = await GenerateLeadNumberAsync(),
                CustomerId = linkedCustomer?.Id,
                SubmittedByCustomerId = submittedBy,
                CustomerName = model.FullName,
                Email = model.Email,
                Phone = model.Phone,
                WhatsAppNumber = model.WhatsAppNumber,
                Source = LeadSource.WebsiteInquiry,
                ServiceInterest = model.ServiceInterest,
                SportId = model.SportId,
                PreferredDate = model.PreferredDate,
                PreferredStartTime = model.PreferredTime,
                ExpectedPlayerCount = model.PlayerCount,
                InquirySubject = model.InquirySubject,
                InquiryDetails = model.InquiryDetails,
                Status = LeadStatus.New,
                Priority = LeadPriority.Medium,
                Temperature = LeadTemperature.Warm,
                IsWebsiteInquiry = true,
                PublicReferenceNumber = await GeneratePublicReferenceAsync(),
                CreatedAt = DateTime.UtcNow,
                IsActive = true
            };
            _dbContext.Leads.Add(lead);
            await _dbContext.SaveChangesAsync();
            _dbContext.LeadActivities.Add(new LeadActivity { LeadId = lead.Id, ActivityType = LeadActivityType.Note, Subject = "Website inquiry received", Description = "Public inquiry submitted from website.", ActivityDate = DateTime.UtcNow, CreatedAt = DateTime.UtcNow, CreatedByUserId = await FallbackUserIdAsync(), IsCompleted = true });
            await _dbContext.SaveChangesAsync();
            TempData.SetToast("success", "Inquiry Received", "Your inquiry has been submitted successfully.");
            return RedirectToAction(nameof(Success), new { reference = lead.PublicReferenceNumber });
        }

        [HttpGet("/Inquiry/Success")]
        public IActionResult Success(string reference)
        {
            ViewBag.Reference = reference;
            return View();
        }

        private async Task ValidateInquiryAsync(PublicInquiryViewModel model)
        {
            if (string.IsNullOrWhiteSpace(model.Email) && string.IsNullOrWhiteSpace(model.Phone)) ModelState.AddModelError(string.Empty, "Please provide email or phone.");
            if (model.PreferredDate.HasValue && model.PreferredDate.Value < DateOnly.FromDateTime(DateTime.UtcNow)) ModelState.AddModelError(nameof(model.PreferredDate), "Preferred date cannot be in the past.");
            var recentSince = DateTime.UtcNow.AddMinutes(-10);
            var duplicate = await _dbContext.Leads.AnyAsync(x => x.IsWebsiteInquiry && x.CreatedAt >= recentSince && x.InquirySubject == model.InquirySubject && ((model.Email != null && x.Email == model.Email) || (model.Phone != null && x.Phone == model.Phone)));
            if (duplicate) ModelState.AddModelError(string.Empty, "A similar inquiry was submitted recently. Please wait before submitting again.");
        }

        private async Task<PublicInquiryViewModel> AddOptionsAsync(PublicInquiryViewModel model)
        {
            model.SportOptions = await _dbContext.Sports.AsNoTracking().Where(x => x.IsActive).OrderBy(x => x.DisplayOrder).Select(x => new SelectListItem(x.Name, x.Id.ToString(), x.Id == model.SportId)).ToListAsync();
            return model;
        }

        private static void Normalize(PublicInquiryViewModel model)
        {
            model.FullName = model.FullName?.Trim() ?? string.Empty;
            model.Email = string.IsNullOrWhiteSpace(model.Email) ? null : model.Email.Trim().ToLowerInvariant();
            model.Phone = NormalizePhone(model.Phone);
            model.WhatsAppNumber = NormalizePhone(model.WhatsAppNumber);
            model.InquirySubject = model.InquirySubject?.Trim() ?? string.Empty;
            model.InquiryDetails = string.IsNullOrWhiteSpace(model.InquiryDetails) ? null : model.InquiryDetails.Trim();
        }

        private static string? NormalizePhone(string? value)
        {
            if (string.IsNullOrWhiteSpace(value)) return null;
            var prefix = value.Trim().StartsWith("+") ? "+" : "";
            return prefix + new string(value.Where(char.IsDigit).ToArray());
        }

        private async Task<string> GenerateLeadNumberAsync()
        {
            var settings = await _dbContext.SystemSettings.AsNoTracking().FirstOrDefaultAsync();
            var prefix = string.IsNullOrWhiteSpace(settings?.LeadPrefix) ? "LEAD" : settings.LeadPrefix.Trim().ToUpperInvariant();
            var latest = await _dbContext.Leads.Where(x => x.LeadNumber.StartsWith(prefix + "-")).OrderByDescending(x => x.LeadNumber).Select(x => x.LeadNumber).FirstOrDefaultAsync();
            var next = !string.IsNullOrWhiteSpace(latest) && int.TryParse(latest.Split('-').Last(), out var n) ? n + 1 : 1;
            return $"{prefix}-{next:000000}";
        }

        private async Task<string> GeneratePublicReferenceAsync()
        {
            var next = await _dbContext.Leads.CountAsync(x => x.IsWebsiteInquiry) + 1;
            return $"INQ-{next:000000}";
        }

        private async Task<int> FallbackUserIdAsync() => await _dbContext.Users.Select(x => x.Id).FirstOrDefaultAsync();
    }
}
