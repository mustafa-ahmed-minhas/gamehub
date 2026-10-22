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

namespace GameHub.Controllers
{
    [GameHubAuthorize(UserRole.SuperAdmin, UserRole.Admin, UserRole.BookingManager, UserRole.Receptionist, UserRole.FinanceManager, UserRole.Viewer)]
    [ValidateActiveUser]
    public class CRMController : Controller
    {
        private static readonly int[] PageSizes = { 10, 25, 50 };
        private readonly ApplicationDbContext _dbContext;
        private readonly ICustomerCodeService _customerCodeService;
        private readonly IWebsiteInquiryNotifier _websiteInquiryNotifier;
        private readonly ILogger<CRMController> _logger;

        public CRMController(ApplicationDbContext dbContext, ICustomerCodeService customerCodeService, IWebsiteInquiryNotifier websiteInquiryNotifier, ILogger<CRMController> logger)
        {
            _dbContext = dbContext;
            _customerCodeService = customerCodeService;
            _websiteInquiryNotifier = websiteInquiryNotifier;
            _logger = logger;
        }

        [HttpGet]
        public async Task<IActionResult> WebsiteInquiryCount()
        {
            if (!CanAccessCrm()) return StatusCode(StatusCodes.Status403Forbidden);
            return Json(new { count = await _websiteInquiryNotifier.GetUnreviewedCountAsync() });
        }

        [HttpGet]
        public async Task<IActionResult> Index(string tab = "overview", string viewMode = "list", string? search = null, LeadStatus? status = null, LeadSource? source = null, LeadPriority? priority = null, LeadServiceInterest? serviceInterest = null, int? assignedToUserId = null, int? sportId = null, OpportunityStage? stage = null, OpportunityType? type = null, string sortBy = "newest", int page = 1, int pageSize = 10)
        {
            if (!CanAccessCrm()) return RedirectToAction("NotFound", "Error");
            tab = NormalizeTab(tab);
            page = Math.Max(page, 1);
            pageSize = PageSizes.Contains(pageSize) ? pageSize : 10;
            search = search?.Trim();
            var settings = await GetSettingsAsync();

            var model = new CrmIndexViewModel
            {
                ActiveTab = tab,
                Search = search,
                ViewMode = viewMode == "kanban" ? "kanban" : "list",
                CurrentPage = page,
                PageSize = pageSize,
                CurrencySymbol = settings.CurrencySymbol ?? "Rs",
                CanManageLeads = CanManageLeads(),
                CanManageOpportunities = CanManageOpportunities(),
                IsReadOnly = IsReadOnlyRole(),
                UserOptions = await UserOptionsAsync(),
                SportOptions = await SportOptionsAsync()
            };

            await AddDashboardAsync(model);

            if (tab == "leads")
            {
                model.WebsiteInquiryCount = await _websiteInquiryNotifier.GetUnreviewedCountAsync();
                var query = _dbContext.Leads.AsNoTracking().Include(x => x.Sport).Include(x => x.AssignedToUser).AsQueryable();
                query = ApplyLeadFilters(query, search, status, source, priority, serviceInterest, assignedToUserId, sportId);
                query = ApplyLeadSort(query, sortBy);
                model.TotalRecords = await query.CountAsync();
                model.TotalPages = Math.Max(1, (int)Math.Ceiling(model.TotalRecords / (double)pageSize));
                model.CurrentPage = Math.Min(page, model.TotalPages);
                model.Leads = await query.Skip((model.CurrentPage - 1) * pageSize).Take(pageSize).Select(x => new LeadListItemViewModel
                {
                    Id = x.Id,
                    LeadNumber = x.LeadNumber,
                    CustomerName = x.CustomerName,
                    Email = x.Email,
                    Phone = x.Phone,
                    WhatsAppNumber = x.WhatsAppNumber,
                    OrganizationName = x.OrganizationName,
                    Source = x.Source,
                    ServiceInterest = x.ServiceInterest,
                    SportName = x.Sport == null ? null : x.Sport.Name,
                    AssignedTo = x.AssignedToUser == null ? null : x.AssignedToUser.FirstName + " " + x.AssignedToUser.LastName,
                    Status = x.Status,
                    Priority = x.Priority,
                    NextFollowUpAt = x.NextFollowUpAt,
                    CreatedAt = x.CreatedAt,
                    IsWebsiteInquiry = x.IsWebsiteInquiry,
                    IsConverted = x.IsConverted,
                    ConvertedOpportunityId = x.ConvertedOpportunityId,
                    DaysOpen = (int)EF.Functions.DateDiffDay(x.CreatedAt, DateTime.UtcNow)
                }).ToListAsync();
            }
            else if (tab == "opportunities")
            {
                var query = _dbContext.Opportunities.AsNoTracking().Include(x => x.Lead).Include(x => x.Customer).Include(x => x.AssignedToUser).AsQueryable();
                query = ApplyOpportunityFilters(query, search, stage, type, assignedToUserId, sportId);
                query = ApplyOpportunitySort(query, sortBy);
                model.TotalRecords = await query.CountAsync();
                model.TotalPages = Math.Max(1, (int)Math.Ceiling(model.TotalRecords / (double)pageSize));
                model.CurrentPage = Math.Min(page, model.TotalPages);
                model.Opportunities = await query.Skip((model.CurrentPage - 1) * pageSize).Take(pageSize).Select(x => new OpportunityListItemViewModel
                {
                    Id = x.Id,
                    OpportunityNumber = x.OpportunityNumber,
                    Name = x.Name,
                    LeadNumber = x.Lead.LeadNumber,
                    CustomerName = x.Customer != null ? x.Customer.FirstName + " " + x.Customer.LastName : x.Lead.CustomerName,
                    Type = x.Type,
                    Stage = x.Stage,
                    ExpectedValue = x.ExpectedValue,
                    ProbabilityPercentage = x.ProbabilityPercentage,
                    WeightedValue = x.ExpectedValue * x.ProbabilityPercentage / 100m,
                    ExpectedCloseDate = x.ExpectedCloseDate,
                    AssignedTo = x.AssignedToUser == null ? null : x.AssignedToUser.FirstName + " " + x.AssignedToUser.LastName,
                    IsOverdue = x.Stage != OpportunityStage.Won && x.Stage != OpportunityStage.Lost && x.ExpectedCloseDate < DateOnly.FromDateTime(DateTime.UtcNow)
                }).ToListAsync();
            }

            return View(model);
        }

        [HttpGet]
        public IActionResult Pipeline() => RedirectToAction(nameof(Index), new { tab = "opportunities", viewMode = "kanban" });

        [HttpGet]
        public async Task<IActionResult> CreateLead(int? customerId)
        {
            if (!CanCreateLeads()) return RedirectToAction("NotFound", "Error");
            var model = new LeadFormViewModel { CustomerId = customerId };
            if (customerId.HasValue)
            {
                var customer = await _dbContext.Customers.AsNoTracking().FirstOrDefaultAsync(x => x.Id == customerId.Value);
                if (customer != null)
                {
                    model.CustomerName = customer.FullName;
                    model.Email = customer.Email;
                    model.Phone = customer.PrimaryPhone;
                    model.WhatsAppNumber = customer.WhatsAppNumber;
                    model.OrganizationName = customer.OrganizationName;
                    model.Source = LeadSource.ExistingCustomer;
                }
            }
            return View("LeadForm", await AddLeadOptionsAsync(model));
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateLead(LeadFormViewModel model)
        {
            if (!CanCreateLeads()) return RedirectToAction("NotFound", "Error");
            NormalizeLead(model);
            // Every lead enters the workflow as New; later statuses are only
            // reached through the qualify/disqualify/convert actions.
            model.Status = LeadStatus.New;
            await ValidateLeadAsync(model);
            model.PossibleDuplicates = await FindDuplicateLeadsAsync(model);
            if (model.PossibleDuplicates.Any() && !model.AllowDuplicate)
            {
                ModelState.AddModelError(string.Empty, "Possible duplicate leads were found. Review them and tick proceed if this is a separate inquiry.");
            }
            if (!ModelState.IsValid) return View("LeadForm", await AddLeadOptionsAsync(model));

            var lead = new Lead();
            MapLead(model, lead);
            lead.LeadNumber = await GenerateLeadNumberAsync();
            lead.CreatedAt = DateTime.UtcNow;
            lead.CreatedByUserId = CurrentUserId();
            try
            {
                _dbContext.Leads.Add(lead);
                await _dbContext.SaveChangesAsync();
                await AddLeadActivityAsync(lead.Id, LeadActivityType.Note, "Lead created", "Lead was created manually.", null, true);
            }
            catch (DbUpdateException exception)
            {
                var message = DescribeDbError(exception);
                ModelState.AddModelError(string.Empty, message);
                TempData.SetToast("error", "Save Failed", message);
                return View("LeadForm", await AddLeadOptionsAsync(model));
            }
            TempData.SetToast("success", "Lead Created", "Lead created successfully.");
            return RedirectToAction(nameof(LeadDetails), new { id = lead.Id });
        }

        [HttpGet]
        public async Task<IActionResult> EditLead(int id)
        {
            if (!CanCreateLeads()) return RedirectToAction("NotFound", "Error");
            var lead = await _dbContext.Leads.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
            if (lead == null) return RedirectToAction("NotFound", "Error");
            return View("LeadForm", await AddLeadOptionsAsync(MapLeadForm(lead)));
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> EditLead([FromRoute] int id, LeadFormViewModel model)
        {
            if (!CanCreateLeads()) return RedirectToAction("NotFound", "Error");
            if (model.Id != id) return RedirectToAction("NotFound", "Error");
            NormalizeLead(model);
            await ValidateLeadAsync(model, id);
            var lead = await _dbContext.Leads.FirstOrDefaultAsync(x => x.Id == id);
            if (lead == null) return RedirectToAction("NotFound", "Error");

            model.PossibleDuplicates = await FindDuplicateLeadsAsync(model, id);
            if (model.PossibleDuplicates.Any() && !model.AllowDuplicate)
            {
                ModelState.AddModelError(string.Empty, "Possible duplicate leads were found. Review them and tick proceed if this is a separate inquiry.");
            }

            if (lead.IsConverted && model.Status != LeadStatus.Converted)
            {
                ModelState.AddModelError(nameof(model.Status), "This lead was already converted to an opportunity, so its status cannot be changed here.");
            }
            if (!lead.IsConverted && model.Status == LeadStatus.Converted)
            {
                ModelState.AddModelError(nameof(model.Status), "A lead can only be marked converted through the Convert to Opportunity action.");
            }
            if (!lead.IsConverted && model.Status == LeadStatus.Qualified && lead.Status != LeadStatus.Qualified)
            {
                ModelState.AddModelError(nameof(model.Status), "Qualification must go through the Qualify Lead action on the lead page, which enforces its required owner, contact and notes checks.");
            }
            if (!lead.IsConverted && model.Status != lead.Status && model.Status is not (LeadStatus.Qualified or LeadStatus.Converted) && !LeadStatusRules.CanTransition(lead.Status, model.Status))
            {
                var allowed = string.Join(", ", LeadStatusRules.AllowedTargets(lead.Status).Select(x => x.GetDisplayName()));
                ModelState.AddModelError(nameof(model.Status), $"A {lead.Status.GetDisplayName()} lead cannot move directly to {model.Status.GetDisplayName()}. Allowed next steps: {allowed}.");
            }

            if (!ModelState.IsValid) return View("LeadForm", await AddLeadOptionsAsync(model));
            var previousStatus = lead.Status;
            var previousOwner = lead.AssignedToUserId;
            MapLead(model, lead);
            lead.UpdatedAt = DateTime.UtcNow;
            lead.UpdatedByUserId = CurrentUserId();
            if (LeadStatusRules.IsReopen(previousStatus, lead.Status))
            {
                // Controlled reopen: the reason text is kept as history, only the timestamp is cleared.
                lead.DisqualifiedAt = null;
            }
            if (!lead.IsConverted && await _dbContext.LeadActivities.AnyAsync(x => x.LeadId == lead.Id && !x.IsCompleted && x.FollowUpDueAt != null))
            {
                // Scheduled activities are the source of truth for the next pending follow-up.
                lead.NextFollowUpAt = await EarliestPendingLeadFollowUpAsync(lead.Id);
            }
            if (previousStatus != lead.Status) await AddLeadActivityAsync(lead.Id, LeadActivityType.StatusChange, "Status changed", $"{previousStatus.GetDisplayName()} to {lead.Status.GetDisplayName()}", null, true, save: false);
            if (previousOwner != lead.AssignedToUserId) await AddLeadActivityAsync(lead.Id, LeadActivityType.Assignment, "Assignment changed", "Lead owner was updated.", null, true, save: false);
            try
            {
                await _dbContext.SaveChangesAsync();
            }
            catch (DbUpdateException exception)
            {
                var message = DescribeDbError(exception);
                ModelState.AddModelError(string.Empty, message);
                TempData.SetToast("error", "Save Failed", message);
                return View("LeadForm", await AddLeadOptionsAsync(model));
            }
            if (lead.IsWebsiteInquiry && previousStatus != lead.Status) await NotifyWebsiteInquiryReviewedAsync();
            TempData.SetToast("success", "Lead Updated", "Lead updated successfully.");
            return RedirectToAction(nameof(LeadDetails), new { id });
        }

        [HttpGet]
        public async Task<IActionResult> LeadDetails(int id)
        {
            var lead = await _dbContext.Leads.AsNoTracking()
                .Include(x => x.Customer).Include(x => x.Sport).Include(x => x.Facility).Include(x => x.Court).Include(x => x.MembershipPlan)
                .Include(x => x.AssignedToUser).Include(x => x.Activities).ThenInclude(x => x.CreatedByUser)
                .Include(x => x.ConvertedOpportunity)
                .FirstOrDefaultAsync(x => x.Id == id);
            if (lead == null) return RedirectToAction("NotFound", "Error");
            ViewBag.CanManageLeads = CanManageLeads();
            ViewBag.CanConvert = CanManageOpportunities();
            return View(lead);
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> AddLeadActivity(int id, LeadActivityType activityType, string subject, string? description, DateTime activityDate, DateTime? followUpDueAt, bool isCompleted)
        {
            if (!CanCreateLeads()) return RedirectToAction("NotFound", "Error");
            var lead = await _dbContext.Leads.FirstOrDefaultAsync(x => x.Id == id);
            if (lead == null) return RedirectToAction("NotFound", "Error");
            if (string.IsNullOrWhiteSpace(subject)) subject = activityType.GetDisplayName();
            if (activityDate == default) activityDate = DateTime.UtcNow;
            try
            {
                await AddLeadActivityAsync(id, activityType, subject.Trim(), description?.Trim(), followUpDueAt, isCompleted, activityDate);
                // Next follow-up always points at the earliest still-pending
                // scheduled activity, so completed follow-ups never qualify.
                lead.NextFollowUpAt = await EarliestPendingLeadFollowUpAsync(id);
                if (activityType is LeadActivityType.PhoneCall or LeadActivityType.WhatsApp or LeadActivityType.Email or LeadActivityType.Meeting) lead.LastContactedAt = DateTime.UtcNow;
                await _dbContext.SaveChangesAsync();
            }
            catch (DbUpdateException exception)
            {
                TempData.SetToast("error", "Save Failed", DescribeDbError(exception));
                return RedirectToAction(nameof(LeadDetails), new { id });
            }
            TempData.SetToast("success", "Activity Added", "Lead activity recorded.");
            return RedirectToAction(nameof(LeadDetails), new { id });
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> AddOpportunityActivity(int id, OpportunityActivityType activityType, string subject, string? description, DateTime activityDate, DateTime? followUpDueAt, bool isCompleted)
        {
            if (!CanManageOpportunities()) return RedirectToAction("NotFound", "Error");
            var opportunity = await _dbContext.Opportunities.FirstOrDefaultAsync(x => x.Id == id);
            if (opportunity == null) return RedirectToAction("NotFound", "Error");
            if (string.IsNullOrWhiteSpace(subject)) subject = activityType.GetDisplayName();
            if (activityDate == default) activityDate = DateTime.UtcNow;
            await AddOpportunityActivityAsync(id, activityType, subject.Trim(), description?.Trim(), followUpDueAt, isCompleted, activityDate);
            // Next follow-up always points at the earliest still-pending scheduled
            // activity; it also drops the date entirely when nothing is pending.
            opportunity.NextFollowUpAt = await EarliestPendingOpportunityFollowUpAsync(id);
            try
            {
                await _dbContext.SaveChangesAsync();
            }
            catch (DbUpdateException exception)
            {
                TempData.SetToast("error", "Save Failed", DescribeDbError(exception));
                return RedirectToAction(nameof(OpportunityDetails), new { id });
            }
            TempData.SetToast("success", "Activity Added", "Opportunity activity recorded.");
            return RedirectToAction(nameof(OpportunityDetails), new { id });
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> QualifyLead(int id, string qualificationNotes)
        {
            if (!CanManageLeads()) return RedirectToAction("NotFound", "Error");
            var lead = await _dbContext.Leads.FirstOrDefaultAsync(x => x.Id == id);
            if (lead == null) return RedirectToAction("NotFound", "Error");
            if (!lead.IsActive) { TempData.SetToast("warning", "Action Blocked", "Inactive leads cannot be qualified."); return RedirectToAction(nameof(LeadDetails), new { id }); }
            if (lead.IsConverted) { TempData.SetToast("warning", "Action Blocked", "This lead has already been converted to an opportunity."); return RedirectToAction(nameof(LeadDetails), new { id }); }
            if (lead.Status is LeadStatus.Disqualified or LeadStatus.Lost)
            {
                TempData.SetToast("warning", "Qualification Blocked", "Reopen this lead back to Contacted before qualifying it.");
                return RedirectToAction(nameof(LeadDetails), new { id });
            }
            if (lead.Status == LeadStatus.New)
            {
                TempData.SetToast("warning", "Qualification Blocked", "Mark the lead as Contacted before qualifying it.");
                return RedirectToAction(nameof(LeadDetails), new { id });
            }
            if (lead.AssignedToUserId == null || string.IsNullOrWhiteSpace(qualificationNotes) || !HasContact(lead))
            {
                TempData.SetToast("warning", "Qualification Blocked", "Lead needs contact information, owner and qualification notes.");
                return RedirectToAction(nameof(LeadDetails), new { id });
            }
            lead.Status = LeadStatus.Qualified;
            lead.QualifiedAt = DateTime.UtcNow;
            lead.QualificationNotes = qualificationNotes.Trim();
            await AddLeadActivityAsync(id, LeadActivityType.Qualification, "Lead qualified", qualificationNotes.Trim(), null, true, save: false);
            try
            {
                await _dbContext.SaveChangesAsync();
            }
            catch (DbUpdateException exception)
            {
                TempData.SetToast("error", "Save Failed", DescribeDbError(exception));
                return RedirectToAction(nameof(LeadDetails), new { id });
            }
            if (lead.IsWebsiteInquiry) await NotifyWebsiteInquiryReviewedAsync();
            TempData.SetToast("success", "Lead Qualified", "Lead qualified successfully.");
            return RedirectToAction(nameof(LeadDetails), new { id });
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> DisqualifyLead(int id, string reason)
        {
            if (!CanManageLeads()) return RedirectToAction("NotFound", "Error");
            var lead = await _dbContext.Leads.FirstOrDefaultAsync(x => x.Id == id);
            if (lead == null) return RedirectToAction("NotFound", "Error");
            if (lead.IsConverted) { TempData.SetToast("warning", "Action Blocked", "A converted lead cannot be disqualified."); return RedirectToAction(nameof(LeadDetails), new { id }); }
            if (!lead.IsActive) { TempData.SetToast("warning", "Action Blocked", "Inactive leads cannot be disqualified."); return RedirectToAction(nameof(LeadDetails), new { id }); }
            if (lead.Status == LeadStatus.Lost)
            {
                TempData.SetToast("warning", "Action Blocked", "This lead has the legacy Lost status. Reopen it back to Contacted first.");
                return RedirectToAction(nameof(LeadDetails), new { id });
            }
            if (string.IsNullOrWhiteSpace(reason) || reason.Trim().Length < 10)
            {
                TempData.SetToast("warning", "Reason Required", "Disqualification reason must be at least 10 characters.");
                return RedirectToAction(nameof(LeadDetails), new { id });
            }
            lead.Status = LeadStatus.Disqualified;
            lead.DisqualifiedAt = DateTime.UtcNow;
            lead.DisqualificationReason = reason.Trim();
            await AddLeadActivityAsync(id, LeadActivityType.Disqualification, "Lead disqualified", reason.Trim(), null, true, save: false);
            try
            {
                await _dbContext.SaveChangesAsync();
            }
            catch (DbUpdateException exception)
            {
                TempData.SetToast("error", "Save Failed", DescribeDbError(exception));
                return RedirectToAction(nameof(LeadDetails), new { id });
            }
            if (lead.IsWebsiteInquiry) await NotifyWebsiteInquiryReviewedAsync();
            TempData.SetToast("success", "Lead Disqualified", "Lead history has been preserved.");
            return RedirectToAction(nameof(LeadDetails), new { id });
        }

        /// <summary>
        /// Controlled way back into the workflow for a disqualified (or legacy
        /// Lost) lead: returns it to Contacted, clears the disqualification
        /// timestamp while keeping the reason as history, and logs the reopen
        /// in the activity timeline.
        /// </summary>
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> ReopenLead(int id)
        {
            if (!CanManageLeads()) return RedirectToAction("NotFound", "Error");
            var lead = await _dbContext.Leads.FirstOrDefaultAsync(x => x.Id == id);
            if (lead == null) return RedirectToAction("NotFound", "Error");
            if (lead.IsConverted) { TempData.SetToast("warning", "Action Blocked", "A converted lead cannot be reopened."); return RedirectToAction(nameof(LeadDetails), new { id }); }
            if (!lead.IsActive) { TempData.SetToast("warning", "Action Blocked", "Inactive leads cannot be reopened."); return RedirectToAction(nameof(LeadDetails), new { id }); }
            if (lead.Status is not (LeadStatus.Disqualified or LeadStatus.Lost))
            {
                TempData.SetToast("warning", "Action Blocked", "Only disqualified or lost leads can be reopened.");
                return RedirectToAction(nameof(LeadDetails), new { id });
            }
            var previousStatus = lead.Status;
            lead.Status = LeadStatus.Contacted;
            lead.DisqualifiedAt = null;
            lead.UpdatedAt = DateTime.UtcNow;
            lead.UpdatedByUserId = CurrentUserId();
            await AddLeadActivityAsync(id, LeadActivityType.StatusChange, "Lead reopened", $"Reopened from {previousStatus.GetDisplayName()} and returned to Contacted.", null, true, save: false);
            try
            {
                await _dbContext.SaveChangesAsync();
            }
            catch (DbUpdateException exception)
            {
                TempData.SetToast("error", "Save Failed", DescribeDbError(exception));
                return RedirectToAction(nameof(LeadDetails), new { id });
            }
            if (lead.IsWebsiteInquiry) await NotifyWebsiteInquiryReviewedAsync();
            TempData.SetToast("success", "Lead Reopened", "Lead returned to the active workflow as Contacted.");
            return RedirectToAction(nameof(LeadDetails), new { id });
        }

        [HttpGet]
        public async Task<IActionResult> ConvertToOpportunity(int id)
        {
            if (!CanManageOpportunities()) return RedirectToAction("NotFound", "Error");
            var lead = await _dbContext.Leads.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
            if (lead == null) return RedirectToAction("NotFound", "Error");
            var reason = ConversionBlockReason(lead);
            if (reason != null)
            {
                TempData.SetToast("warning", "Conversion Blocked", reason);
                return RedirectToAction(nameof(LeadDetails), new { id });
            }
            var model = new OpportunityFormViewModel
            {
                LeadId = lead.Id,
                CustomerId = lead.CustomerId,
                IsLeadConversion = true,
                Name = $"{lead.CustomerName} - {lead.ServiceInterest.GetDisplayName()}",
                Type = MapOpportunityType(lead.ServiceInterest),
                ExpectedCloseDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(14)),
                ProbabilityPercentage = StageProbability(OpportunityStage.Qualification),
                AssignedToUserId = lead.AssignedToUserId,
                SportId = lead.SportId,
                FacilityId = lead.FacilityId,
                CourtId = lead.CourtId,
                MembershipPlanId = lead.MembershipPlanId,
                CustomerRequirement = lead.InquiryDetails
            };
            return View("OpportunityForm", await AddOpportunityOptionsAsync(model));
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> ConvertToOpportunity([FromRoute] int id, OpportunityFormViewModel model, bool createCustomerFromLead = false)
        {
            if (!CanManageOpportunities()) return RedirectToAction("NotFound", "Error");
            var lead = await _dbContext.Leads.FirstOrDefaultAsync(x => x.Id == id);
            if (lead == null) return RedirectToAction("NotFound", "Error");
            var blockReason = ConversionBlockReason(lead);
            if (blockReason != null)
            {
                TempData.SetToast("warning", "Conversion Blocked", blockReason);
                return RedirectToAction(nameof(LeadDetails), new { id });
            }
            model.LeadId = id;
            model.IsLeadConversion = true;
            NormalizeOpportunity(model);
            await ValidateOpportunityAsync(model);
            if (!ModelState.IsValid) return View("OpportunityForm", await AddOpportunityOptionsAsync(model));
            var previousLeadStatus = lead.Status;
            try
            {
                var opportunity = await ConvertLeadAsync(lead, model, createCustomerFromLead);
                if (opportunity == null)
                {
                    const string duplicateMessage = "This lead was already converted to an opportunity. Open the existing opportunity instead of creating another one.";
                    ModelState.AddModelError(string.Empty, duplicateMessage);
                    TempData.SetToast("warning", "Duplicate Conversion", duplicateMessage);
                    return View("OpportunityForm", await AddOpportunityOptionsAsync(model));
                }
                if (lead.IsWebsiteInquiry && previousLeadStatus != lead.Status) await NotifyWebsiteInquiryReviewedAsync();
                TempData.SetToast("success", "Opportunity Created", "Lead converted successfully.");
                return RedirectToAction(nameof(OpportunityDetails), new { id = opportunity.Id });
            }
            catch (DbUpdateException exception)
            {
                var message = DescribeDbError(exception);
                ModelState.AddModelError(string.Empty, message);
                TempData.SetToast("error", "Conversion Failed", message);
                return View("OpportunityForm", await AddOpportunityOptionsAsync(model));
            }
        }

        private string? ConversionBlockReason(Lead lead)
        {
            // Check the duplicate case first so an already-converted lead always
            // ends with the duplicate-conversion message, even if it has since
            // been deactivated.
            if (lead.IsConverted || lead.Status == LeadStatus.Converted) return "This lead has already been converted to an opportunity.";
            if (!lead.IsActive) return "This lead is inactive and cannot be converted.";
            if (lead.Status != LeadStatus.Qualified) return "Only qualified leads can be converted. Qualify the lead first.";
            if (lead.AssignedToUserId == null) return "Assign an owner to this lead before converting it.";
            if (!HasContact(lead)) return "Add at least one contact method to this lead before converting it.";
            return null;
        }

        [HttpGet]
        public async Task<IActionResult> CreateOpportunity()
        {
            if (!CanManageOpportunities()) return RedirectToAction("NotFound", "Error");
            return View("OpportunityForm", await AddOpportunityOptionsAsync(new OpportunityFormViewModel()));
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateOpportunity(OpportunityFormViewModel model, bool createCustomerFromLead = false)
        {
            if (!CanManageOpportunities()) return RedirectToAction("NotFound", "Error");
            NormalizeOpportunity(model);
            await ValidateOpportunityAsync(model);
            if (!ModelState.IsValid) return View("OpportunityForm", await AddOpportunityOptionsAsync(model));
            var lead = await _dbContext.Leads.FirstOrDefaultAsync(x => x.Id == model.LeadId);
            if (lead == null)
            {
                ModelState.AddModelError(nameof(model.LeadId), "Selected lead no longer exists.");
                return View("OpportunityForm", await AddOpportunityOptionsAsync(model));
            }
            var blockReason = ConversionBlockReason(lead);
            if (blockReason != null)
            {
                ModelState.AddModelError(nameof(model.LeadId), blockReason);
            }
            if (!ModelState.IsValid) return View("OpportunityForm", await AddOpportunityOptionsAsync(model));
            try
            {
                var opportunity = await ConvertLeadAsync(lead, model, createCustomerFromLead);
                if (opportunity == null)
                {
                    const string duplicateMessage = "This lead was already converted to an opportunity. Open the existing opportunity instead of creating another one.";
                    ModelState.AddModelError(string.Empty, duplicateMessage);
                    TempData.SetToast("warning", "Duplicate Conversion", duplicateMessage);
                    return View("OpportunityForm", await AddOpportunityOptionsAsync(model));
                }
                TempData.SetToast("success", "Opportunity Created", "Opportunity created successfully.");
                return RedirectToAction(nameof(OpportunityDetails), new { id = opportunity.Id });
            }
            catch (DbUpdateException exception)
            {
                var message = DescribeDbError(exception);
                ModelState.AddModelError(string.Empty, message);
                TempData.SetToast("error", "Save Failed", message);
                return View("OpportunityForm", await AddOpportunityOptionsAsync(model));
            }
        }

        [HttpGet]
        public async Task<IActionResult> EditOpportunity(int id)
        {
            if (!CanManageOpportunities()) return RedirectToAction("NotFound", "Error");
            var opportunity = await _dbContext.Opportunities.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
            if (opportunity == null) return RedirectToAction("NotFound", "Error");
            return View("OpportunityForm", await AddOpportunityOptionsAsync(MapOpportunityForm(opportunity)));
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> EditOpportunity([FromRoute] int id, OpportunityFormViewModel model)
        {
            if (!CanManageOpportunities()) return RedirectToAction("NotFound", "Error");
            if (model.Id != id) return RedirectToAction("NotFound", "Error");
            NormalizeOpportunity(model);
            await ValidateOpportunityAsync(model, id);
            var opportunity = await _dbContext.Opportunities.FirstOrDefaultAsync(x => x.Id == id);
            if (opportunity == null) return RedirectToAction("NotFound", "Error");
            if (opportunity.LeadId != model.LeadId && await _dbContext.Leads.AnyAsync(x => x.Id == opportunity.LeadId && x.IsConverted))
            {
                ModelState.AddModelError(nameof(model.LeadId), "The lead link cannot be changed after that lead has been converted.");
            }
            if (!CanTransition(opportunity.Stage, model.Stage))
            {
                ModelState.AddModelError(nameof(model.Stage), "Invalid stage transition.");
            }
            if (!ModelState.IsValid) return View("OpportunityForm", await AddOpportunityOptionsAsync(model));
            var oldStage = opportunity.Stage;
            MapOpportunity(model, opportunity);
            // Keep the opportunity's customer consistent with its source lead:
            // the lead's link is authoritative, otherwise the customer picked
            // here is also linked onto the lead.
            var sourceLead = await _dbContext.Leads.FirstOrDefaultAsync(x => x.Id == opportunity.LeadId);
            if (sourceLead != null)
            {
                if (sourceLead.CustomerId.HasValue) opportunity.CustomerId = sourceLead.CustomerId;
                else if (opportunity.CustomerId.HasValue) sourceLead.CustomerId = opportunity.CustomerId;
            }
            opportunity.UpdatedAt = DateTime.UtcNow;
            opportunity.UpdatedByUserId = CurrentUserId();
            ApplyStageRules(opportunity);
            if (oldStage != opportunity.Stage)
            {
                _dbContext.OpportunityActivities.Add(new OpportunityActivity { OpportunityId = opportunity.Id, ActivityType = OpportunityActivityType.StageChange, Subject = "Stage changed", Description = $"{oldStage.GetDisplayName()} to {opportunity.Stage.GetDisplayName()}", ActivityDate = DateTime.UtcNow, CreatedAt = DateTime.UtcNow, CreatedByUserId = CurrentUserId(), IsCompleted = true });
            }
            try
            {
                await _dbContext.SaveChangesAsync();
            }
            catch (DbUpdateException exception)
            {
                var message = DescribeDbError(exception);
                ModelState.AddModelError(string.Empty, message);
                TempData.SetToast("error", "Save Failed", message);
                return View("OpportunityForm", await AddOpportunityOptionsAsync(model));
            }
            TempData.SetToast("success", "Opportunity Updated", "Opportunity updated successfully.");
            return RedirectToAction(nameof(OpportunityDetails), new { id });
        }

        [HttpGet]
        public async Task<IActionResult> OpportunityDetails(int id)
        {
            var opportunity = await _dbContext.Opportunities.AsNoTracking()
                .Include(x => x.Lead).ThenInclude(l => l.Activities).ThenInclude(a => a.CreatedByUser)
                .Include(x => x.Lead).ThenInclude(l => l.AssignedToUser)
                .Include(x => x.Customer).Include(x => x.Sport).Include(x => x.Facility).Include(x => x.Court).Include(x => x.MembershipPlan)
                .Include(x => x.AssignedToUser).Include(x => x.Activities).ThenInclude(x => x.CreatedByUser)
                .FirstOrDefaultAsync(x => x.Id == id);
            if (opportunity == null) return RedirectToAction("NotFound", "Error");
            ViewBag.CurrencySymbol = (await GetSettingsAsync()).CurrencySymbol ?? "Rs";
            ViewBag.CanManageOpportunities = CanManageOpportunities();
            ViewBag.CanCreateQuotation = CanCreateQuotation();
            var linkedQuotation = await _dbContext.SalesQuotations.AsNoTracking()
                .Where(x => x.OpportunityId == id && x.IsActive)
                .OrderByDescending(x => x.Id)
                .FirstOrDefaultAsync();
            ViewBag.LinkedQuotation = linkedQuotation;
            ViewBag.QuotationBlocksNew = linkedQuotation != null && linkedQuotation.Status != Models.Enums.SalesQuotationStatus.Cancelled && linkedQuotation.Status != Models.Enums.SalesQuotationStatus.Rejected;
            return View(opportunity);
        }

        [HttpGet]
        public async Task<IActionResult> FollowUps()
        {
            if (!CanAccessCrm()) return RedirectToAction("NotFound", "Error");
            var model = await BuildFollowUpsAsync();
            ViewBag.CanCompleteFollowUps = CanCreateLeads();
            return View(model);
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> CompleteFollowUp(string recordType, int activityId)
        {
            if (!CanCreateLeads()) return RedirectToAction("NotFound", "Error");
            if (recordType == "Lead")
            {
                var item = await _dbContext.LeadActivities.Include(x => x.Lead).FirstOrDefaultAsync(x => x.Id == activityId);
                if (item == null) { TempData.SetToast("warning", "Not Found", "Follow-up not found."); return RedirectToAction(nameof(FollowUps)); }
                if (item.FollowUpDueAt == null) { TempData.SetToast("warning", "Not a Follow-up", "This activity has no scheduled follow-up date."); return RedirectToAction(nameof(FollowUps)); }
                item.IsCompleted = true; item.UpdatedAt = DateTime.UtcNow;
                item.Lead.NextFollowUpAt = await _dbContext.LeadActivities.Where(x => x.LeadId == item.LeadId && !x.IsCompleted && x.FollowUpDueAt != null && x.Id != item.Id).OrderBy(x => x.FollowUpDueAt).Select(x => x.FollowUpDueAt).FirstOrDefaultAsync();
            }
            else if (recordType == "Opportunity")
            {
                var item = await _dbContext.OpportunityActivities.Include(x => x.Opportunity).FirstOrDefaultAsync(x => x.Id == activityId);
                if (item == null) { TempData.SetToast("warning", "Not Found", "Follow-up not found."); return RedirectToAction(nameof(FollowUps)); }
                if (item.FollowUpDueAt == null) { TempData.SetToast("warning", "Not a Follow-up", "This activity has no scheduled follow-up date."); return RedirectToAction(nameof(FollowUps)); }
                item.IsCompleted = true; item.UpdatedAt = DateTime.UtcNow;
                item.Opportunity.NextFollowUpAt = await _dbContext.OpportunityActivities.Where(x => x.OpportunityId == item.OpportunityId && !x.IsCompleted && x.FollowUpDueAt != null && x.Id != item.Id).OrderBy(x => x.FollowUpDueAt).Select(x => x.FollowUpDueAt).FirstOrDefaultAsync();
            }
            else
            {
                TempData.SetToast("warning", "Invalid Follow-up", "That follow-up type is not recognised.");
                return RedirectToAction(nameof(FollowUps));
            }
            try
            {
                await _dbContext.SaveChangesAsync();
            }
            catch (DbUpdateException exception)
            {
                TempData.SetToast("error", "Save Failed", DescribeDbError(exception));
                return RedirectToAction(nameof(FollowUps));
            }
            TempData.SetToast("success", "Follow-up Completed", "Follow-up marked as complete.");
            return RedirectToAction(nameof(FollowUps));
        }

        private async Task AddDashboardAsync(CrmIndexViewModel model)
        {
            var openStatuses = new[] { LeadStatus.New, LeadStatus.Contacted, LeadStatus.FollowUp, LeadStatus.Qualified };
            var now = DateTime.UtcNow;
            var today = DateOnly.FromDateTime(now);
            var monthStart = new DateOnly(today.Year, today.Month, 1);
            var openLeads = _dbContext.Leads.Where(x => x.IsActive && openStatuses.Contains(x.Status));
            var openPipeline = _dbContext.Opportunities.Where(x => x.IsActive && x.Stage != OpportunityStage.Won && x.Stage != OpportunityStage.Lost);

            // Lead metrics (overall totals, never scoped to the current page or filters).
            model.TotalOpenLeads = await openLeads.CountAsync();
            model.NewLeads = await _dbContext.Leads.CountAsync(x => x.Status == LeadStatus.New && x.IsActive);
            model.QualifiedLeads = await _dbContext.Leads.CountAsync(x => x.Status == LeadStatus.Qualified && x.IsActive);
            model.ConvertedLeads = await _dbContext.Leads.CountAsync(x => x.IsConverted);
            model.UnassignedLeads = await openLeads.CountAsync(x => x.AssignedToUserId == null);
            model.NoFollowUpLeads = await openLeads.CountAsync(x => x.NextFollowUpAt == null);
            var totalLeads = await _dbContext.Leads.CountAsync();
            model.ConversionRate = totalLeads == 0 ? 0 : Math.Round(model.ConvertedLeads * 100m / totalLeads, 1);

            // Opportunity metrics.
            model.OpportunityCount = await openPipeline.CountAsync();
            model.PipelineValue = await openPipeline.SumAsync(x => x.ExpectedValue);
            model.WeightedPipeline = await openPipeline.SumAsync(x => x.ExpectedValue * x.ProbabilityPercentage / 100m);
            model.WonOpportunities = await _dbContext.Opportunities.CountAsync(x => x.IsActive && x.Stage == OpportunityStage.Won);
            model.LostOpportunities = await _dbContext.Opportunities.CountAsync(x => x.IsActive && x.Stage == OpportunityStage.Lost);
            model.ClosingThisMonth = await openPipeline.CountAsync(x => x.ExpectedCloseDate >= monthStart && x.ExpectedCloseDate < monthStart.AddMonths(1));
            model.OverdueOpportunities = await openPipeline.CountAsync(x => x.ExpectedCloseDate < today);
            model.UnassignedOpportunities = await openPipeline.CountAsync(x => x.AssignedToUserId == null);
            model.NegotiationValue = await _dbContext.Opportunities.Where(x => x.IsActive && x.Stage == OpportunityStage.Negotiation).SumAsync(x => x.ExpectedValue);

            // Follow-ups backed by scheduled activities on leads and opportunities.
            model.OverdueFollowUps = await _dbContext.LeadActivities.CountAsync(x => x.FollowUpDueAt != null && !x.IsCompleted && x.FollowUpDueAt < now)
                + await _dbContext.OpportunityActivities.CountAsync(x => x.FollowUpDueAt != null && !x.IsCompleted && x.FollowUpDueAt < now);

            // Status is persisted as text (EnumToStringConverter). Legacy rows that were
            // written outside EF can hold the numeric enum value (e.g. '5' instead of
            // 'Converted'); SQL groups those rows separately, while the converter parses
            // both strings back to the same LeadStatus. Merge those groups after
            // materialising so one malformed row cannot fail the whole dashboard.
            var leadFunnelGroups = await _dbContext.Leads.GroupBy(x => x.Status).Select(x => new { x.Key, Count = x.Count() }).ToListAsync();
            model.LeadFunnel = leadFunnelGroups.GroupBy(x => x.Key).ToDictionary(x => x.Key, x => x.Sum(group => group.Count));
            model.PipelineByStage = await _dbContext.Opportunities.GroupBy(x => x.Stage).Select(x => new { x.Key, Value = x.Sum(o => o.ExpectedValue) }).ToDictionaryAsync(x => x.Key, x => x.Value);
            model.LeadsBySource = await _dbContext.Leads.GroupBy(x => x.Source).Select(x => new { x.Key, Count = x.Count() }).ToDictionaryAsync(x => x.Key, x => x.Count);
            model.FollowUps = (await BuildFollowUpsAsync()).Take(8).ToList();
        }

        private async Task<List<CrmFollowUpItemViewModel>> BuildFollowUpsAsync()
        {
            var leads = await _dbContext.LeadActivities.AsNoTracking().Include(x => x.Lead).ThenInclude(x => x.AssignedToUser)
                .Where(x => x.FollowUpDueAt != null).Select(x => new CrmFollowUpItemViewModel { RecordType = "Lead", ActivityId = x.Id, ParentId = x.LeadId, Number = x.Lead.LeadNumber, Customer = x.Lead.CustomerName, Subject = x.Subject, Owner = x.Lead.AssignedToUser == null ? null : x.Lead.AssignedToUser.FirstName + " " + x.Lead.AssignedToUser.LastName, DueAt = x.FollowUpDueAt!.Value, IsCompleted = x.IsCompleted, ActivityType = x.ActivityType.GetDisplayName() }).ToListAsync();
            var opps = await _dbContext.OpportunityActivities.AsNoTracking().Include(x => x.Opportunity).ThenInclude(x => x.AssignedToUser).Include(x => x.Opportunity).ThenInclude(x => x.Lead)
                .Where(x => x.FollowUpDueAt != null).Select(x => new CrmFollowUpItemViewModel { RecordType = "Opportunity", ActivityId = x.Id, ParentId = x.OpportunityId, Number = x.Opportunity.OpportunityNumber, Customer = x.Opportunity.Lead.CustomerName, Subject = x.Subject, Owner = x.Opportunity.AssignedToUser == null ? null : x.Opportunity.AssignedToUser.FirstName + " " + x.Opportunity.AssignedToUser.LastName, DueAt = x.FollowUpDueAt!.Value, IsCompleted = x.IsCompleted, ActivityType = x.ActivityType.GetDisplayName() }).ToListAsync();
            return leads.Concat(opps).OrderBy(x => x.IsCompleted).ThenBy(x => x.DueAt).ToList();
        }

        private IQueryable<Lead> ApplyLeadFilters(IQueryable<Lead> query, string? search, LeadStatus? status, LeadSource? source, LeadPriority? priority, LeadServiceInterest? serviceInterest, int? assignedToUserId, int? sportId)
        {
            if (!string.IsNullOrWhiteSpace(search))
            {
                var s = search.ToLower();
                query = query.Where(x => x.LeadNumber.ToLower().Contains(s) || x.CustomerName.ToLower().Contains(s) || (x.Email != null && x.Email.ToLower().Contains(s)) || (x.Phone != null && x.Phone.Contains(s)) || (x.WhatsAppNumber != null && x.WhatsAppNumber.Contains(s)) || (x.OrganizationName != null && x.OrganizationName.ToLower().Contains(s)) || (x.InquirySubject != null && x.InquirySubject.ToLower().Contains(s)) || (x.Tags != null && x.Tags.ToLower().Contains(s)));
            }
            if (status.HasValue) query = query.Where(x => x.Status == status);
            if (source.HasValue) query = query.Where(x => x.Source == source);
            if (priority.HasValue) query = query.Where(x => x.Priority == priority);
            if (serviceInterest.HasValue) query = query.Where(x => x.ServiceInterest == serviceInterest);
            if (assignedToUserId.HasValue) query = query.Where(x => x.AssignedToUserId == assignedToUserId);
            if (sportId.HasValue) query = query.Where(x => x.SportId == sportId);
            return query;
        }

        private IQueryable<Opportunity> ApplyOpportunityFilters(IQueryable<Opportunity> query, string? search, OpportunityStage? stage, OpportunityType? type, int? assignedToUserId, int? sportId)
        {
            if (!string.IsNullOrWhiteSpace(search))
            {
                var s = search.ToLower();
                query = query.Where(x => x.OpportunityNumber.ToLower().Contains(s) || x.Name.ToLower().Contains(s) || x.Lead.LeadNumber.ToLower().Contains(s) || x.Lead.CustomerName.ToLower().Contains(s) || (x.Lead.Phone != null && x.Lead.Phone.Contains(s)) || (x.Lead.OrganizationName != null && x.Lead.OrganizationName.ToLower().Contains(s)) || (x.Tags != null && x.Tags.ToLower().Contains(s)));
            }
            if (stage.HasValue) query = query.Where(x => x.Stage == stage);
            if (type.HasValue) query = query.Where(x => x.Type == type);
            if (assignedToUserId.HasValue) query = query.Where(x => x.AssignedToUserId == assignedToUserId);
            if (sportId.HasValue) query = query.Where(x => x.SportId == sportId);
            return query;
        }

        private static IQueryable<Lead> ApplyLeadSort(IQueryable<Lead> query, string sortBy) => sortBy switch
        {
            "oldest" => query.OrderBy(x => x.CreatedAt),
            "name" => query.OrderBy(x => x.CustomerName),
            "priority" => query.OrderByDescending(x => x.Priority),
            "followup" => query.OrderBy(x => x.NextFollowUpAt),
            "status" => query.OrderBy(x => x.Status),
            "assigned" => query.OrderBy(x => x.AssignedToUserId),
            _ => query.OrderByDescending(x => x.CreatedAt)
        };

        private static IQueryable<Opportunity> ApplyOpportunitySort(IQueryable<Opportunity> query, string sortBy) => sortBy switch
        {
            "oldest" => query.OrderBy(x => x.CreatedAt),
            "value" => query.OrderByDescending(x => x.ExpectedValue),
            "weighted" => query.OrderByDescending(x => x.ExpectedValue * x.ProbabilityPercentage / 100m),
            "close" => query.OrderBy(x => x.ExpectedCloseDate),
            "stage" => query.OrderBy(x => x.Stage),
            _ => query.OrderByDescending(x => x.CreatedAt)
        };

        private async Task ValidateLeadAsync(LeadFormViewModel model, int? id = null)
        {
            if (string.IsNullOrWhiteSpace(model.Email) && string.IsNullOrWhiteSpace(model.Phone) && string.IsNullOrWhiteSpace(model.WhatsAppNumber)) ModelState.AddModelError(string.Empty, "At least one contact method is required.");
            if (!string.IsNullOrWhiteSpace(model.Email) && !HasValidComEmail(model.Email)) ModelState.AddModelError(nameof(model.Email), EmailFormatMessage);
            if (!string.IsNullOrWhiteSpace(model.Phone) && !HasValidPakPhone(model.Phone)) ModelState.AddModelError(nameof(model.Phone), PhoneFormatMessage);
            if (!string.IsNullOrWhiteSpace(model.WhatsAppNumber) && !HasValidPakPhone(model.WhatsAppNumber)) ModelState.AddModelError(nameof(model.WhatsAppNumber), PhoneFormatMessage);
            if (model.PreferredDate.HasValue && model.PreferredDate.Value < DateOnly.FromDateTime(DateTime.UtcNow)) ModelState.AddModelError(nameof(model.PreferredDate), "Preferred date cannot be in the past.");
            if (id == null && model.NextFollowUpAt.HasValue && model.NextFollowUpAt.Value < DateTime.UtcNow) ModelState.AddModelError(nameof(model.NextFollowUpAt), "Next follow-up cannot be scheduled in the past.");
            if (model.AssignedToUserId.HasValue)
            {
                var assignedUser = await _dbContext.Users.AsNoTracking().FirstOrDefaultAsync(x => x.Id == model.AssignedToUserId);
                if (assignedUser == null)
                {
                    ModelState.AddModelError(nameof(model.AssignedToUserId), "Assigned user does not exist.");
                }
                else
                {
                    var isSalesRole = assignedUser.Role == UserRole.BookingManager;
                    var isActive = assignedUser.IsActive;
                    var keepsExistingOwner = id.HasValue
                        && await _dbContext.Leads.AnyAsync(x => x.Id == id && x.AssignedToUserId == model.AssignedToUserId);

                    // For new assignments, user must be active and have Sales role.
                    // For existing assignments, we allow keeping the owner even if inactive or role changed.
                    if (!keepsExistingOwner)
                    {
                        if (!isActive)
                        {
                            ModelState.AddModelError(nameof(model.AssignedToUserId), "Assigned user is no longer active.");
                        }
                        else if (!isSalesRole)
                        {
                            ModelState.AddModelError(nameof(model.AssignedToUserId), "Only users with the Sales role can be assigned to leads.");
                        }
                    }
                    else if (!isActive)
                    {
                        // Existing owner kept but inactive - allow but no error (UserOptionsAsync will show them as inactive)
                    }
                    else if (!isSalesRole)
                    {
                        // Existing owner kept but no longer Sales role - allow but no error
                    }
                }
            }
            if (model.CustomerId.HasValue && !await _dbContext.Customers.AnyAsync(x => x.Id == model.CustomerId)) ModelState.AddModelError(nameof(model.CustomerId), "Selected customer no longer exists.");
            if (model.SportId.HasValue && !await _dbContext.Sports.AnyAsync(x => x.Id == model.SportId)) ModelState.AddModelError(nameof(model.SportId), "Selected sport no longer exists.");
            if (model.FacilityId.HasValue && !await _dbContext.Facilities.AnyAsync(x => x.Id == model.FacilityId)) ModelState.AddModelError(nameof(model.FacilityId), "Selected facility no longer exists.");
            if (model.CourtId.HasValue && !await _dbContext.Courts.AnyAsync(x => x.Id == model.CourtId)) ModelState.AddModelError(nameof(model.CourtId), "Selected court no longer exists.");
            if (model.MembershipPlanId.HasValue && !await _dbContext.MembershipPlans.AnyAsync(x => x.Id == model.MembershipPlanId)) ModelState.AddModelError(nameof(model.MembershipPlanId), "Selected membership plan no longer exists.");
            if (model.CourtId.HasValue && model.FacilityId.HasValue && await _dbContext.Courts.AnyAsync(x => x.Id == model.CourtId && x.FacilityId != model.FacilityId))
            {
                ModelState.AddModelError(nameof(model.CourtId), "Selected court does not belong to the selected facility.");
            }
        }

        private async Task ValidateOpportunityAsync(OpportunityFormViewModel model, int? id = null)
        {
            if (!await _dbContext.Leads.AnyAsync(x => x.Id == model.LeadId)) ModelState.AddModelError(nameof(model.LeadId), "Selected lead no longer exists.");
            if (model.AssignedToUserId == null) ModelState.AddModelError(nameof(model.AssignedToUserId), "Select an owner for this opportunity.");
            else if (!await _dbContext.Users.AnyAsync(x => x.Id == model.AssignedToUserId && x.IsActive))
            {
                // Same rule as leads: an existing inactive owner can be kept, but an
                // inactive user cannot be newly assigned.
                var keepsExistingOwner = id.HasValue
                    && await _dbContext.Opportunities.AnyAsync(x => x.Id == id && x.AssignedToUserId == model.AssignedToUserId);
                if (!keepsExistingOwner) ModelState.AddModelError(nameof(model.AssignedToUserId), "Assigned user is invalid or no longer active.");
            }
            if (model.CustomerId.HasValue)
            {
                if (!await _dbContext.Customers.AnyAsync(x => x.Id == model.CustomerId)) ModelState.AddModelError(nameof(model.CustomerId), "Selected customer no longer exists.");
                var leadCustomerId = await _dbContext.Leads.AsNoTracking().Where(x => x.Id == model.LeadId).Select(x => x.CustomerId).FirstOrDefaultAsync();
                if (leadCustomerId.HasValue && leadCustomerId.Value != model.CustomerId.Value)
                {
                    ModelState.AddModelError(nameof(model.CustomerId), "Customer must match the customer linked on the source lead.");
                }
            }
            if (model.SportId.HasValue && !await _dbContext.Sports.AnyAsync(x => x.Id == model.SportId)) ModelState.AddModelError(nameof(model.SportId), "Selected sport no longer exists.");
            if (model.FacilityId.HasValue && !await _dbContext.Facilities.AnyAsync(x => x.Id == model.FacilityId)) ModelState.AddModelError(nameof(model.FacilityId), "Selected facility no longer exists.");
            if (model.CourtId.HasValue && !await _dbContext.Courts.AnyAsync(x => x.Id == model.CourtId)) ModelState.AddModelError(nameof(model.CourtId), "Selected court no longer exists.");
            if (model.MembershipPlanId.HasValue && !await _dbContext.MembershipPlans.AnyAsync(x => x.Id == model.MembershipPlanId)) ModelState.AddModelError(nameof(model.MembershipPlanId), "Selected membership plan no longer exists.");
            if (model.ExpectedCloseDate < DateOnly.FromDateTime(DateTime.UtcNow)) ModelState.AddModelError(nameof(model.ExpectedCloseDate), "Expected close date cannot be in the past.");
            if (model.ActualCloseDate.HasValue && model.ActualCloseDate.Value > DateOnly.FromDateTime(DateTime.UtcNow)) ModelState.AddModelError(nameof(model.ActualCloseDate), "Actual close date cannot be in the future.");
            if (model.Stage == OpportunityStage.Lost && string.IsNullOrWhiteSpace(model.LossReason)) ModelState.AddModelError(nameof(model.LossReason), "Loss reason is required when the stage is Lost.");
            if (id == null && await _dbContext.Opportunities.AnyAsync(x => x.LeadId == model.LeadId)) ModelState.AddModelError(nameof(model.LeadId), "This lead already has an opportunity. Edit the existing opportunity instead.");
            if (id != null && await _dbContext.Opportunities.AnyAsync(x => x.LeadId == model.LeadId && x.Id != id)) ModelState.AddModelError(nameof(model.LeadId), "Another opportunity already exists for the selected lead.");
        }

        private async Task<List<LeadListItemViewModel>> FindDuplicateLeadsAsync(LeadFormViewModel model, int? id = null)
        {
            var recent = DateTime.UtcNow.AddDays(-7);
            var q = _dbContext.Leads.AsNoTracking().Where(x => x.IsActive && x.CreatedAt >= recent && x.Status != LeadStatus.Converted && x.Status != LeadStatus.Disqualified && (!id.HasValue || x.Id != id.Value));
            q = q.Where(x => (model.CustomerId != null && x.CustomerId == model.CustomerId) || (!string.IsNullOrWhiteSpace(model.Email) && x.Email == model.Email) || (!string.IsNullOrWhiteSpace(model.Phone) && x.Phone == model.Phone) || (!string.IsNullOrWhiteSpace(model.WhatsAppNumber) && x.WhatsAppNumber == model.WhatsAppNumber) || (x.CustomerName == model.CustomerName && x.ServiceInterest == model.ServiceInterest));
            return await q.Take(5).Select(x => new LeadListItemViewModel { Id = x.Id, LeadNumber = x.LeadNumber, CustomerName = x.CustomerName, Status = x.Status, ServiceInterest = x.ServiceInterest, CreatedAt = x.CreatedAt }).ToListAsync();
        }

        private void MapLead(LeadFormViewModel model, Lead lead)
        {
            lead.CustomerId = model.CustomerId; lead.CustomerName = model.CustomerName; lead.Email = model.Email; lead.Phone = model.Phone; lead.WhatsAppNumber = model.WhatsAppNumber; lead.OrganizationName = model.OrganizationName;
            lead.Source = model.Source; lead.ServiceInterest = model.ServiceInterest; lead.SportId = model.SportId; lead.FacilityId = model.FacilityId; lead.CourtId = model.CourtId; lead.MembershipPlanId = model.MembershipPlanId;
            lead.PreferredDate = model.PreferredDate; lead.PreferredStartTime = model.PreferredStartTime; lead.ExpectedPlayerCount = model.ExpectedPlayerCount;
            lead.InquirySubject = model.InquirySubject; lead.InquiryDetails = model.InquiryDetails; lead.AssignedToUserId = model.AssignedToUserId; lead.Status = model.Status; lead.Priority = model.Priority; lead.NextFollowUpAt = model.NextFollowUpAt;
            lead.QualificationNotes = model.QualificationNotes; lead.DisqualificationReason = model.DisqualificationReason; lead.InternalNotes = model.InternalNotes; lead.Tags = model.Tags; lead.IsActive = model.IsActive;
        }

        private LeadFormViewModel MapLeadForm(Lead lead) => new() { Id = lead.Id, LeadNumber = lead.LeadNumber, CustomerId = lead.CustomerId, CustomerName = lead.CustomerName, Email = lead.Email, Phone = lead.Phone, WhatsAppNumber = lead.WhatsAppNumber, OrganizationName = lead.OrganizationName, Source = lead.Source, ServiceInterest = lead.ServiceInterest, SportId = lead.SportId, FacilityId = lead.FacilityId, CourtId = lead.CourtId, MembershipPlanId = lead.MembershipPlanId, PreferredDate = lead.PreferredDate, PreferredStartTime = lead.PreferredStartTime, ExpectedPlayerCount = lead.ExpectedPlayerCount, InquirySubject = lead.InquirySubject, InquiryDetails = lead.InquiryDetails, AssignedToUserId = lead.AssignedToUserId, Status = lead.Status, Priority = lead.Priority, NextFollowUpAt = lead.NextFollowUpAt, QualificationNotes = lead.QualificationNotes, DisqualificationReason = lead.DisqualificationReason, InternalNotes = lead.InternalNotes, Tags = lead.Tags, IsActive = lead.IsActive };

        private void MapOpportunity(OpportunityFormViewModel model, Opportunity opportunity)
        {
            opportunity.LeadId = model.LeadId; opportunity.CustomerId = model.CustomerId; opportunity.Name = model.Name; opportunity.Type = model.Type; opportunity.Stage = model.Stage; opportunity.ExpectedValue = model.ExpectedValue; opportunity.ProbabilityPercentage = model.ProbabilityPercentage; opportunity.ExpectedCloseDate = model.ExpectedCloseDate; opportunity.ActualCloseDate = model.ActualCloseDate; opportunity.SportId = model.SportId; opportunity.FacilityId = model.FacilityId; opportunity.CourtId = model.CourtId; opportunity.MembershipPlanId = model.MembershipPlanId; opportunity.AssignedToUserId = model.AssignedToUserId; opportunity.CustomerRequirement = model.CustomerRequirement; opportunity.ProposedSolution = model.ProposedSolution; opportunity.CompetitorInformation = model.CompetitorInformation; opportunity.LossReason = model.LossReason; opportunity.Tags = model.Tags; opportunity.IsActive = model.IsActive;
        }

        private OpportunityFormViewModel MapOpportunityForm(Opportunity x) => new() { Id = x.Id, OpportunityNumber = x.OpportunityNumber, LeadId = x.LeadId, CustomerId = x.CustomerId, Name = x.Name, Type = x.Type, Stage = x.Stage, ExpectedValue = x.ExpectedValue, ProbabilityPercentage = x.ProbabilityPercentage, ExpectedCloseDate = x.ExpectedCloseDate, ActualCloseDate = x.ActualCloseDate, SportId = x.SportId, FacilityId = x.FacilityId, CourtId = x.CourtId, MembershipPlanId = x.MembershipPlanId, AssignedToUserId = x.AssignedToUserId, CustomerRequirement = x.CustomerRequirement, ProposedSolution = x.ProposedSolution, CompetitorInformation = x.CompetitorInformation, LossReason = x.LossReason, Tags = x.Tags, IsActive = x.IsActive };

        private async Task<LeadFormViewModel> AddLeadOptionsAsync(LeadFormViewModel model)
        {
            model.CustomerOptions = await _dbContext.Customers.AsNoTracking().OrderBy(x => x.FirstName).Select(x => new SelectListItem(x.FirstName + " " + x.LastName + " - " + x.PrimaryPhone, x.Id.ToString(), x.Id == model.CustomerId)).ToListAsync();
            model.UserOptions = await LeadUserOptionsAsync(model.AssignedToUserId);
            model.SportOptions = await SportOptionsAsync(model.SportId);
            model.FacilityOptions = await _dbContext.Facilities.AsNoTracking().Where(x => x.IsActive).OrderBy(x => x.DisplayOrder).Select(x => new SelectListItem(x.Name, x.Id.ToString(), x.Id == model.FacilityId)).ToListAsync();
            model.CourtOptions = await _dbContext.Courts.AsNoTracking().Where(x => x.IsActive).OrderBy(x => x.DisplayOrder).Select(x => new SelectListItem(x.Name, x.Id.ToString(), x.Id == model.CourtId)).ToListAsync();
            model.MembershipPlanOptions = await _dbContext.MembershipPlans.AsNoTracking().Where(x => x.IsActive).OrderBy(x => x.DisplayOrder).Select(x => new SelectListItem(x.Name, x.Id.ToString(), x.Id == model.MembershipPlanId)).ToListAsync();
            return model;
        }

        private async Task<OpportunityFormViewModel> AddOpportunityOptionsAsync(OpportunityFormViewModel model)
        {
            model.CurrencySymbol = (await GetSettingsAsync()).CurrencySymbol ?? "Rs";
            model.LeadOptions = await _dbContext.Leads.AsNoTracking().Where(x => x.Status == LeadStatus.Qualified || x.Id == model.LeadId).OrderByDescending(x => x.CreatedAt).Select(x => new SelectListItem(x.LeadNumber + " - " + x.CustomerName, x.Id.ToString(), x.Id == model.LeadId)).ToListAsync();
            model.CustomerOptions = await _dbContext.Customers.AsNoTracking().OrderBy(x => x.FirstName).Select(x => new SelectListItem(x.FirstName + " " + x.LastName + " - " + x.PrimaryPhone, x.Id.ToString(), x.Id == model.CustomerId)).ToListAsync();
            model.UserOptions = await UserOptionsAsync(model.AssignedToUserId);
            model.SportOptions = await SportOptionsAsync(model.SportId);
            model.FacilityOptions = await _dbContext.Facilities.AsNoTracking().Where(x => x.IsActive).OrderBy(x => x.DisplayOrder).Select(x => new SelectListItem(x.Name, x.Id.ToString(), x.Id == model.FacilityId)).ToListAsync();
            model.CourtOptions = await _dbContext.Courts.AsNoTracking().Where(x => x.IsActive).OrderBy(x => x.DisplayOrder).Select(x => new SelectListItem(x.Name, x.Id.ToString(), x.Id == model.CourtId)).ToListAsync();
            model.MembershipPlanOptions = await _dbContext.MembershipPlans.AsNoTracking().Where(x => x.IsActive).OrderBy(x => x.DisplayOrder).Select(x => new SelectListItem(x.Name, x.Id.ToString(), x.Id == model.MembershipPlanId)).ToListAsync();
            return model;
        }

        /// <summary>
        /// Users available for lead assignment (active Sales-role users only).
        /// When a lead is already assigned to a user who has since been deactivated
        /// or changed roles, that user is still listed and selected (labelled
        /// "(inactive)") so the dropdown reflects the saved owner instead of
        /// showing "Unassigned" and silently clearing the assignment on next save.
        /// Display format: "UserID | Username | [Sales]"
        /// </summary>
        private async Task<List<SelectListItem>> LeadUserOptionsAsync(int? selected = null)
        {
            var selectedId = selected ?? 0;
            var users = await _dbContext.Users.AsNoTracking()
                .Where(x => (x.IsActive && x.Role == UserRole.BookingManager) || x.Id == selectedId)
                .OrderBy(x => x.IsActive && x.Role == UserRole.BookingManager ? 0 : 1)
                .ThenBy(x => x.FirstName)
                .Select(x => new { x.Id, x.FirstName, x.LastName, x.Email, x.IsActive, x.Role })
                .ToListAsync();

            if (!users.Any())
            {
                return new List<SelectListItem>
                {
                    new SelectListItem("No eligible Sales users available", "", false, true) { Disabled = true }
                };
            }

            return users.Select(x =>
            {
                var isEligible = x.IsActive && x.Role == UserRole.BookingManager;
                var username = string.IsNullOrWhiteSpace(x.Email) ? $"{x.FirstName}.{x.LastName}" : x.Email;
                var label = $"{x.Id} | {username} | [Sales]" + (isEligible ? string.Empty : " (inactive)");
                return new SelectListItem(label, x.Id.ToString(), x.Id == selectedId);
            }).ToList();
        }

        /// <summary>
        /// Users available for assignment (active accounts only). When a record is
        /// already owned by someone who has since been deactivated, that owner is
        /// still listed and selected (and labelled "(inactive)") so the dropdown
        /// reflects the saved owner instead of showing "Unassigned" and silently
        /// clearing the assignment the next time the form is saved.
        /// </summary>
        private async Task<List<SelectListItem>> UserOptionsAsync(int? selected = null)
        {
            var selectedId = selected ?? 0;
            var users = await _dbContext.Users.AsNoTracking()
                .Where(x => x.IsActive || x.Id == selectedId)
                .OrderBy(x => x.IsActive ? 0 : 1)
                .ThenBy(x => x.FirstName)
                .Select(x => new { x.Id, x.FirstName, x.LastName, x.IsActive })
                .ToListAsync();
            return users.Select(x => new SelectListItem(
                x.FirstName + " " + x.LastName + (x.IsActive ? string.Empty : " (inactive)"),
                x.Id.ToString(),
                x.Id == selectedId)).ToList();
        }
        private async Task<List<SelectListItem>> SportOptionsAsync(int? selected = null) => await _dbContext.Sports.AsNoTracking().Where(x => x.IsActive).OrderBy(x => x.DisplayOrder).Select(x => new SelectListItem(x.Name, x.Id.ToString(), x.Id == selected)).ToListAsync();

        private async Task AddLeadActivityAsync(int leadId, LeadActivityType type, string subject, string? description, DateTime? followUpDueAt, bool isCompleted, DateTime? activityDate = null, bool save = true)
        {
            _dbContext.LeadActivities.Add(new LeadActivity { LeadId = leadId, ActivityType = type, Subject = subject, Description = description, ActivityDate = activityDate ?? DateTime.UtcNow, FollowUpDueAt = followUpDueAt, IsCompleted = isCompleted, CreatedByUserId = CurrentUserId(), CreatedAt = DateTime.UtcNow });
            if (save) await _dbContext.SaveChangesAsync();
        }

        private async Task AddOpportunityActivityAsync(int opportunityId, OpportunityActivityType type, string subject, string? description, DateTime? followUpDueAt, bool isCompleted, DateTime? activityDate = null, bool save = true)
        {
            _dbContext.OpportunityActivities.Add(new OpportunityActivity { OpportunityId = opportunityId, ActivityType = type, Subject = subject, Description = description, ActivityDate = activityDate ?? DateTime.UtcNow, FollowUpDueAt = followUpDueAt, IsCompleted = isCompleted, CreatedByUserId = CurrentUserId(), CreatedAt = DateTime.UtcNow });
            if (save) await _dbContext.SaveChangesAsync();
        }

        /// <summary>Earliest still-pending scheduled follow-up on a lead (null when nothing is pending).</summary>
        private async Task<DateTime?> EarliestPendingLeadFollowUpAsync(int leadId) =>
            await _dbContext.LeadActivities.Where(x => x.LeadId == leadId && !x.IsCompleted && x.FollowUpDueAt != null).OrderBy(x => x.FollowUpDueAt).Select(x => x.FollowUpDueAt).FirstOrDefaultAsync();

        /// <summary>Earliest still-pending scheduled follow-up on an opportunity (null when nothing is pending).</summary>
        private async Task<DateTime?> EarliestPendingOpportunityFollowUpAsync(int opportunityId) =>
            await _dbContext.OpportunityActivities.Where(x => x.OpportunityId == opportunityId && !x.IsCompleted && x.FollowUpDueAt != null).OrderBy(x => x.FollowUpDueAt).Select(x => x.FollowUpDueAt).FirstOrDefaultAsync();

        private async Task<string> GenerateLeadNumberAsync() => await GenerateNumberAsync((await GetSettingsAsync()).LeadPrefix, _dbContext.Leads.Select(x => x.LeadNumber));
        private async Task<string> GenerateOpportunityNumberAsync() => await GenerateNumberAsync((await GetSettingsAsync()).OpportunityPrefix, _dbContext.Opportunities.Select(x => x.OpportunityNumber));
        private async Task<string> GenerateNumberAsync(string? prefix, IQueryable<string> query)
        {
            prefix = string.IsNullOrWhiteSpace(prefix) ? "CRM" : prefix.Trim().ToUpperInvariant();
            var latest = await query.Where(x => x.StartsWith(prefix + "-")).OrderByDescending(x => x).FirstOrDefaultAsync();
            var next = 1;
            if (!string.IsNullOrWhiteSpace(latest) && int.TryParse(latest.Split('-').LastOrDefault(), out var n)) next = n + 1;
            return $"{prefix}-{next:000000}";
        }

        private async Task<SystemSettings> GetSettingsAsync() => await _dbContext.SystemSettings.AsNoTracking().FirstOrDefaultAsync() ?? new SystemSettings { CurrencySymbol = "Rs", LeadPrefix = "LEAD", OpportunityPrefix = "OPP" };
        private int CurrentUserId() => int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : 0;
        private bool CanAccessCrm() => RolePermissions.CanAccessModule(User, "CRM");

        /// <summary>
        /// Tells every connected ERP tab that the "New Website Inquiries" counter
        /// changed because a website inquiry left the New status through the normal
        /// Lead Management workflow (edit, qualify, disqualify or convert).
        /// Best-effort: a broadcast failure must never affect the saved workflow result.
        /// </summary>
        private async Task NotifyWebsiteInquiryReviewedAsync()
        {
            try
            {
                await _websiteInquiryNotifier.BroadcastCountAsync();
            }
            catch (Exception exception)
            {
                _logger.LogWarning(exception, "Website inquiry counter broadcast failed after a status change.");
            }
        }

        private bool CanCreateLeads() => RolePermissions.TryGetCurrentRole(User, out var role) && role is UserRole.SuperAdmin or UserRole.Admin or UserRole.BookingManager or UserRole.Receptionist;
        private bool CanManageLeads() => RolePermissions.TryGetCurrentRole(User, out var role) && role is UserRole.SuperAdmin or UserRole.Admin or UserRole.BookingManager;
        private bool CanManageOpportunities() => RolePermissions.TryGetCurrentRole(User, out var role) && role is UserRole.SuperAdmin or UserRole.Admin or UserRole.BookingManager;
        private bool CanCreateQuotation() => RolePermissions.TryGetCurrentRole(User, out var role) && role is UserRole.SuperAdmin or UserRole.Admin or UserRole.BookingManager or UserRole.Receptionist;
        private bool IsReadOnlyRole() => RolePermissions.TryGetCurrentRole(User, out var role) && role is UserRole.Viewer or UserRole.FinanceManager;
        private static string NormalizeTab(string? tab) => tab?.ToLowerInvariant() is "leads" or "opportunities" ? tab!.ToLowerInvariant() : "overview";
        private static bool HasContact(Lead lead) => LeadInquiryRules.HasContact(lead.Email, lead.Phone, lead.WhatsAppNumber);
        private static bool HasContact(LeadFormViewModel model) => LeadInquiryRules.HasContact(model.Email, model.Phone, model.WhatsAppNumber);
        // Shared business rules (single source of truth with the public website
        // inquiry endpoint). See LeadInquiryRules - extracted verbatim, no behavior change.
        private const string PhoneFormatMessage = LeadInquiryRules.PhoneFormatMessage;
        private const string EmailFormatMessage = LeadInquiryRules.EmailFormatMessage;
        private static bool HasValidPakPhone(string? value) => LeadInquiryRules.HasValidPakPhone(value);
        private static bool HasValidComEmail(string? value) => LeadInquiryRules.HasValidComEmail(value);
        private static string DescribeDbError(DbUpdateException exception)
        {
            var message = exception.GetBaseException().Message;
            if (message.Contains("UNIQUE", StringComparison.OrdinalIgnoreCase) || message.Contains("duplicate key", StringComparison.OrdinalIgnoreCase) || message.Contains("Cannot insert duplicate", StringComparison.OrdinalIgnoreCase))
            {
                return "This record conflicts with an existing CRM record. Check the lead or opportunity number, or the selected lead for a duplicate.";
            }
            if (message.Contains("FOREIGN KEY", StringComparison.OrdinalIgnoreCase) || message.Contains("REFERENCE", StringComparison.OrdinalIgnoreCase))
            {
                return "The selected related record no longer exists. Refresh the page and try again.";
            }
            return "The record could not be saved because of a database conflict. Please review the values and try again.";
        }
        private static string? Clean(string? value) => LeadInquiryRules.Clean(value);
        private static string? NormalizeEmail(string? value) => LeadInquiryRules.NormalizeEmail(value);
        private static string? NormalizePhone(string? value) => LeadInquiryRules.NormalizePhone(value);
        private static void NormalizeLead(LeadFormViewModel model) { model.CustomerName = model.CustomerName.Trim(); model.Email = NormalizeEmail(model.Email); model.Phone = NormalizePhone(model.Phone); model.WhatsAppNumber = NormalizePhone(model.WhatsAppNumber); model.OrganizationName = Clean(model.OrganizationName); model.InquirySubject = Clean(model.InquirySubject); model.InquiryDetails = Clean(model.InquiryDetails); model.QualificationNotes = Clean(model.QualificationNotes); model.DisqualificationReason = Clean(model.DisqualificationReason); model.InternalNotes = Clean(model.InternalNotes); model.Tags = Clean(model.Tags); }
        private static void NormalizeOpportunity(OpportunityFormViewModel model) { model.Name = model.Name.Trim(); model.CustomerRequirement = Clean(model.CustomerRequirement); model.ProposedSolution = Clean(model.ProposedSolution); model.CompetitorInformation = Clean(model.CompetitorInformation); model.LossReason = Clean(model.LossReason); model.Tags = Clean(model.Tags); if (model.ProbabilityPercentage == 0 && model.Stage != OpportunityStage.Lost) model.ProbabilityPercentage = StageProbability(model.Stage); }
        private static OpportunityType MapOpportunityType(LeadServiceInterest interest) => interest switch { LeadServiceInterest.Membership => OpportunityType.Membership, LeadServiceInterest.CorporateBooking => OpportunityType.CorporatePackage, LeadServiceInterest.Tournament => OpportunityType.Tournament, LeadServiceInterest.Event => OpportunityType.Event, LeadServiceInterest.Coaching => OpportunityType.Coaching, _ => OpportunityType.CourtBooking };
        private static int StageProbability(OpportunityStage stage) => stage switch { OpportunityStage.Qualification => 10, OpportunityStage.NeedsAnalysis => 25, OpportunityStage.ProposalPreparation => 40, OpportunityStage.ProposalSent => 60, OpportunityStage.Negotiation => 80, OpportunityStage.Won => 100, OpportunityStage.Lost => 0, OpportunityStage.OnHold => 20, _ => 10 };
        private static bool CanTransition(OpportunityStage from, OpportunityStage to) => from == to || from == OpportunityStage.OnHold || to == OpportunityStage.OnHold || from is not (OpportunityStage.Won or OpportunityStage.Lost);
        private static void ApplyStageRules(Opportunity opportunity) { if (opportunity.Stage == OpportunityStage.Won) { opportunity.ProbabilityPercentage = 100; opportunity.WonAt ??= DateTime.UtcNow; opportunity.ActualCloseDate ??= DateOnly.FromDateTime(DateTime.UtcNow); } if (opportunity.Stage == OpportunityStage.Lost) { opportunity.ProbabilityPercentage = 0; opportunity.LostAt ??= DateTime.UtcNow; opportunity.ActualCloseDate ??= DateOnly.FromDateTime(DateTime.UtcNow); } }
        private async Task<Customer?> FindCustomerForLeadAsync(Lead lead) => await _dbContext.Customers.FirstOrDefaultAsync(x => (!string.IsNullOrWhiteSpace(lead.Email) && x.Email == lead.Email) || (!string.IsNullOrWhiteSpace(lead.Phone) && x.PrimaryPhone == lead.Phone) || (!string.IsNullOrWhiteSpace(lead.WhatsAppNumber) && x.WhatsAppNumber == lead.WhatsAppNumber));
        private static string FirstName(string name) => (name.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? "Customer").Trim();
        private static string LastName(string name) => string.Join(" ", name.Split(' ', StringSplitOptions.RemoveEmptyEntries).Skip(1)).Trim() is { Length: > 0 } value ? value : "Lead";

        /// <summary>
        /// Shared conversion path for every Lead -> Opportunity entry point.
        /// Returns null when the lead was already converted or already has an
        /// opportunity (the one-to-one relationship is also enforced by a unique
        /// index, this just fails fast with a clear message instead).
        /// </summary>
        private async Task<Opportunity?> ConvertLeadAsync(Lead lead, OpportunityFormViewModel model, bool createCustomerFromLead)
        {
            if (lead.IsConverted || await _dbContext.Opportunities.AnyAsync(x => x.LeadId == lead.Id)) return null;
            await using var transaction = await _dbContext.Database.BeginTransactionAsync();
            var customerId = lead.CustomerId ?? model.CustomerId;
            if (!customerId.HasValue && createCustomerFromLead)
            {
                var existing = await FindCustomerForLeadAsync(lead);
                if (existing != null)
                {
                    customerId = existing.Id;
                }
                else
                {
                    var customer = new Customer
                    {
                        CustomerCode = await _customerCodeService.GenerateNextCodeAsync(),
                        FirstName = FirstName(lead.CustomerName),
                        LastName = LastName(lead.CustomerName),
                        Email = lead.Email,
                        PrimaryPhone = lead.Phone ?? lead.WhatsAppNumber ?? string.Empty,
                        WhatsAppNumber = lead.WhatsAppNumber,
                        OrganizationName = lead.OrganizationName,
                        CustomerType = string.IsNullOrWhiteSpace(lead.OrganizationName) ? CustomerType.Individual : CustomerType.Corporate,
                        CustomerSource = CustomerSource.Other,
                        PreferredContactMethod = PreferredContactMethod.Phone,
                        Country = "Pakistan",
                        IsActive = true,
                        CreatedAt = DateTime.UtcNow
                    };
                    _dbContext.Customers.Add(customer);
                    await _dbContext.SaveChangesAsync();
                    customerId = customer.Id;
                }
            }

            var opportunity = new Opportunity();
            MapOpportunity(model, opportunity);
            opportunity.CustomerId = customerId;
            opportunity.LeadId = lead.Id;
            opportunity.OpportunityNumber = await GenerateOpportunityNumberAsync();
            opportunity.CreatedAt = DateTime.UtcNow;
            opportunity.CreatedByUserId = CurrentUserId();
            ApplyStageRules(opportunity);

            _dbContext.Opportunities.Add(opportunity);
            await _dbContext.SaveChangesAsync();

            lead.CustomerId = customerId;
            lead.IsConverted = true;
            lead.Status = LeadStatus.Converted;
            lead.ConvertedAt = DateTime.UtcNow;
            lead.ConvertedOpportunityId = opportunity.Id;

            await AddLeadActivityAsync(lead.Id, LeadActivityType.Conversion, "Converted to opportunity", opportunity.OpportunityNumber, null, true, save: false);
            _dbContext.OpportunityActivities.Add(new OpportunityActivity
            {
                OpportunityId = opportunity.Id,
                ActivityType = OpportunityActivityType.Note,
                Subject = "Opportunity created",
                Description = "Created from qualified lead.",
                ActivityDate = DateTime.UtcNow,
                CreatedAt = DateTime.UtcNow,
                CreatedByUserId = CurrentUserId(),
                IsCompleted = true
            });
            await _dbContext.SaveChangesAsync();
            await transaction.CommitAsync();
            return opportunity;
        }
    }
}
