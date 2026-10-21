using System.Collections.Concurrent;
using System.Security.Claims;
using GameHub.Data;
using GameHub.Helpers;
using GameHub.Models.Entities;
using GameHub.Models.Enums;
using GameHub.Models.ViewModels;
using GameHub.Services.Interfaces;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace GameHub.Controllers
{
    /// <summary>
    /// Public website inquiry endpoint. Submissions are stored as CRM Leads with
    /// Source = WebsiteInquiry / IsWebsiteInquiry = true, exactly like inquiries
    /// created manually in the ERP (CRMController.CreateLead), so they appear in
    /// the existing ERP Inquiry/Lead Management screen and workflow.
    /// Validation, normalization and business rules are shared with the ERP via
    /// <see cref="LeadInquiryRules"/>. Supports both full-page form posts (the
    /// standalone /Inquiry page) and AJAX fetch submissions (the home page
    /// section) by detecting the X-Requested-With header and returning JSON for
    /// the latter.
    /// </summary>
    [AllowAnonymous]
    public class InquiryController : Controller
    {
        private readonly ApplicationDbContext _dbContext;
        private readonly ILogger<InquiryController> _logger;
        private readonly IWebsiteInquiryNotifier _websiteInquiryNotifier;

        // Lightweight in-memory per-IP sliding-window rate limit for the public
        // endpoint. No external dependencies; appropriate for this app's scale.
        private static readonly ConcurrentDictionary<string, Queue<DateTime>> SubmissionsByIp = new(StringComparer.Ordinal);
        private const int RateLimitWindowMinutes = 10;
        private const int RateLimitMaxSubmissions = 5;

        public InquiryController(ApplicationDbContext dbContext, ILogger<InquiryController> logger, IWebsiteInquiryNotifier websiteInquiryNotifier)
        {
            _dbContext = dbContext;
            _logger = logger;
            _websiteInquiryNotifier = websiteInquiryNotifier;
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
            var isAjax = IsAjaxRequest();
            var clientIp = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";

            // Honeypot: real users never fill this hidden field. Silently drop
            // without saving, and return a plausible-looking success so bots
            // cannot tell they were detected.
            if (!string.IsNullOrWhiteSpace(model.WebsiteUrl))
            {
                _logger.LogWarning("Public inquiry honeypot triggered from {Ip}. Submission dropped.", clientIp);
                RecordSubmission(clientIp);
                return RespondFakeSuccess(isAjax);
            }

            if (IsRateLimited(clientIp))
            {
                const string rateMessage = "Too many inquiries from your network. Please wait a few minutes and try again.";
                _logger.LogWarning("Public inquiry rate limit exceeded for {Ip}.", clientIp);
                if (isAjax) return StatusCode(StatusCodes.Status429TooManyRequests, new { success = false, message = rateMessage, errors = (Dictionary<string, string[]>?)null });
                ModelState.AddModelError(string.Empty, rateMessage);
                return View(await AddOptionsAsync(model));
            }

            Normalize(model);
            await ValidateInquiryAsync(model);
            if (!ModelState.IsValid)
            {
                if (isAjax) return BadRequest(new { success = false, message = SummaryMessage(), errors = ExtractAjaxErrors() });
                return View(await AddOptionsAsync(model));
            }

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
                IsWebsiteInquiry = true,
                PublicReferenceNumber = await GeneratePublicReferenceAsync(),
                CreatedAt = DateTime.UtcNow,
                IsActive = true
            };

            try
            {
                _dbContext.Leads.Add(lead);
                await _dbContext.SaveChangesAsync();
            }
            catch (DbUpdateException ex) when (IsDuplicateKey(ex))
            {
                // Rare race: two concurrent submissions generated the same
                // LeadNumber (unique index). Detach and retry once with a fresh
                // number instead of surfacing a database error to the visitor.
                _logger.LogWarning(ex, "LeadNumber collision on public inquiry; retrying once.");
                _dbContext.Entry(lead).State = EntityState.Detached;
                lead.LeadNumber = await GenerateLeadNumberAsync();
                lead.PublicReferenceNumber = await GeneratePublicReferenceAsync();
                _dbContext.Leads.Add(lead);
                try
                {
                    await _dbContext.SaveChangesAsync();
                }
                catch (Exception retryEx)
                {
                    _logger.LogError(retryEx, "Public inquiry could not be saved after retry.");
                    _dbContext.Entry(lead).State = EntityState.Detached;
                    return await RespondServerError(isAjax, model);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Public inquiry could not be saved.");
                _dbContext.Entry(lead).State = EntityState.Detached;
                return await RespondServerError(isAjax, model);
            }

            // Non-fatal: the lead itself is already saved. A failure to record
            // the follow-up note should not discard the inquiry or surface a 500.
            try
            {
                _dbContext.LeadActivities.Add(new LeadActivity { LeadId = lead.Id, ActivityType = LeadActivityType.Note, Subject = "Website inquiry received", Description = "Public inquiry submitted from website.", ActivityDate = DateTime.UtcNow, CreatedAt = DateTime.UtcNow, CreatedByUserId = await FallbackUserIdAsync(), IsCompleted = true });
                await _dbContext.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to record lead activity for public inquiry {LeadId}.", lead.Id);
            }

            RecordSubmission(clientIp);

            // Real-time only after the inquiry is committed: a single broadcast
            // per successful save (the retry path above exits on failure), so
            // ERP tabs are never told about an inquiry that was not persisted.
            await NotifyNewInquiryAsync(lead);

            if (isAjax) return Ok(new { success = true, reference = lead.PublicReferenceNumber, message = "Your inquiry has been submitted successfully." });
            TempData.SetToast("success", "Inquiry Received", "Your inquiry has been submitted successfully.");
            return RedirectToAction(nameof(Success), new { reference = lead.PublicReferenceNumber });
        }

        [HttpGet("/Inquiry/Success")]
        public IActionResult Success(string reference)
        {
            ViewBag.Reference = reference;
            return View();
        }

        /// <summary>
        /// Server-side validation and business rules. Mirrors the ERP manual
        /// lead-creation rules (CRMController.ValidateLeadAsync) so website
        /// inquiries behave exactly like inquiries created in the ERP: at least
        /// one contact method, .com email format, 0XXXXXXXXX phone format, and a
        /// preferred date that is not in the past. Plus a strict duplicate check
        /// for the public endpoint.
        /// </summary>
        private async Task ValidateInquiryAsync(PublicInquiryViewModel model)
        {
            if (!LeadInquiryRules.HasContact(model.Email, model.Phone, model.WhatsAppNumber))
            {
                ModelState.AddModelError(string.Empty, "Please provide at least one contact method (email, phone or WhatsApp).");
            }

            if (!string.IsNullOrWhiteSpace(model.Email) && !LeadInquiryRules.HasValidComEmail(model.Email))
            {
                ModelState.AddModelError(nameof(model.Email), LeadInquiryRules.EmailFormatMessage);
            }

            if (!string.IsNullOrWhiteSpace(model.Phone) && !LeadInquiryRules.HasValidPakPhone(model.Phone))
            {
                ModelState.AddModelError(nameof(model.Phone), LeadInquiryRules.PhoneFormatMessage);
            }

            if (!string.IsNullOrWhiteSpace(model.WhatsAppNumber) && !LeadInquiryRules.HasValidPakPhone(model.WhatsAppNumber))
            {
                ModelState.AddModelError(nameof(model.WhatsAppNumber), LeadInquiryRules.PhoneFormatMessage);
            }

            if (model.PreferredDate.HasValue && model.PreferredDate.Value < DateOnly.FromDateTime(DateTime.UtcNow))
            {
                ModelState.AddModelError(nameof(model.PreferredDate), LeadInquiryRules.PreferredDatePastMessage);
            }

            if (model.SportId.HasValue)
            {
                var sportExists = await _dbContext.Sports.AsNoTracking().AnyAsync(x => x.Id == model.SportId.Value && x.IsActive);
                if (!sportExists) ModelState.AddModelError(nameof(model.SportId), "The selected sport is no longer available.");
            }

            var recentSince = DateTime.UtcNow.AddMinutes(-10);
            var duplicate = await _dbContext.Leads.AnyAsync(x => x.IsWebsiteInquiry && x.CreatedAt >= recentSince && x.InquirySubject == model.InquirySubject && ((model.Email != null && x.Email == model.Email) || (model.Phone != null && x.Phone == model.Phone) || (model.WhatsAppNumber != null && x.WhatsAppNumber == model.WhatsAppNumber)));
            if (duplicate) ModelState.AddModelError(string.Empty, "A similar inquiry was submitted recently. Please wait before submitting again.");
        }

        private async Task<PublicInquiryViewModel> AddOptionsAsync(PublicInquiryViewModel model)
        {
            model.SportOptions = await _dbContext.Sports.AsNoTracking().Where(x => x.IsActive).OrderBy(x => x.DisplayOrder).Select(x => new SelectListItem(x.Name, x.Id.ToString(), x.Id == model.SportId)).ToListAsync();
            return model;
        }

        private static void Normalize(PublicInquiryViewModel model)
        {
            model.FullName = LeadInquiryRules.Clean(model.FullName) ?? string.Empty;
            model.Email = LeadInquiryRules.NormalizeEmail(model.Email);
            model.Phone = LeadInquiryRules.NormalizePhone(model.Phone);
            model.WhatsAppNumber = LeadInquiryRules.NormalizePhone(model.WhatsAppNumber);
            model.InquirySubject = LeadInquiryRules.Clean(model.InquirySubject) ?? string.Empty;
            model.InquiryDetails = LeadInquiryRules.SanitizeFreeText(model.InquiryDetails, allowMultiline: true, maxLength: 2000);
        }

        private async Task<string> GenerateLeadNumberAsync()
        {
            var settings = await _dbContext.SystemSettings.AsNoTracking().FirstOrDefaultAsync();
            var prefix = string.IsNullOrWhiteSpace(settings?.LeadPrefix) ? "LEAD" : settings.LeadPrefix.Trim().ToUpperInvariant();
            var latest = await _dbContext.Leads.Where(x => x.LeadNumber.StartsWith(prefix + "-")).OrderByDescending(x => x.LeadNumber).Select(x => x.LeadNumber).FirstOrDefaultAsync();
            var next = !string.IsNullOrWhiteSpace(latest) && int.TryParse(latest.Split('-').Last(), out var n) ? n + 1 : 1;
            return $"{prefix}-{next:000000}";
        }

        /// <summary>
        /// Generates the customer-facing INQ reference. Uses the highest existing
        /// INQ number (not a count) so deletions cannot cause a duplicate.
        /// </summary>
        private async Task<string> GeneratePublicReferenceAsync()
        {
            var latest = await _dbContext.Leads.AsNoTracking()
                .Where(x => x.IsWebsiteInquiry && x.PublicReferenceNumber != null && x.PublicReferenceNumber.StartsWith("INQ-"))
                .OrderByDescending(x => x.PublicReferenceNumber)
                .Select(x => x.PublicReferenceNumber)
                .FirstOrDefaultAsync();
            var next = !string.IsNullOrWhiteSpace(latest) && int.TryParse(latest.Split('-').Last(), out var n) ? n + 1 : 1;
            return $"INQ-{next:000000}";
        }

        private async Task<int> FallbackUserIdAsync() => await _dbContext.Users.Select(x => x.Id).FirstOrDefaultAsync();

        /// <summary>
        /// Pushes the "new website inquiry" real-time event. The lead is already
        /// saved, so any failure here is logged and ignored rather than turning a
        /// completed inquiry into an error.
        /// </summary>
        private async Task NotifyNewInquiryAsync(Lead lead)
        {
            try
            {
                await _websiteInquiryNotifier.BroadcastNewInquiryAsync(lead);
            }
            catch (Exception exception)
            {
                _logger.LogWarning(exception, "Real-time notification failed for website inquiry {LeadId}.", lead.Id);
            }
        }

        // ---- AJAX/JSON plumbing -------------------------------------------------

        private bool IsAjaxRequest() => string.Equals(Request.Headers["X-Requested-With"], "XMLHttpRequest", StringComparison.OrdinalIgnoreCase);

        private string SummaryMessage()
        {
            if (ModelState.TryGetValue(string.Empty, out var modelErrors) && modelErrors.Errors.Count > 0)
            {
                var first = modelErrors.Errors.Select(e => e.ErrorMessage).FirstOrDefault(m => !string.IsNullOrWhiteSpace(m));
                if (!string.IsNullOrWhiteSpace(first)) return first;
            }
            return "Please review the highlighted fields and try again.";
        }

        private Dictionary<string, string[]> ExtractAjaxErrors()
        {
            var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);
            foreach (var entry in ModelState)
            {
                var messages = entry.Value?.Errors?.Select(e => e.ErrorMessage).Where(m => !string.IsNullOrWhiteSpace(m)).ToArray() ?? Array.Empty<string>();
                if (messages.Length > 0 && !string.IsNullOrEmpty(entry.Key)) errors[entry.Key] = messages;
            }
            return errors;
        }

        private async Task<IActionResult> RespondServerError(bool isAjax, PublicInquiryViewModel model)
        {
            const string message = "We could not submit your inquiry right now. Please try again in a moment.";
            if (isAjax) return StatusCode(StatusCodes.Status500InternalServerError, new { success = false, message, errors = (Dictionary<string, string[]>?)null });
            ModelState.AddModelError(string.Empty, message);
            return View(await AddOptionsAsync(model));
        }

        private IActionResult RespondFakeSuccess(bool isAjax)
        {
            if (isAjax) return Ok(new { success = true, reference = (string?)null, message = "Your inquiry has been submitted successfully." });
            return RedirectToAction(nameof(Success), new { reference = (string?)null });
        }

        // ---- Spam / abuse protection --------------------------------------------

        private static bool IsDuplicateKey(DbUpdateException exception)
        {
            var message = exception.GetBaseException().Message;
            return message.Contains("UNIQUE", StringComparison.OrdinalIgnoreCase)
                || message.Contains("duplicate key", StringComparison.OrdinalIgnoreCase)
                || message.Contains("Cannot insert duplicate", StringComparison.OrdinalIgnoreCase);
        }

        private bool IsRateLimited(string clientIp)
        {
            var now = DateTime.UtcNow;
            if (!SubmissionsByIp.TryGetValue(clientIp, out var queue)) return false;
            lock (queue)
            {
                while (queue.Count > 0 && queue.Peek() < now.AddMinutes(-RateLimitWindowMinutes)) queue.Dequeue();
                return queue.Count >= RateLimitMaxSubmissions;
            }
        }

        private void RecordSubmission(string clientIp)
        {
            var queue = SubmissionsByIp.GetOrAdd(clientIp, _ => new Queue<DateTime>());
            lock (queue) queue.Enqueue(DateTime.UtcNow);

            // Opportunistic cleanup so the map cannot grow without bound.
            if (SubmissionsByIp.Count <= 1000) return;
            var cutoff = DateTime.UtcNow.AddMinutes(-RateLimitWindowMinutes);
            foreach (var key in SubmissionsByIp.Keys.ToArray())
            {
                if (!SubmissionsByIp.TryGetValue(key, out var q)) continue;
                lock (q)
                {
                    while (q.Count > 0 && q.Peek() < cutoff) q.Dequeue();
                    if (q.Count == 0) SubmissionsByIp.TryRemove(key, out _);
                }
            }
        }
    }
}
