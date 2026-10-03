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

        public CRMController(ApplicationDbContext dbContext, ICustomerCodeService customerCodeService)
        {
            _dbContext = dbContext;
            _customerCodeService = customerCodeService;
        }

        [HttpGet]
        public async Task<IActionResult> Index(string tab = "overview", string viewMode = "list", string? search = null, LeadStatus? status = null, LeadSource? source = null, LeadPriority? priority = null, LeadTemperature? temperature = null, LeadServiceInterest? serviceInterest = null, int? assignedToUserId = null, int? sportId = null, OpportunityStage? stage = null, OpportunityType? type = null, string sortBy = "newest", int page = 1, int pageSize = 10)
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
                var query = _dbContext.Leads.AsNoTracking().Include(x => x.Sport).Include(x => x.AssignedToUser).AsQueryable();
                query = ApplyLeadFilters(query, search, status, source, priority, temperature, serviceInterest, assignedToUserId, sportId);
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
                    Temperature = x.Temperature,
                    NextFollowUpAt = x.NextFollowUpAt,
                    CreatedAt = x.CreatedAt,
                    IsWebsiteInquiry = x.IsWebsiteInquiry,
                    IsConverted = x.IsConverted,
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
            _dbContext.Leads.Add(lead);
            await _dbContext.SaveChangesAsync();
            await AddLeadActivityAsync(lead.Id, LeadActivityType.Note, "Lead created", "Lead was created manually.", null, true);
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
        public async Task<IActionResult> EditLead(int id, LeadFormViewModel model)
        {
            if (!CanCreateLeads()) return RedirectToAction("NotFound", "Error");
            if (model.Id != id) return RedirectToAction("NotFound", "Error");
            NormalizeLead(model);
            await ValidateLeadAsync(model, id);
            var lead = await _dbContext.Leads.FirstOrDefaultAsync(x => x.Id == id);
            if (lead == null) return RedirectToAction("NotFound", "Error");
            if (!ModelState.IsValid) return View("LeadForm", await AddLeadOptionsAsync(model));
            var previousStatus = lead.Status;
            var previousOwner = lead.AssignedToUserId;
            MapLead(model, lead);
            lead.UpdatedAt = DateTime.UtcNow;
            lead.UpdatedByUserId = CurrentUserId();
            if (previousStatus != lead.Status) await AddLeadActivityAsync(lead.Id, LeadActivityType.StatusChange, "Status changed", $"{previousStatus.GetDisplayName()} to {lead.Status.GetDisplayName()}", null, true, save: false);
            if (previousOwner != lead.AssignedToUserId) await AddLeadActivityAsync(lead.Id, LeadActivityType.Assignment, "Assignment changed", "Lead owner was updated.", null, true, save: false);
            await _dbContext.SaveChangesAsync();
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
            await AddLeadActivityAsync(id, activityType, subject.Trim(), description?.Trim(), followUpDueAt, isCompleted, activityDate);
            if (followUpDueAt.HasValue) lead.NextFollowUpAt = followUpDueAt;
            if (activityType is LeadActivityType.PhoneCall or LeadActivityType.WhatsApp or LeadActivityType.Email or LeadActivityType.Meeting) lead.LastContactedAt = DateTime.UtcNow;
            await _dbContext.SaveChangesAsync();
            TempData.SetToast("success", "Activity Added", "Lead activity recorded.");
            return RedirectToAction(nameof(LeadDetails), new { id });
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> QualifyLead(int id, string qualificationNotes)
        {
            if (!CanManageLeads()) return RedirectToAction("NotFound", "Error");
            var lead = await _dbContext.Leads.FirstOrDefaultAsync(x => x.Id == id);
            if (lead == null || !lead.IsActive || lead.IsConverted) return RedirectToAction("NotFound", "Error");
            if (lead.AssignedToUserId == null || string.IsNullOrWhiteSpace(qualificationNotes) || !HasContact(lead))
            {
                TempData.SetToast("warning", "Qualification Blocked", "Lead needs contact information, owner and qualification notes.");
                return RedirectToAction(nameof(LeadDetails), new { id });
            }
            lead.Status = LeadStatus.Qualified;
            lead.QualifiedAt = DateTime.UtcNow;
            lead.QualificationNotes = qualificationNotes.Trim();
            await AddLeadActivityAsync(id, LeadActivityType.Qualification, "Lead qualified", qualificationNotes.Trim(), null, true, save: false);
            await _dbContext.SaveChangesAsync();
            TempData.SetToast("success", "Lead Qualified", "Lead qualified successfully.");
            return RedirectToAction(nameof(LeadDetails), new { id });
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> DisqualifyLead(int id, string reason)
        {
            if (!CanManageLeads()) return RedirectToAction("NotFound", "Error");
            if (string.IsNullOrWhiteSpace(reason) || reason.Trim().Length < 10)
            {
                TempData.SetToast("warning", "Reason Required", "Disqualification reason must be at least 10 characters.");
                return RedirectToAction(nameof(LeadDetails), new { id });
            }
            var lead = await _dbContext.Leads.FirstOrDefaultAsync(x => x.Id == id);
            if (lead == null || !lead.IsActive) return RedirectToAction("NotFound", "Error");
            lead.Status = LeadStatus.Disqualified;
            lead.DisqualifiedAt = DateTime.UtcNow;
            lead.DisqualificationReason = reason.Trim();
            await AddLeadActivityAsync(id, LeadActivityType.Disqualification, "Lead disqualified", reason.Trim(), null, true, save: false);
            await _dbContext.SaveChangesAsync();
            TempData.SetToast("success", "Lead Disqualified", "Lead history has been preserved.");
            return RedirectToAction(nameof(LeadDetails), new { id });
        }

        [HttpGet]
        public async Task<IActionResult> ConvertToOpportunity(int id)
        {
            if (!CanManageOpportunities()) return RedirectToAction("NotFound", "Error");
            var lead = await _dbContext.Leads.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
            if (lead == null || lead.Status != LeadStatus.Qualified || lead.IsConverted) return RedirectToAction("NotFound", "Error");
            var model = new OpportunityFormViewModel
            {
                LeadId = lead.Id,
                CustomerId = lead.CustomerId,
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
        public async Task<IActionResult> ConvertToOpportunity(int id, OpportunityFormViewModel model, bool createCustomerFromLead = false)
        {
            if (!CanManageOpportunities()) return RedirectToAction("NotFound", "Error");
            var lead = await _dbContext.Leads.FirstOrDefaultAsync(x => x.Id == id);
            if (lead == null || lead.Status != LeadStatus.Qualified || lead.IsConverted || lead.AssignedToUserId == null || !HasContact(lead))
            {
                TempData.SetToast("warning", "Conversion Blocked", "Only qualified unconverted leads with owner and contact can be converted.");
                return RedirectToAction(nameof(LeadDetails), new { id });
            }
            model.LeadId = id;
            NormalizeOpportunity(model);
            await ValidateOpportunityAsync(model);
            if (!ModelState.IsValid) return View("OpportunityForm", await AddOpportunityOptionsAsync(model));
            await using var transaction = await _dbContext.Database.BeginTransactionAsync();
            var customerId = lead.CustomerId ?? model.CustomerId;
            if (!customerId.HasValue && createCustomerFromLead)
            {
                var existing = await FindCustomerForLeadAsync(lead);
                if (existing != null) customerId = existing.Id;
                else
                {
                    var customer = new Customer { CustomerCode = await _customerCodeService.GenerateNextCodeAsync(), FirstName = FirstName(lead.CustomerName), LastName = LastName(lead.CustomerName), Email = lead.Email, PrimaryPhone = lead.Phone ?? lead.WhatsAppNumber ?? string.Empty, WhatsAppNumber = lead.WhatsAppNumber, OrganizationName = lead.OrganizationName, CustomerType = string.IsNullOrWhiteSpace(lead.OrganizationName) ? CustomerType.Individual : CustomerType.Corporate, CustomerSource = CustomerSource.Other, PreferredContactMethod = PreferredContactMethod.Phone, Country = "Pakistan", IsActive = true, CreatedAt = DateTime.UtcNow };
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
            _dbContext.OpportunityActivities.Add(new OpportunityActivity { OpportunityId = opportunity.Id, ActivityType = OpportunityActivityType.Note, Subject = "Opportunity created", Description = "Created from qualified lead.", ActivityDate = DateTime.UtcNow, CreatedAt = DateTime.UtcNow, CreatedByUserId = CurrentUserId(), IsCompleted = true });
            await _dbContext.SaveChangesAsync();
            await transaction.CommitAsync();
            TempData.SetToast("success", "Opportunity Created", "Lead converted successfully.");
            return RedirectToAction(nameof(OpportunityDetails), new { id = opportunity.Id });
        }

        [HttpGet]
        public async Task<IActionResult> CreateOpportunity()
        {
            if (!CanManageOpportunities()) return RedirectToAction("NotFound", "Error");
            return View("OpportunityForm", await AddOpportunityOptionsAsync(new OpportunityFormViewModel()));
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateOpportunity(OpportunityFormViewModel model)
        {
            if (!CanManageOpportunities()) return RedirectToAction("NotFound", "Error");
            NormalizeOpportunity(model);
            await ValidateOpportunityAsync(model);
            if (!ModelState.IsValid) return View("OpportunityForm", await AddOpportunityOptionsAsync(model));
            var lead = await _dbContext.Leads.FirstOrDefaultAsync(x => x.Id == model.LeadId);
            if (lead == null || lead.IsConverted) return RedirectToAction("NotFound", "Error");
            await using var transaction = await _dbContext.Database.BeginTransactionAsync();
            var opportunity = new Opportunity();
            MapOpportunity(model, opportunity);
            opportunity.OpportunityNumber = await GenerateOpportunityNumberAsync();
            opportunity.CreatedAt = DateTime.UtcNow;
            opportunity.CreatedByUserId = CurrentUserId();
            ApplyStageRules(opportunity);
            _dbContext.Opportunities.Add(opportunity);
            await _dbContext.SaveChangesAsync();
            lead.IsConverted = true; lead.Status = LeadStatus.Converted; lead.ConvertedAt = DateTime.UtcNow; lead.ConvertedOpportunityId = opportunity.Id;
            _dbContext.OpportunityActivities.Add(new OpportunityActivity { OpportunityId = opportunity.Id, ActivityType = OpportunityActivityType.Note, Subject = "Opportunity created", ActivityDate = DateTime.UtcNow, CreatedAt = DateTime.UtcNow, CreatedByUserId = CurrentUserId(), IsCompleted = true });
            await _dbContext.SaveChangesAsync();
            await transaction.CommitAsync();
            TempData.SetToast("success", "Opportunity Created", "Opportunity created successfully.");
            return RedirectToAction(nameof(OpportunityDetails), new { id = opportunity.Id });
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
        public async Task<IActionResult> EditOpportunity(int id, OpportunityFormViewModel model)
        {
            if (!CanManageOpportunities()) return RedirectToAction("NotFound", "Error");
            if (model.Id != id) return RedirectToAction("NotFound", "Error");
            NormalizeOpportunity(model);
            await ValidateOpportunityAsync(model, id);
            var opportunity = await _dbContext.Opportunities.FirstOrDefaultAsync(x => x.Id == id);
            if (opportunity == null) return RedirectToAction("NotFound", "Error");
            if (!CanTransition(opportunity.Stage, model.Stage))
            {
                ModelState.AddModelError(nameof(model.Stage), "Invalid stage transition.");
            }
            if (!ModelState.IsValid) return View("OpportunityForm", await AddOpportunityOptionsAsync(model));
            var oldStage = opportunity.Stage;
            MapOpportunity(model, opportunity);
            opportunity.UpdatedAt = DateTime.UtcNow;
            opportunity.UpdatedByUserId = CurrentUserId();
            ApplyStageRules(opportunity);
            if (oldStage != opportunity.Stage)
            {
                _dbContext.OpportunityActivities.Add(new OpportunityActivity { OpportunityId = opportunity.Id, ActivityType = OpportunityActivityType.StageChange, Subject = "Stage changed", Description = $"{oldStage.GetDisplayName()} to {opportunity.Stage.GetDisplayName()}", ActivityDate = DateTime.UtcNow, CreatedAt = DateTime.UtcNow, CreatedByUserId = CurrentUserId(), IsCompleted = true });
            }
            await _dbContext.SaveChangesAsync();
            TempData.SetToast("success", "Opportunity Updated", "Opportunity updated successfully.");
            return RedirectToAction(nameof(OpportunityDetails), new { id });
        }

        [HttpGet]
        public async Task<IActionResult> OpportunityDetails(int id)
        {
            var opportunity = await _dbContext.Opportunities.AsNoTracking()
                .Include(x => x.Lead).Include(x => x.Customer).Include(x => x.Sport).Include(x => x.Facility).Include(x => x.Court).Include(x => x.MembershipPlan)
                .Include(x => x.AssignedToUser).Include(x => x.Activities).ThenInclude(x => x.CreatedByUser)
                .FirstOrDefaultAsync(x => x.Id == id);
            if (opportunity == null) return RedirectToAction("NotFound", "Error");
            ViewBag.CurrencySymbol = (await GetSettingsAsync()).CurrencySymbol ?? "Rs";
            ViewBag.CanManageOpportunities = CanManageOpportunities();
            return View(opportunity);
        }

        [HttpGet]
        public async Task<IActionResult> FollowUps()
        {
            if (!CanAccessCrm()) return RedirectToAction("NotFound", "Error");
            var model = await BuildFollowUpsAsync();
            return View(model);
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> CompleteFollowUp(string recordType, int activityId)
        {
            if (!CanCreateLeads()) return RedirectToAction("NotFound", "Error");
            if (recordType == "Lead")
            {
                var item = await _dbContext.LeadActivities.Include(x => x.Lead).FirstOrDefaultAsync(x => x.Id == activityId);
                if (item == null) return RedirectToAction("NotFound", "Error");
                item.IsCompleted = true; item.UpdatedAt = DateTime.UtcNow;
                item.Lead.NextFollowUpAt = await _dbContext.LeadActivities.Where(x => x.LeadId == item.LeadId && !x.IsCompleted && x.FollowUpDueAt != null && x.Id != item.Id).OrderBy(x => x.FollowUpDueAt).Select(x => x.FollowUpDueAt).FirstOrDefaultAsync();
            }
            else
            {
                var item = await _dbContext.OpportunityActivities.FirstOrDefaultAsync(x => x.Id == activityId);
                if (item == null) return RedirectToAction("NotFound", "Error");
                item.IsCompleted = true; item.UpdatedAt = DateTime.UtcNow;
            }
            await _dbContext.SaveChangesAsync();
            TempData.SetToast("success", "Follow-up Completed", "Follow-up marked as complete.");
            return RedirectToAction(nameof(FollowUps));
        }

        private async Task AddDashboardAsync(CrmIndexViewModel model)
        {
            var openStatuses = new[] { LeadStatus.New, LeadStatus.Contacted, LeadStatus.FollowUp, LeadStatus.Qualified };
            model.TotalOpenLeads = await _dbContext.Leads.CountAsync(x => openStatuses.Contains(x.Status) && x.IsActive);
            model.NewLeads = await _dbContext.Leads.CountAsync(x => x.Status == LeadStatus.New && x.IsActive);
            model.QualifiedLeads = await _dbContext.Leads.CountAsync(x => x.Status == LeadStatus.Qualified && x.IsActive);
            model.OpportunityCount = await _dbContext.Opportunities.CountAsync(x => x.IsActive);
            model.PipelineValue = await _dbContext.Opportunities.Where(x => x.IsActive && x.Stage != OpportunityStage.Lost).SumAsync(x => x.ExpectedValue);
            model.WeightedPipeline = await _dbContext.Opportunities.Where(x => x.IsActive && x.Stage != OpportunityStage.Lost).SumAsync(x => x.ExpectedValue * x.ProbabilityPercentage / 100m);
            model.OverdueFollowUps = await _dbContext.Leads.CountAsync(x => x.NextFollowUpAt != null && x.NextFollowUpAt < DateTime.UtcNow && openStatuses.Contains(x.Status));
            var totalLeads = await _dbContext.Leads.CountAsync();
            var converted = await _dbContext.Leads.CountAsync(x => x.IsConverted);
            model.ConversionRate = totalLeads == 0 ? 0 : Math.Round(converted * 100m / totalLeads, 1);
            model.LeadFunnel = await _dbContext.Leads.GroupBy(x => x.Status).Select(x => new { x.Key, Count = x.Count() }).ToDictionaryAsync(x => x.Key, x => x.Count);
            model.PipelineByStage = await _dbContext.Opportunities.GroupBy(x => x.Stage).Select(x => new { x.Key, Value = x.Sum(o => o.ExpectedValue) }).ToDictionaryAsync(x => x.Key, x => x.Value);
            model.LeadsBySource = await _dbContext.Leads.GroupBy(x => x.Source).Select(x => new { x.Key, Count = x.Count() }).ToDictionaryAsync(x => x.Key, x => x.Count);
            model.FollowUps = (await BuildFollowUpsAsync()).Take(8).ToList();
            model.RecentLeadActivities = await _dbContext.LeadActivities.AsNoTracking().Include(x => x.Lead).Include(x => x.CreatedByUser).OrderByDescending(x => x.ActivityDate).Take(5).ToListAsync();
            model.RecentOpportunityActivities = await _dbContext.OpportunityActivities.AsNoTracking().Include(x => x.Opportunity).Include(x => x.CreatedByUser).OrderByDescending(x => x.ActivityDate).Take(5).ToListAsync();
            model.OwnerPerformance = await _dbContext.Users.AsNoTracking().Select(u => new CrmOwnerPerformanceViewModel
            {
                Owner = u.FirstName + " " + u.LastName,
                OpenLeads = _dbContext.Leads.Count(l => l.AssignedToUserId == u.Id && openStatuses.Contains(l.Status)),
                Qualified = _dbContext.Leads.Count(l => l.AssignedToUserId == u.Id && l.Status == LeadStatus.Qualified),
                Converted = _dbContext.Leads.Count(l => l.AssignedToUserId == u.Id && l.IsConverted),
                PipelineValue = _dbContext.Opportunities.Where(o => o.AssignedToUserId == u.Id).Sum(o => o.ExpectedValue)
            }).ToListAsync();
        }

        private async Task<List<CrmFollowUpItemViewModel>> BuildFollowUpsAsync()
        {
            var leads = await _dbContext.LeadActivities.AsNoTracking().Include(x => x.Lead).ThenInclude(x => x.AssignedToUser)
                .Where(x => x.FollowUpDueAt != null).Select(x => new CrmFollowUpItemViewModel { RecordType = "Lead", ActivityId = x.Id, ParentId = x.LeadId, Number = x.Lead.LeadNumber, Customer = x.Lead.CustomerName, Subject = x.Subject, Owner = x.Lead.AssignedToUser == null ? null : x.Lead.AssignedToUser.FirstName + " " + x.Lead.AssignedToUser.LastName, DueAt = x.FollowUpDueAt!.Value, Priority = x.Lead.Priority, IsCompleted = x.IsCompleted }).ToListAsync();
            var opps = await _dbContext.OpportunityActivities.AsNoTracking().Include(x => x.Opportunity).ThenInclude(x => x.AssignedToUser).Include(x => x.Opportunity).ThenInclude(x => x.Lead)
                .Where(x => x.FollowUpDueAt != null).Select(x => new CrmFollowUpItemViewModel { RecordType = "Opportunity", ActivityId = x.Id, ParentId = x.OpportunityId, Number = x.Opportunity.OpportunityNumber, Customer = x.Opportunity.Lead.CustomerName, Subject = x.Subject, Owner = x.Opportunity.AssignedToUser == null ? null : x.Opportunity.AssignedToUser.FirstName + " " + x.Opportunity.AssignedToUser.LastName, DueAt = x.FollowUpDueAt!.Value, Priority = LeadPriority.Medium, IsCompleted = x.IsCompleted }).ToListAsync();
            return leads.Concat(opps).OrderBy(x => x.IsCompleted).ThenBy(x => x.DueAt).ToList();
        }

        private IQueryable<Lead> ApplyLeadFilters(IQueryable<Lead> query, string? search, LeadStatus? status, LeadSource? source, LeadPriority? priority, LeadTemperature? temperature, LeadServiceInterest? serviceInterest, int? assignedToUserId, int? sportId)
        {
            if (!string.IsNullOrWhiteSpace(search))
            {
                var s = search.ToLower();
                query = query.Where(x => x.LeadNumber.ToLower().Contains(s) || x.CustomerName.ToLower().Contains(s) || (x.Email != null && x.Email.ToLower().Contains(s)) || (x.Phone != null && x.Phone.Contains(s)) || (x.WhatsAppNumber != null && x.WhatsAppNumber.Contains(s)) || (x.OrganizationName != null && x.OrganizationName.ToLower().Contains(s)) || (x.InquirySubject != null && x.InquirySubject.ToLower().Contains(s)) || (x.Tags != null && x.Tags.ToLower().Contains(s)));
            }
            if (status.HasValue) query = query.Where(x => x.Status == status);
            if (source.HasValue) query = query.Where(x => x.Source == source);
            if (priority.HasValue) query = query.Where(x => x.Priority == priority);
            if (temperature.HasValue) query = query.Where(x => x.Temperature == temperature);
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
            "temperature" => query.OrderByDescending(x => x.Temperature),
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
            if (model.PreferredDate.HasValue && model.PreferredDate.Value < DateOnly.FromDateTime(DateTime.UtcNow)) ModelState.AddModelError(nameof(model.PreferredDate), "Preferred date cannot be in the past.");
            if (model.AssignedToUserId.HasValue && !await _dbContext.Users.AnyAsync(x => x.Id == model.AssignedToUserId && x.IsActive)) ModelState.AddModelError(nameof(model.AssignedToUserId), "Assigned user is invalid.");
        }

        private async Task ValidateOpportunityAsync(OpportunityFormViewModel model, int? id = null)
        {
            if (!await _dbContext.Leads.AnyAsync(x => x.Id == model.LeadId)) ModelState.AddModelError(nameof(model.LeadId), "Lead is required.");
            if (model.ExpectedCloseDate < DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1))) ModelState.AddModelError(nameof(model.ExpectedCloseDate), "Expected close date is invalid.");
            if (model.Stage == OpportunityStage.Lost && string.IsNullOrWhiteSpace(model.LossReason)) ModelState.AddModelError(nameof(model.LossReason), "Loss reason is required.");
            if (id == null && await _dbContext.Opportunities.AnyAsync(x => x.LeadId == model.LeadId)) ModelState.AddModelError(nameof(model.LeadId), "This lead already has an opportunity.");
        }

        private async Task<List<LeadListItemViewModel>> FindDuplicateLeadsAsync(LeadFormViewModel model)
        {
            var recent = DateTime.UtcNow.AddDays(-7);
            var q = _dbContext.Leads.AsNoTracking().Where(x => x.IsActive && x.CreatedAt >= recent && x.Status != LeadStatus.Converted && x.Status != LeadStatus.Disqualified);
            q = q.Where(x => (model.CustomerId != null && x.CustomerId == model.CustomerId) || (!string.IsNullOrWhiteSpace(model.Email) && x.Email == model.Email) || (!string.IsNullOrWhiteSpace(model.Phone) && x.Phone == model.Phone) || (!string.IsNullOrWhiteSpace(model.WhatsAppNumber) && x.WhatsAppNumber == model.WhatsAppNumber) || (x.CustomerName == model.CustomerName && x.ServiceInterest == model.ServiceInterest));
            return await q.Take(5).Select(x => new LeadListItemViewModel { Id = x.Id, LeadNumber = x.LeadNumber, CustomerName = x.CustomerName, Status = x.Status, ServiceInterest = x.ServiceInterest, CreatedAt = x.CreatedAt }).ToListAsync();
        }

        private void MapLead(LeadFormViewModel model, Lead lead)
        {
            lead.CustomerId = model.CustomerId; lead.CustomerName = model.CustomerName; lead.Email = model.Email; lead.Phone = model.Phone; lead.WhatsAppNumber = model.WhatsAppNumber; lead.OrganizationName = model.OrganizationName;
            lead.Source = model.Source; lead.ServiceInterest = model.ServiceInterest; lead.SportId = model.SportId; lead.FacilityId = model.FacilityId; lead.CourtId = model.CourtId; lead.MembershipPlanId = model.MembershipPlanId;
            lead.PreferredDate = model.PreferredDate; lead.PreferredStartTime = model.PreferredStartTime; lead.PreferredDurationMinutes = model.PreferredDurationMinutes; lead.ExpectedPlayerCount = model.ExpectedPlayerCount;
            lead.InquirySubject = model.InquirySubject; lead.InquiryDetails = model.InquiryDetails; lead.AssignedToUserId = model.AssignedToUserId; lead.Status = model.Status; lead.Priority = model.Priority; lead.Temperature = model.Temperature; lead.NextFollowUpAt = model.NextFollowUpAt;
            lead.QualificationNotes = model.QualificationNotes; lead.DisqualificationReason = model.DisqualificationReason; lead.InternalNotes = model.InternalNotes; lead.Tags = model.Tags; lead.IsActive = model.IsActive;
        }

        private LeadFormViewModel MapLeadForm(Lead lead) => new() { Id = lead.Id, LeadNumber = lead.LeadNumber, CustomerId = lead.CustomerId, CustomerName = lead.CustomerName, Email = lead.Email, Phone = lead.Phone, WhatsAppNumber = lead.WhatsAppNumber, OrganizationName = lead.OrganizationName, Source = lead.Source, ServiceInterest = lead.ServiceInterest, SportId = lead.SportId, FacilityId = lead.FacilityId, CourtId = lead.CourtId, MembershipPlanId = lead.MembershipPlanId, PreferredDate = lead.PreferredDate, PreferredStartTime = lead.PreferredStartTime, PreferredDurationMinutes = lead.PreferredDurationMinutes, ExpectedPlayerCount = lead.ExpectedPlayerCount, InquirySubject = lead.InquirySubject, InquiryDetails = lead.InquiryDetails, AssignedToUserId = lead.AssignedToUserId, Status = lead.Status, Priority = lead.Priority, Temperature = lead.Temperature, NextFollowUpAt = lead.NextFollowUpAt, QualificationNotes = lead.QualificationNotes, DisqualificationReason = lead.DisqualificationReason, InternalNotes = lead.InternalNotes, Tags = lead.Tags, IsActive = lead.IsActive };

        private void MapOpportunity(OpportunityFormViewModel model, Opportunity opportunity)
        {
            opportunity.LeadId = model.LeadId; opportunity.CustomerId = model.CustomerId; opportunity.Name = model.Name; opportunity.Description = model.Description; opportunity.Type = model.Type; opportunity.Stage = model.Stage; opportunity.ExpectedValue = model.ExpectedValue; opportunity.ProbabilityPercentage = model.ProbabilityPercentage; opportunity.ExpectedCloseDate = model.ExpectedCloseDate; opportunity.ActualCloseDate = model.ActualCloseDate; opportunity.SportId = model.SportId; opportunity.FacilityId = model.FacilityId; opportunity.CourtId = model.CourtId; opportunity.MembershipPlanId = model.MembershipPlanId; opportunity.AssignedToUserId = model.AssignedToUserId; opportunity.CustomerRequirement = model.CustomerRequirement; opportunity.ProposedSolution = model.ProposedSolution; opportunity.CompetitorInformation = model.CompetitorInformation; opportunity.LossReason = model.LossReason; opportunity.InternalNotes = model.InternalNotes; opportunity.Tags = model.Tags; opportunity.IsActive = model.IsActive;
        }

        private OpportunityFormViewModel MapOpportunityForm(Opportunity x) => new() { Id = x.Id, OpportunityNumber = x.OpportunityNumber, LeadId = x.LeadId, CustomerId = x.CustomerId, Name = x.Name, Description = x.Description, Type = x.Type, Stage = x.Stage, ExpectedValue = x.ExpectedValue, ProbabilityPercentage = x.ProbabilityPercentage, ExpectedCloseDate = x.ExpectedCloseDate, ActualCloseDate = x.ActualCloseDate, SportId = x.SportId, FacilityId = x.FacilityId, CourtId = x.CourtId, MembershipPlanId = x.MembershipPlanId, AssignedToUserId = x.AssignedToUserId, CustomerRequirement = x.CustomerRequirement, ProposedSolution = x.ProposedSolution, CompetitorInformation = x.CompetitorInformation, LossReason = x.LossReason, InternalNotes = x.InternalNotes, Tags = x.Tags, IsActive = x.IsActive };

        private async Task<LeadFormViewModel> AddLeadOptionsAsync(LeadFormViewModel model)
        {
            model.CustomerOptions = await _dbContext.Customers.AsNoTracking().OrderBy(x => x.FirstName).Select(x => new SelectListItem(x.FirstName + " " + x.LastName + " - " + x.PrimaryPhone, x.Id.ToString(), x.Id == model.CustomerId)).ToListAsync();
            model.UserOptions = await UserOptionsAsync(model.AssignedToUserId);
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

        private async Task<List<SelectListItem>> UserOptionsAsync(int? selected = null) => await _dbContext.Users.AsNoTracking().Where(x => x.IsActive).OrderBy(x => x.FirstName).Select(x => new SelectListItem(x.FirstName + " " + x.LastName, x.Id.ToString(), x.Id == selected)).ToListAsync();
        private async Task<List<SelectListItem>> SportOptionsAsync(int? selected = null) => await _dbContext.Sports.AsNoTracking().Where(x => x.IsActive).OrderBy(x => x.DisplayOrder).Select(x => new SelectListItem(x.Name, x.Id.ToString(), x.Id == selected)).ToListAsync();

        private async Task AddLeadActivityAsync(int leadId, LeadActivityType type, string subject, string? description, DateTime? followUpDueAt, bool isCompleted, DateTime? activityDate = null, bool save = true)
        {
            _dbContext.LeadActivities.Add(new LeadActivity { LeadId = leadId, ActivityType = type, Subject = subject, Description = description, ActivityDate = activityDate ?? DateTime.UtcNow, FollowUpDueAt = followUpDueAt, IsCompleted = isCompleted, CreatedByUserId = CurrentUserId(), CreatedAt = DateTime.UtcNow });
            if (save) await _dbContext.SaveChangesAsync();
        }

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
        private bool CanCreateLeads() => RolePermissions.TryGetCurrentRole(User, out var role) && role is UserRole.SuperAdmin or UserRole.Admin or UserRole.BookingManager or UserRole.Receptionist;
        private bool CanManageLeads() => RolePermissions.TryGetCurrentRole(User, out var role) && role is UserRole.SuperAdmin or UserRole.Admin or UserRole.BookingManager;
        private bool CanManageOpportunities() => RolePermissions.TryGetCurrentRole(User, out var role) && role is UserRole.SuperAdmin or UserRole.Admin or UserRole.BookingManager;
        private bool IsReadOnlyRole() => RolePermissions.TryGetCurrentRole(User, out var role) && role is UserRole.Viewer or UserRole.FinanceManager;
        private static string NormalizeTab(string? tab) => tab?.ToLowerInvariant() is "leads" or "opportunities" ? tab!.ToLowerInvariant() : "overview";
        private static bool HasContact(Lead lead) => !string.IsNullOrWhiteSpace(lead.Email) || !string.IsNullOrWhiteSpace(lead.Phone) || !string.IsNullOrWhiteSpace(lead.WhatsAppNumber);
        private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        private static string? NormalizeEmail(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim().ToLowerInvariant();
        private static string? NormalizePhone(string? value) { if (string.IsNullOrWhiteSpace(value)) return null; var prefix = value.Trim().StartsWith("+") ? "+" : ""; return prefix + new string(value.Where(char.IsDigit).ToArray()); }
        private static void NormalizeLead(LeadFormViewModel model) { model.CustomerName = model.CustomerName.Trim(); model.Email = NormalizeEmail(model.Email); model.Phone = NormalizePhone(model.Phone); model.WhatsAppNumber = NormalizePhone(model.WhatsAppNumber); model.OrganizationName = Clean(model.OrganizationName); model.InquirySubject = Clean(model.InquirySubject); model.InquiryDetails = Clean(model.InquiryDetails); model.QualificationNotes = Clean(model.QualificationNotes); model.DisqualificationReason = Clean(model.DisqualificationReason); model.InternalNotes = Clean(model.InternalNotes); model.Tags = Clean(model.Tags); }
        private static void NormalizeOpportunity(OpportunityFormViewModel model) { model.Name = model.Name.Trim(); model.Description = Clean(model.Description); model.CustomerRequirement = Clean(model.CustomerRequirement); model.ProposedSolution = Clean(model.ProposedSolution); model.CompetitorInformation = Clean(model.CompetitorInformation); model.LossReason = Clean(model.LossReason); model.InternalNotes = Clean(model.InternalNotes); model.Tags = Clean(model.Tags); if (model.ProbabilityPercentage == 0 && model.Stage != OpportunityStage.Lost) model.ProbabilityPercentage = StageProbability(model.Stage); }
        private static OpportunityType MapOpportunityType(LeadServiceInterest interest) => interest switch { LeadServiceInterest.Membership => OpportunityType.Membership, LeadServiceInterest.CorporateBooking => OpportunityType.CorporatePackage, LeadServiceInterest.Tournament => OpportunityType.Tournament, LeadServiceInterest.Event => OpportunityType.Event, LeadServiceInterest.Coaching => OpportunityType.Coaching, _ => OpportunityType.CourtBooking };
        private static int StageProbability(OpportunityStage stage) => stage switch { OpportunityStage.Qualification => 10, OpportunityStage.NeedsAnalysis => 25, OpportunityStage.ProposalPreparation => 40, OpportunityStage.ProposalSent => 60, OpportunityStage.Negotiation => 80, OpportunityStage.Won => 100, OpportunityStage.Lost => 0, OpportunityStage.OnHold => 20, _ => 10 };
        private static bool CanTransition(OpportunityStage from, OpportunityStage to) => from == to || from == OpportunityStage.OnHold || to == OpportunityStage.OnHold || from is not (OpportunityStage.Won or OpportunityStage.Lost);
        private static void ApplyStageRules(Opportunity opportunity) { if (opportunity.Stage == OpportunityStage.Won) { opportunity.ProbabilityPercentage = 100; opportunity.WonAt ??= DateTime.UtcNow; opportunity.ActualCloseDate ??= DateOnly.FromDateTime(DateTime.UtcNow); } if (opportunity.Stage == OpportunityStage.Lost) { opportunity.ProbabilityPercentage = 0; opportunity.LostAt ??= DateTime.UtcNow; opportunity.ActualCloseDate ??= DateOnly.FromDateTime(DateTime.UtcNow); } }
        private async Task<Customer?> FindCustomerForLeadAsync(Lead lead) => await _dbContext.Customers.FirstOrDefaultAsync(x => (!string.IsNullOrWhiteSpace(lead.Email) && x.Email == lead.Email) || (!string.IsNullOrWhiteSpace(lead.Phone) && x.PrimaryPhone == lead.Phone) || (!string.IsNullOrWhiteSpace(lead.WhatsAppNumber) && x.WhatsAppNumber == lead.WhatsAppNumber));
        private static string FirstName(string name) => (name.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? "Customer").Trim();
        private static string LastName(string name) => string.Join(" ", name.Split(' ', StringSplitOptions.RemoveEmptyEntries).Skip(1)).Trim() is { Length: > 0 } value ? value : "Lead";
    }
}
