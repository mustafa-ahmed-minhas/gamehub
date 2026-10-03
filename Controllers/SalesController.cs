using System.Security.Claims;
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
    [GameHubAuthorize(UserRole.SuperAdmin, UserRole.Admin, UserRole.BookingManager, UserRole.Receptionist, UserRole.FinanceManager, UserRole.Viewer)]
    [ValidateActiveUser]
    public class SalesController : Controller
    {
        private static readonly int[] PageSizes = { 10, 25, 50 };
        private readonly ApplicationDbContext _dbContext;

        public SalesController(ApplicationDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        [HttpGet]
        public async Task<IActionResult> Index(string tab = "overview", string? search = null, SalesQuotationStatus? status = null, int? customerId = null, int? opportunityId = null, int? assignedToUserId = null, DateOnly? dateFrom = null, DateOnly? dateTo = null, string? validityState = null, string sortBy = "newest", string sortDirection = "desc", int page = 1, int pageSize = 10)
        {
            if (!RolePermissions.CanAccessModule(User, "Sales")) return RedirectToAction("NotFound", "Error");
            await SynchronizeExpiredQuotationsAsync();

            page = Math.Max(1, page);
            pageSize = PageSizes.Contains(pageSize) ? pageSize : 10;
            tab = NormalizeTab(tab);
            var settings = await GetSettingsAsync();
            var query = _dbContext.SalesQuotations.AsNoTracking()
                .Include(x => x.Customer)
                .Include(x => x.Opportunity)
                .Include(x => x.AssignedToUser)
                .AsQueryable();

            query = ApplyFilters(query, search, status, customerId, opportunityId, assignedToUserId, dateFrom, dateTo, validityState);
            query = ApplySort(query, sortBy, sortDirection);
            var total = await query.CountAsync();
            var totalPages = Math.Max(1, (int)Math.Ceiling(total / (double)pageSize));
            page = Math.Min(page, totalPages);

            var list = await query.Skip((page - 1) * pageSize).Take(pageSize).Select(x => new SalesQuotationListItemViewModel
            {
                Id = x.Id,
                QuotationNumber = x.QuotationNumber,
                Subject = x.Subject,
                QuotationDate = x.QuotationDate,
                ValidUntil = x.ValidUntil,
                Status = x.Status,
                GrandTotal = x.GrandTotal,
                CurrencySymbol = x.CurrencySymbol,
                CustomerName = x.Customer.FirstName + " " + x.Customer.LastName,
                CustomerCode = x.Customer.CustomerCode,
                OrganizationName = x.Customer.OrganizationName,
                OpportunityNumber = x.Opportunity.OpportunityNumber,
                OpportunityName = x.Opportunity.Name,
                OpportunityStage = x.Opportunity.Stage,
                Owner = x.AssignedToUser == null ? null : x.AssignedToUser.FirstName + " " + x.AssignedToUser.LastName,
                SalesOrderCreated = x.SalesOrderCreated
            }).ToListAsync();

            var all = _dbContext.SalesQuotations.AsNoTracking();
            var model = new SalesDashboardViewModel
            {
                ActiveTab = tab,
                CurrencySymbol = settings.CurrencySymbol ?? "Rs",
                CanCreate = CanCreateQuotation(),
                CanManageLifecycle = CanManageLifecycle(),
                IsReadOnly = IsReadOnly(),
                DraftCount = await all.CountAsync(x => x.Status == SalesQuotationStatus.Draft),
                AwaitingReviewCount = await all.CountAsync(x => x.Status == SalesQuotationStatus.ReadyForReview),
                SentCount = await all.CountAsync(x => x.Status == SalesQuotationStatus.Sent || x.Status == SalesQuotationStatus.Viewed),
                AcceptedCount = await all.CountAsync(x => x.Status == SalesQuotationStatus.Accepted),
                RejectedCount = await all.CountAsync(x => x.Status == SalesQuotationStatus.Rejected),
                ExpiringSoonCount = await all.CountAsync(x => (x.Status == SalesQuotationStatus.Sent || x.Status == SalesQuotationStatus.Viewed) && x.ValidUntil >= DateOnly.FromDateTime(DateTime.UtcNow) && x.ValidUntil <= DateOnly.FromDateTime(DateTime.UtcNow.AddDays(2))),
                TotalQuotedValue = await all.SumAsync(x => x.GrandTotal),
                AcceptedValue = await all.Where(x => x.Status == SalesQuotationStatus.Accepted).SumAsync(x => x.GrandTotal),
                Quotations = new SalesQuotationIndexViewModel
                {
                    Items = list,
                    Search = search,
                    Status = status,
                    ValidityState = validityState,
                    SortBy = sortBy,
                    SortDirection = sortDirection,
                    CurrentPage = page,
                    PageSize = pageSize,
                    TotalRecords = total,
                    TotalPages = totalPages,
                    CustomerOptions = await CustomerOptionsAsync(customerId),
                    OpportunityOptions = await OpportunityOptionsAsync(opportunityId),
                    UserOptions = await UserOptionsAsync(assignedToUserId)
                }
            };

            return View(model);
        }

        [HttpGet]
        public async Task<IActionResult> CreateFromOpportunity(int opportunityId)
        {
            if (!CanCreateQuotation()) return RedirectToAction("NotFound", "Error");
            var result = await ValidateOpportunityForQuotationAsync(opportunityId);
            if (!result.Success)
            {
                TempData.SetToast("warning", "Quotation Blocked", result.Message);
                return RedirectToAction(nameof(Index), new { tab = "quotations" });
            }

            var opportunity = result.Opportunity!;
            var settings = await GetSettingsAsync();
            var model = new SalesQuotationFormViewModel
            {
                OpportunityId = opportunity.Id,
                CustomerId = opportunity.CustomerId!.Value,
                AssignedToUserId = opportunity.AssignedToUserId,
                QuotationDate = DateOnly.FromDateTime(DateTime.UtcNow),
                ValidUntil = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(Math.Max(settings.DefaultQuotationValidityDays, 1))),
                Subject = $"{opportunity.Name} Proposal",
                Introduction = opportunity.CustomerRequirement,
                TermsAndConditions = "This quotation is valid until the date mentioned above. Prices are subject to availability and final confirmation.",
                Currency = settings.Currency,
                CurrencySymbol = settings.CurrencySymbol ?? "Rs",
                OpportunityLabel = $"{opportunity.OpportunityNumber} - {opportunity.Name}",
                CustomerLabel = $"{opportunity.Customer!.CustomerCode} - {opportunity.Customer.FullName}",
                ManualDiscountAmount = 0,
                Items = new List<SalesQuotationItemViewModel>
                {
                    new()
                    {
                        LineNumber = 1,
                        DisplayOrder = 1,
                        ItemType = MapItemType(opportunity.Type),
                        SportId = opportunity.SportId,
                        FacilityId = opportunity.FacilityId,
                        CourtId = opportunity.CourtId,
                        MembershipPlanId = opportunity.MembershipPlanId,
                        Description = opportunity.ProposedSolution ?? opportunity.CustomerRequirement ?? opportunity.Name,
                        Quantity = 1,
                        UnitOfMeasure = "Package",
                        UnitPrice = opportunity.ExpectedValue,
                        TaxPercentage = settings.TaxPercentage
                    }
                }
            };
            Recalculate(model);
            return View("QuotationForm", await AddOptionsAsync(model));
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateQuotation(SalesQuotationFormViewModel model, string? submitAction)
        {
            if (!CanCreateQuotation()) return RedirectToAction("NotFound", "Error");
            Normalize(model);
            Recalculate(model);
            await ValidateFormAsync(model);
            if (!ModelState.IsValid) return View("QuotationForm", await AddOptionsAsync(model));

            await using var transaction = await _dbContext.Database.BeginTransactionAsync();
            var settings = await GetSettingsAsync();
            var quotation = new SalesQuotation
            {
                QuotationNumber = await GenerateQuotationNumberAsync(settings.QuotationPrefix),
                OpportunityId = model.OpportunityId,
                CustomerId = model.CustomerId,
                AssignedToUserId = model.AssignedToUserId,
                QuotationDate = model.QuotationDate,
                ValidUntil = model.ValidUntil,
                Status = submitAction == "review" ? SalesQuotationStatus.ReadyForReview : SalesQuotationStatus.Draft,
                Subject = model.Subject,
                Introduction = model.Introduction,
                TermsAndConditions = model.TermsAndConditions,
                CustomerNotes = model.CustomerNotes,
                InternalNotes = model.InternalNotes,
                CustomerReference = model.CustomerReference,
                ManualDiscountAmount = model.ManualDiscountAmount,
                Currency = settings.Currency,
                CurrencySymbol = settings.CurrencySymbol ?? "Rs",
                CreatedByUserId = CurrentUserId(),
                CreatedAt = DateTime.UtcNow,
                IsActive = true
            };
            ApplyTotals(model, quotation);
            MapItems(model, quotation);
            _dbContext.SalesQuotations.Add(quotation);
            await _dbContext.SaveChangesAsync();

            var opportunity = await _dbContext.Opportunities.FirstAsync(x => x.Id == quotation.OpportunityId);
            opportunity.QuotationCreated = true;
            opportunity.QuotationId = quotation.Id;
            opportunity.UpdatedAt = DateTime.UtcNow;
            _dbContext.OpportunityActivities.Add(new OpportunityActivity
            {
                OpportunityId = opportunity.Id,
                ActivityType = OpportunityActivityType.Proposal,
                Subject = "Quotation created",
                Description = $"Quotation {quotation.QuotationNumber} was created.",
                ActivityDate = DateTime.UtcNow,
                CreatedAt = DateTime.UtcNow,
                CreatedByUserId = CurrentUserId(),
                IsCompleted = true
            });
            await _dbContext.SaveChangesAsync();
            await transaction.CommitAsync();

            TempData.SetToast("success", "Quotation Created", "Quotation was created successfully.");
            return RedirectToAction(nameof(QuotationDetails), new { id = quotation.Id });
        }

        [HttpGet]
        public async Task<IActionResult> EditQuotation(int id)
        {
            if (!CanCreateQuotation()) return RedirectToAction("NotFound", "Error");
            var quotation = await LoadQuotationQuery(tracked: false).FirstOrDefaultAsync(x => x.Id == id);
            if (quotation == null) return RedirectToAction("NotFound", "Error");
            if (!quotation.IsEditable)
            {
                TempData.SetToast("warning", "Editing Locked", "Only Draft and Ready for Review quotations can be edited.");
                return RedirectToAction(nameof(QuotationDetails), new { id });
            }
            return View("QuotationForm", await AddOptionsAsync(MapForm(quotation)));
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> EditQuotation(int id, SalesQuotationFormViewModel model)
        {
            if (!CanCreateQuotation()) return RedirectToAction("NotFound", "Error");
            if (model.Id != id) return RedirectToAction("NotFound", "Error");
            var quotation = await LoadQuotationQuery(tracked: true).FirstOrDefaultAsync(x => x.Id == id);
            if (quotation == null) return RedirectToAction("NotFound", "Error");
            if (!quotation.IsEditable) return RedirectToAction("NotFound", "Error");

            Normalize(model);
            Recalculate(model);
            await ValidateFormAsync(model, id);
            if (!ModelState.IsValid) return View("QuotationForm", await AddOptionsAsync(model));

            quotation.AssignedToUserId = model.AssignedToUserId;
            quotation.QuotationDate = model.QuotationDate;
            quotation.ValidUntil = model.ValidUntil;
            quotation.Subject = model.Subject;
            quotation.Introduction = model.Introduction;
            quotation.TermsAndConditions = model.TermsAndConditions;
            quotation.CustomerNotes = model.CustomerNotes;
            quotation.InternalNotes = model.InternalNotes;
            quotation.CustomerReference = model.CustomerReference;
            quotation.ManualDiscountAmount = model.ManualDiscountAmount;
            quotation.UpdatedAt = DateTime.UtcNow;
            quotation.UpdatedByUserId = CurrentUserId();
            ApplyTotals(model, quotation);
            _dbContext.SalesQuotationItems.RemoveRange(quotation.Items);
            quotation.Items.Clear();
            MapItems(model, quotation);
            await _dbContext.SaveChangesAsync();
            TempData.SetToast("success", "Quotation Updated", "Quotation changes were saved.");
            return RedirectToAction(nameof(QuotationDetails), new { id });
        }

        [HttpGet]
        public async Task<IActionResult> QuotationDetails(int id)
        {
            await SynchronizeExpiredQuotationsAsync(id);
            var quotation = await LoadQuotationQuery(tracked: false).FirstOrDefaultAsync(x => x.Id == id);
            if (quotation == null) return RedirectToAction("NotFound", "Error");
            return View(new SalesQuotationDetailsViewModel
            {
                Quotation = quotation,
                CanEdit = CanCreateQuotation() && quotation.IsEditable,
                CanManageLifecycle = CanManageLifecycle(),
                IsReadOnly = IsReadOnly(),
                ApprovalHistory = await _dbContext.DocumentApprovals.AsNoTracking().Include(x => x.ActionByUser).Where(x => x.DocumentType == DocumentType.SalesQuotation && x.DocumentId == id).OrderBy(x => x.ActionAt).ToListAsync(),
                RevisionHistory = await _dbContext.DocumentRevisions.AsNoTracking().Include(x => x.CreatedByUser).Where(x => x.DocumentType == DocumentType.SalesQuotation && x.DocumentId == id).OrderBy(x => x.RevisionNumber).ToListAsync()
            });
        }

        [HttpGet]
        public async Task<IActionResult> QuotationPreview(int id)
        {
            await SynchronizeExpiredQuotationsAsync(id);
            var quotation = await LoadQuotationQuery(tracked: false).FirstOrDefaultAsync(x => x.Id == id);
            if (quotation == null) return RedirectToAction("NotFound", "Error");
            return View(new SalesQuotationPreviewViewModel { Quotation = quotation, Settings = await GetSettingsAsync() });
        }

        [HttpPost, ValidateAntiForgeryToken] public async Task<IActionResult> SubmitForReview(int id, string? comments) => await SubmitQuotationAsync(id, comments, false);
        [HttpPost, ValidateAntiForgeryToken] public async Task<IActionResult> SubmitForApproval(int id, string? comments) => await SubmitQuotationAsync(id, comments, false);
        [HttpPost, ValidateAntiForgeryToken] public async Task<IActionResult> ResubmitQuotation(int id, string revisionReason) => await SubmitQuotationAsync(id, revisionReason, true);
        [HttpPost, ValidateAntiForgeryToken] public async Task<IActionResult> ApproveQuotation(int id, string? comments) => await ApproveQuotationAsync(id, comments);
        [HttpPost, ValidateAntiForgeryToken] public async Task<IActionResult> ReturnToDraft(int id) => await ChangeStatusAsync(id, SalesQuotationStatus.ReadyForReview, SalesQuotationStatus.Draft, "Quotation returned to draft.", null);
        [HttpPost, ValidateAntiForgeryToken] public async Task<IActionResult> MarkSent(int id) => await ChangeStatusAsync(id, SalesQuotationStatus.ReadyForReview, SalesQuotationStatus.Sent, "Quotation marked as sent.", q => { q.SentAt = DateTime.UtcNow; });
        [HttpPost, ValidateAntiForgeryToken] public async Task<IActionResult> MarkViewed(int id) => await ChangeStatusAsync(id, SalesQuotationStatus.Sent, SalesQuotationStatus.Viewed, "Quotation marked as viewed.", q => { q.ViewedAt = DateTime.UtcNow; });
        [HttpPost, ValidateAntiForgeryToken] public async Task<IActionResult> AcceptQuotation(int id) => await AcceptInternalAsync(id);
        [HttpPost, ValidateAntiForgeryToken] public async Task<IActionResult> RejectQuotation(int id, string reason) => await RejectAsync(id, reason, customerId: null);
        [HttpPost, ValidateAntiForgeryToken] public async Task<IActionResult> CancelQuotation(int id, string reason) => await CancelAsync(id, reason);

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> DuplicateQuotation(int id)
        {
            if (!CanCreateQuotation()) return RedirectToAction("NotFound", "Error");
            var source = await LoadQuotationQuery(tracked: false).FirstOrDefaultAsync(x => x.Id == id);
            if (source == null) return RedirectToAction("NotFound", "Error");
            var settings = await GetSettingsAsync();
            var copy = new SalesQuotation
            {
                QuotationNumber = await GenerateQuotationNumberAsync(settings.QuotationPrefix),
                OpportunityId = source.OpportunityId,
                CustomerId = source.CustomerId,
                AssignedToUserId = source.AssignedToUserId,
                QuotationDate = DateOnly.FromDateTime(DateTime.UtcNow),
                ValidUntil = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(Math.Max(settings.DefaultQuotationValidityDays, 1))),
                Status = SalesQuotationStatus.Draft,
                Subject = source.Subject,
                Introduction = source.Introduction,
                TermsAndConditions = source.TermsAndConditions,
                CustomerNotes = source.CustomerNotes,
                InternalNotes = source.InternalNotes,
                CustomerReference = source.CustomerReference,
                ManualDiscountAmount = source.ManualDiscountAmount,
                Currency = source.Currency,
                CurrencySymbol = source.CurrencySymbol,
                CreatedAt = DateTime.UtcNow,
                CreatedByUserId = CurrentUserId(),
                IsActive = true
            };
            foreach (var item in source.Items.OrderBy(x => x.DisplayOrder))
            {
                copy.Items.Add(new SalesQuotationItem
                {
                    LineNumber = item.LineNumber,
                    ItemType = item.ItemType,
                    SportId = item.SportId,
                    FacilityId = item.FacilityId,
                    CourtId = item.CourtId,
                    MembershipPlanId = item.MembershipPlanId,
                    Description = item.Description,
                    Quantity = item.Quantity,
                    UnitOfMeasure = item.UnitOfMeasure,
                    UnitPrice = item.UnitPrice,
                    DiscountPercentage = item.DiscountPercentage,
                    TaxPercentage = item.TaxPercentage,
                    DisplayOrder = item.DisplayOrder,
                    CreatedAt = DateTime.UtcNow
                });
            }
            Recalculate(copy);
            _dbContext.SalesQuotations.Add(copy);
            await _dbContext.SaveChangesAsync();
            TempData.SetToast("success", "Quotation Duplicated", "A new draft quotation was created.");
            return RedirectToAction(nameof(QuotationDetails), new { id = copy.Id });
        }

        private async Task<IActionResult> ChangeStatusAsync(int id, SalesQuotationStatus from, SalesQuotationStatus to, string message, Action<SalesQuotation>? mutate)
        {
            if (!CanManageLifecycle()) return RedirectToAction("NotFound", "Error");
            var quotation = await LoadQuotationQuery(tracked: true).FirstOrDefaultAsync(x => x.Id == id);
            if (quotation == null || quotation.Status != from) return RedirectToAction("NotFound", "Error");
            quotation.Status = to;
            quotation.UpdatedAt = DateTime.UtcNow;
            quotation.UpdatedByUserId = CurrentUserId();
            mutate?.Invoke(quotation);
            if (to == SalesQuotationStatus.Sent && CanTransition(quotation.Opportunity.Stage, OpportunityStage.ProposalSent))
            {
                quotation.Opportunity.Stage = OpportunityStage.ProposalSent;
                quotation.Opportunity.ProbabilityPercentage = Math.Max(quotation.Opportunity.ProbabilityPercentage, 60);
            }
            await AddOpportunityActivityAsync(quotation.OpportunityId, "Quotation status changed", $"{quotation.QuotationNumber}: {from.GetDisplayName()} to {to.GetDisplayName()}");
            await _dbContext.SaveChangesAsync();
            TempData.SetToast("success", "Quotation Updated", message);
            return RedirectToAction(nameof(QuotationDetails), new { id });
        }

        private async Task<IActionResult> AcceptInternalAsync(int id)
        {
            if (!CanManageLifecycle()) return RedirectToAction("NotFound", "Error");
            var quotation = await LoadQuotationQuery(tracked: true).FirstOrDefaultAsync(x => x.Id == id);
            if (quotation == null || quotation.Status is not (SalesQuotationStatus.Sent or SalesQuotationStatus.Viewed)) return RedirectToAction("NotFound", "Error");
            quotation.Status = SalesQuotationStatus.Accepted;
            quotation.AcceptedAt = DateTime.UtcNow;
            quotation.UpdatedAt = DateTime.UtcNow;
            quotation.UpdatedByUserId = CurrentUserId();
            await AddOpportunityActivityAsync(quotation.OpportunityId, "Quotation accepted", $"{quotation.QuotationNumber} accepted. Ready for Sales Order phase.");
            await RecordApprovalAsync(quotation, ApprovalAction.CustomerAccepted, SalesQuotationStatus.Approved.ToString(), null, null, true);
            await RecordActivityAsync(quotation.Id, ApprovalAction.CustomerAccepted, "Quotation accepted by customer.");
            await _dbContext.SaveChangesAsync();
            TempData.SetToast("success", "Quotation Accepted", "Quotation is ready for Sales Order in the next phase.");
            return RedirectToAction(nameof(QuotationDetails), new { id });
        }

        private async Task<IActionResult> RejectAsync(int id, string reason, int? customerId)
        {
            var quotation = await LoadQuotationQuery(tracked: true).FirstOrDefaultAsync(x => x.Id == id);
            if (quotation == null || (customerId.HasValue && quotation.CustomerId != customerId.Value)) return RedirectToAction("NotFound", "Error");
            if (customerId == null && !CanManageLifecycle()) return RedirectToAction("NotFound", "Error");
            if (quotation.Status is not (SalesQuotationStatus.Sent or SalesQuotationStatus.Viewed)) return RedirectToAction("NotFound", "Error");
            if (string.IsNullOrWhiteSpace(reason))
            {
                TempData.SetToast("warning", "Reason Required", "Please provide a rejection reason.");
                return RedirectToAction(customerId.HasValue ? "QuotationDetails" : nameof(QuotationDetails), customerId.HasValue ? "MyAccount" : "Sales", new { id });
            }
            quotation.Status = SalesQuotationStatus.Rejected;
            quotation.ApprovalStatus = ApprovalStatus.Rejected;
            quotation.RejectedAt = DateTime.UtcNow;
            quotation.RejectedByUserId = CurrentUserId();
            quotation.RejectionReason = reason.Trim();
            quotation.IsLocked = false;
            quotation.UpdatedAt = DateTime.UtcNow;
            await AddOpportunityActivityAsync(quotation.OpportunityId, "Quotation rejected", $"{quotation.QuotationNumber} rejected. Reason: {quotation.RejectionReason}");
            await RecordApprovalAsync(quotation, ApprovalAction.Rejected, SalesQuotationStatus.SubmittedForApproval.ToString(), null, quotation.RejectionReason, true);
            await RecordActivityAsync(quotation.Id, ApprovalAction.Rejected, $"Quotation rejected: {quotation.RejectionReason}");
            await _dbContext.SaveChangesAsync();
            TempData.SetToast("info", "Quotation Rejected", "Quotation was marked as rejected.");
            return customerId.HasValue ? RedirectToAction("QuotationDetails", "MyAccount", new { id }) : RedirectToAction(nameof(QuotationDetails), new { id });
        }

        private async Task<IActionResult> CancelAsync(int id, string reason)
        {
            if (!CanManageLifecycle()) return RedirectToAction("NotFound", "Error");
            var quotation = await LoadQuotationQuery(tracked: true).FirstOrDefaultAsync(x => x.Id == id);
            if (quotation == null || quotation.Status is SalesQuotationStatus.Accepted or SalesQuotationStatus.Rejected or SalesQuotationStatus.Cancelled) return RedirectToAction("NotFound", "Error");
            if (string.IsNullOrWhiteSpace(reason))
            {
                TempData.SetToast("warning", "Reason Required", "Please provide a cancellation reason.");
                return RedirectToAction(nameof(QuotationDetails), new { id });
            }
            quotation.Status = SalesQuotationStatus.Cancelled;
            quotation.CancelledAt = DateTime.UtcNow;
            quotation.CancellationReason = reason.Trim();
            quotation.UpdatedAt = DateTime.UtcNow;
            quotation.UpdatedByUserId = CurrentUserId();
            await AddOpportunityActivityAsync(quotation.OpportunityId, "Quotation cancelled", $"{quotation.QuotationNumber} cancelled. Reason: {quotation.CancellationReason}");
            await _dbContext.SaveChangesAsync();
            TempData.SetToast("info", "Quotation Cancelled", "Quotation was cancelled.");
            return RedirectToAction(nameof(QuotationDetails), new { id });
        }

        private async Task<IActionResult> SubmitQuotationAsync(int id, string? notes, bool revision)
        {
            if (!CanCreateQuotation()) return RedirectToAction("NotFound", "Error");
            var quotation = await LoadQuotationQuery(tracked: true).FirstOrDefaultAsync(x => x.Id == id && x.IsActive);
            if (quotation == null || !quotation.IsEditable) return RedirectToAction("NotFound", "Error");
            if (revision && string.IsNullOrWhiteSpace(notes))
            {
                TempData.SetToast("warning", "Revision Notes Required", "Describe the changes made before resubmitting.");
                return RedirectToAction(nameof(QuotationDetails), new { id });
            }
            await using var transaction = await _dbContext.Database.BeginTransactionAsync();
            if (revision)
            {
                await SaveRevisionAsync(DocumentType.SalesQuotation, quotation.Id, quotation.QuotationNumber, quotation.RevisionNumber, quotation, notes!);
                quotation.RevisionNumber++;
                quotation.LastRevisionReason = notes!.Trim();
            }
            var previous = quotation.Status.ToString();
            quotation.Status = SalesQuotationStatus.SubmittedForApproval;
            quotation.ApprovalStatus = ApprovalStatus.PendingApproval;
            quotation.SubmittedAt = DateTime.UtcNow;
            quotation.IsLocked = true;
            quotation.UpdatedAt = DateTime.UtcNow;
            quotation.UpdatedByUserId = CurrentUserId();
            await RecordApprovalAsync(quotation, revision ? ApprovalAction.Resubmitted : ApprovalAction.Submitted, previous, Clean(notes), null, false);
            await RecordActivityAsync(quotation.Id, revision ? ApprovalAction.Resubmitted : ApprovalAction.Submitted, "Quotation submitted for approval.");
            await _dbContext.SaveChangesAsync();
            await transaction.CommitAsync();
            TempData.SetToast("success", "Submitted for Approval", "Quotation is locked until an approver acts.");
            return RedirectToAction(nameof(QuotationDetails), new { id });
        }

        private async Task<IActionResult> ApproveQuotationAsync(int id, string? comments)
        {
            if (!CanApprove()) return RedirectToAction("NotFound", "Error");
            var quotation = await LoadQuotationQuery(tracked: true).FirstOrDefaultAsync(x => x.Id == id && x.IsActive);
            if (quotation == null || quotation.Status != SalesQuotationStatus.SubmittedForApproval) return RedirectToAction("NotFound", "Error");
            if (quotation.CreatedByUserId == CurrentUserId() && !IsSuperAdmin())
            {
                TempData.SetToast("warning", "Approval Blocked", "You cannot approve your own quotation.");
                return RedirectToAction(nameof(QuotationDetails), new { id });
            }
            quotation.Status = SalesQuotationStatus.Approved;
            quotation.ApprovalStatus = ApprovalStatus.Approved;
            quotation.ApprovedAt = DateTime.UtcNow;
            quotation.ApprovedByUserId = CurrentUserId();
            quotation.IsLocked = true;
            quotation.UpdatedAt = DateTime.UtcNow;
            await RecordApprovalAsync(quotation, ApprovalAction.Approved, SalesQuotationStatus.SubmittedForApproval.ToString(), Clean(comments), null, true);
            await RecordActivityAsync(quotation.Id, ApprovalAction.Approved, "Quotation approved.");
            await _dbContext.SaveChangesAsync();
            TempData.SetToast("success", "Quotation Approved", "Await customer acceptance before conversion to a sales order.");
            return RedirectToAction(nameof(QuotationDetails), new { id });
        }

        private IQueryable<SalesQuotation> LoadQuotationQuery(bool tracked)
        {
            var query = _dbContext.SalesQuotations
                .Include(x => x.Customer)
                .Include(x => x.Opportunity).ThenInclude(x => x.Lead)
                .Include(x => x.Opportunity).ThenInclude(x => x.Sport)
                .Include(x => x.Opportunity).ThenInclude(x => x.Facility)
                .Include(x => x.Opportunity).ThenInclude(x => x.Court)
                .Include(x => x.AssignedToUser)
                .Include(x => x.Items).ThenInclude(x => x.Sport)
                .Include(x => x.Items).ThenInclude(x => x.Facility)
                .Include(x => x.Items).ThenInclude(x => x.Court)
                .Include(x => x.Items).ThenInclude(x => x.MembershipPlan)
                .AsQueryable();
            return tracked ? query : query.AsNoTracking();
        }

        private IQueryable<SalesQuotation> ApplyFilters(IQueryable<SalesQuotation> query, string? search, SalesQuotationStatus? status, int? customerId, int? opportunityId, int? assignedToUserId, DateOnly? dateFrom, DateOnly? dateTo, string? validityState)
        {
            if (!string.IsNullOrWhiteSpace(search))
            {
                var term = search.Trim().ToLower();
                query = query.Where(x => x.QuotationNumber.ToLower().Contains(term) || (x.Subject ?? "").ToLower().Contains(term) || (x.CustomerReference ?? "").ToLower().Contains(term) || x.Customer.CustomerCode.ToLower().Contains(term) || (x.Customer.FirstName + " " + x.Customer.LastName).ToLower().Contains(term) || x.Customer.PrimaryPhone.Contains(term) || x.Opportunity.OpportunityNumber.ToLower().Contains(term) || x.Opportunity.Name.ToLower().Contains(term));
            }
            if (status.HasValue) query = query.Where(x => x.Status == status.Value);
            if (customerId.HasValue) query = query.Where(x => x.CustomerId == customerId.Value);
            if (opportunityId.HasValue) query = query.Where(x => x.OpportunityId == opportunityId.Value);
            if (assignedToUserId.HasValue) query = query.Where(x => x.AssignedToUserId == assignedToUserId.Value);
            if (dateFrom.HasValue) query = query.Where(x => x.QuotationDate >= dateFrom.Value);
            if (dateTo.HasValue) query = query.Where(x => x.QuotationDate <= dateTo.Value);
            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            query = validityState switch
            {
                "valid" => query.Where(x => x.ValidUntil >= today && x.Status != SalesQuotationStatus.Accepted && x.Status != SalesQuotationStatus.Rejected && x.Status != SalesQuotationStatus.Cancelled),
                "expiring" => query.Where(x => x.ValidUntil >= today && x.ValidUntil <= today.AddDays(2) && (x.Status == SalesQuotationStatus.Sent || x.Status == SalesQuotationStatus.Viewed)),
                "expired" => query.Where(x => x.Status == SalesQuotationStatus.Expired || x.ValidUntil < today),
                "ready" => query.Where(x => x.Status == SalesQuotationStatus.Accepted && !x.SalesOrderCreated),
                _ => query
            };
            return query;
        }

        private static IQueryable<SalesQuotation> ApplySort(IQueryable<SalesQuotation> query, string sortBy, string sortDirection)
        {
            var desc = !string.Equals(sortDirection, "asc", StringComparison.OrdinalIgnoreCase);
            return sortBy switch
            {
                "oldest" => query.OrderBy(x => x.CreatedAt),
                "date" => desc ? query.OrderByDescending(x => x.QuotationDate) : query.OrderBy(x => x.QuotationDate),
                "valid" => desc ? query.OrderByDescending(x => x.ValidUntil) : query.OrderBy(x => x.ValidUntil),
                "customer" => desc ? query.OrderByDescending(x => x.Customer.FirstName) : query.OrderBy(x => x.Customer.FirstName),
                "highest" => query.OrderByDescending(x => x.GrandTotal),
                "lowest" => query.OrderBy(x => x.GrandTotal),
                "status" => desc ? query.OrderByDescending(x => x.Status) : query.OrderBy(x => x.Status),
                _ => query.OrderByDescending(x => x.CreatedAt)
            };
        }

        private async Task<(bool Success, string Message, Opportunity? Opportunity)> ValidateOpportunityForQuotationAsync(int opportunityId, int? currentQuotationId = null)
        {
            var opportunity = await _dbContext.Opportunities.Include(x => x.Customer).FirstOrDefaultAsync(x => x.Id == opportunityId);
            if (opportunity == null || !opportunity.IsActive) return (false, "Opportunity was not found.", null);
            if (opportunity.Stage == OpportunityStage.Lost) return (false, "Lost opportunities cannot be quoted.", opportunity);
            if (opportunity.CustomerId == null || opportunity.Customer == null) return (false, "Opportunity must be linked to a customer before quotation.", opportunity);
            if (!opportunity.Customer.IsActive || opportunity.Customer.IsBlacklisted) return (false, "Customer is inactive or blocked.", opportunity);
            var hasQuotation = await _dbContext.SalesQuotations.AnyAsync(x => x.OpportunityId == opportunityId && x.IsActive && x.Id != currentQuotationId && x.Status != SalesQuotationStatus.Cancelled && x.Status != SalesQuotationStatus.Rejected);
            if (hasQuotation) return (false, "This opportunity already has an active quotation.", opportunity);
            return (true, string.Empty, opportunity);
        }

        private async Task ValidateFormAsync(SalesQuotationFormViewModel model, int? id = null)
        {
            var result = await ValidateOpportunityForQuotationAsync(model.OpportunityId, id);
            if (!result.Success) ModelState.AddModelError(nameof(model.OpportunityId), result.Message);
            if (model.ValidUntil < model.QuotationDate) ModelState.AddModelError(nameof(model.ValidUntil), "Valid until date must be after quotation date.");
            if (!model.Items.Any(x => !string.IsNullOrWhiteSpace(x.Description))) ModelState.AddModelError(string.Empty, "At least one quotation line is required.");
            foreach (var item in model.Items.Where(x => !string.IsNullOrWhiteSpace(x.Description)))
            {
                if (item.Quantity <= 0) ModelState.AddModelError(string.Empty, "Line quantity must be greater than zero.");
                if (item.UnitPrice < 0) ModelState.AddModelError(string.Empty, "Unit price cannot be negative.");
                if (item.DiscountPercentage is < 0 or > 100) ModelState.AddModelError(string.Empty, "Discount must be between 0 and 100.");
                if (item.TaxPercentage is < 0 or > 100) ModelState.AddModelError(string.Empty, "Tax must be between 0 and 100.");
            }
            if (model.ManualDiscountAmount < 0 || model.ManualDiscountAmount > model.Subtotal - model.LineDiscountTotal) ModelState.AddModelError(nameof(model.ManualDiscountAmount), "Manual discount is invalid.");
        }

        private static void Recalculate(SalesQuotationFormViewModel model)
        {
            model.Items = model.Items.Where(x => !string.IsNullOrWhiteSpace(x.Description)).ToList();
            var line = 1;
            foreach (var item in model.Items)
            {
                item.LineNumber = line++;
                item.DisplayOrder = item.LineNumber;
                item.LineSubtotal = Math.Round(item.Quantity * item.UnitPrice, 2);
                item.DiscountAmount = Math.Round(item.LineSubtotal * item.DiscountPercentage / 100m, 2);
                var taxable = Math.Max(0, item.LineSubtotal - item.DiscountAmount);
                item.TaxAmount = Math.Round(taxable * item.TaxPercentage / 100m, 2);
                item.LineTotal = Math.Round(taxable + item.TaxAmount, 2);
            }
            model.Subtotal = model.Items.Sum(x => x.LineSubtotal);
            model.LineDiscountTotal = model.Items.Sum(x => x.DiscountAmount);
            model.ManualDiscountAmount = Math.Min(Math.Max(0, model.ManualDiscountAmount), Math.Max(0, model.Subtotal - model.LineDiscountTotal));
            model.DiscountTotal = model.LineDiscountTotal + model.ManualDiscountAmount;
            model.TaxTotal = model.Items.Sum(x => x.TaxAmount);
            model.GrandTotal = Math.Max(0, model.Subtotal - model.DiscountTotal + model.TaxTotal);
        }

        private static void Recalculate(SalesQuotation quotation)
        {
            foreach (var item in quotation.Items)
            {
                item.LineSubtotal = Math.Round(item.Quantity * item.UnitPrice, 2);
                item.DiscountAmount = Math.Round(item.LineSubtotal * item.DiscountPercentage / 100m, 2);
                var taxable = Math.Max(0, item.LineSubtotal - item.DiscountAmount);
                item.TaxAmount = Math.Round(taxable * item.TaxPercentage / 100m, 2);
                item.LineTotal = Math.Round(taxable + item.TaxAmount, 2);
            }
            quotation.Subtotal = quotation.Items.Sum(x => x.LineSubtotal);
            quotation.LineDiscountTotal = quotation.Items.Sum(x => x.DiscountAmount);
            quotation.DiscountTotal = quotation.LineDiscountTotal + quotation.ManualDiscountAmount;
            quotation.TaxTotal = quotation.Items.Sum(x => x.TaxAmount);
            quotation.GrandTotal = Math.Max(0, quotation.Subtotal - quotation.DiscountTotal + quotation.TaxTotal);
        }

        private static void ApplyTotals(SalesQuotationFormViewModel model, SalesQuotation quotation)
        {
            quotation.Subtotal = model.Subtotal;
            quotation.LineDiscountTotal = model.LineDiscountTotal;
            quotation.ManualDiscountAmount = model.ManualDiscountAmount;
            quotation.DiscountTotal = model.DiscountTotal;
            quotation.TaxTotal = model.TaxTotal;
            quotation.GrandTotal = model.GrandTotal;
        }

        private static void MapItems(SalesQuotationFormViewModel model, SalesQuotation quotation)
        {
            foreach (var item in model.Items)
            {
                quotation.Items.Add(new SalesQuotationItem
                {
                    LineNumber = item.LineNumber,
                    ItemType = item.ItemType,
                    SportId = item.SportId,
                    FacilityId = item.FacilityId,
                    CourtId = item.CourtId,
                    MembershipPlanId = item.MembershipPlanId,
                    Description = item.Description,
                    Quantity = item.Quantity,
                    UnitOfMeasure = item.UnitOfMeasure,
                    UnitPrice = item.UnitPrice,
                    DiscountPercentage = item.DiscountPercentage,
                    DiscountAmount = item.DiscountAmount,
                    TaxPercentage = item.TaxPercentage,
                    TaxAmount = item.TaxAmount,
                    LineSubtotal = item.LineSubtotal,
                    LineTotal = item.LineTotal,
                    DisplayOrder = item.DisplayOrder,
                    CreatedAt = DateTime.UtcNow
                });
            }
        }

        private SalesQuotationFormViewModel MapForm(SalesQuotation quotation)
        {
            var model = new SalesQuotationFormViewModel
            {
                Id = quotation.Id,
                QuotationNumber = quotation.QuotationNumber,
                OpportunityId = quotation.OpportunityId,
                CustomerId = quotation.CustomerId,
                AssignedToUserId = quotation.AssignedToUserId,
                QuotationDate = quotation.QuotationDate,
                ValidUntil = quotation.ValidUntil,
                Status = quotation.Status,
                Subject = quotation.Subject,
                Introduction = quotation.Introduction,
                TermsAndConditions = quotation.TermsAndConditions,
                CustomerNotes = quotation.CustomerNotes,
                InternalNotes = quotation.InternalNotes,
                CustomerReference = quotation.CustomerReference,
                ManualDiscountAmount = quotation.ManualDiscountAmount,
                Currency = quotation.Currency,
                CurrencySymbol = quotation.CurrencySymbol,
                OpportunityLabel = $"{quotation.Opportunity.OpportunityNumber} - {quotation.Opportunity.Name}",
                CustomerLabel = $"{quotation.Customer.CustomerCode} - {quotation.Customer.FullName}",
                Items = quotation.Items.OrderBy(x => x.DisplayOrder).Select(x => new SalesQuotationItemViewModel
                {
                    Id = x.Id,
                    LineNumber = x.LineNumber,
                    ItemType = x.ItemType,
                    SportId = x.SportId,
                    FacilityId = x.FacilityId,
                    CourtId = x.CourtId,
                    MembershipPlanId = x.MembershipPlanId,
                    Description = x.Description,
                    Quantity = x.Quantity,
                    UnitOfMeasure = x.UnitOfMeasure,
                    UnitPrice = x.UnitPrice,
                    DiscountPercentage = x.DiscountPercentage,
                    DiscountAmount = x.DiscountAmount,
                    TaxPercentage = x.TaxPercentage,
                    TaxAmount = x.TaxAmount,
                    LineSubtotal = x.LineSubtotal,
                    LineTotal = x.LineTotal,
                    DisplayOrder = x.DisplayOrder
                }).ToList()
            };
            Recalculate(model);
            return model;
        }

        private async Task<SalesQuotationFormViewModel> AddOptionsAsync(SalesQuotationFormViewModel model)
        {
            model.UserOptions = await UserOptionsAsync(model.AssignedToUserId);
            model.SportOptions = await _dbContext.Sports.AsNoTracking().Where(x => x.IsActive).OrderBy(x => x.DisplayOrder).Select(x => new SelectListItem(x.Name, x.Id.ToString())).ToListAsync();
            model.FacilityOptions = await _dbContext.Facilities.AsNoTracking().Where(x => x.IsActive).OrderBy(x => x.DisplayOrder).Select(x => new SelectListItem(x.Name, x.Id.ToString())).ToListAsync();
            model.CourtOptions = await _dbContext.Courts.AsNoTracking().Where(x => x.IsActive).OrderBy(x => x.DisplayOrder).Select(x => new SelectListItem(x.Name, x.Id.ToString())).ToListAsync();
            model.MembershipPlanOptions = await _dbContext.MembershipPlans.AsNoTracking().Where(x => x.IsActive).OrderBy(x => x.DisplayOrder).Select(x => new SelectListItem($"{x.Name} - {x.JoiningFee:N0}", x.Id.ToString())).ToListAsync();
            model.PricingOptions = await _dbContext.CourtPricings.AsNoTracking().Where(x => x.IsActive).OrderBy(x => x.CourtId).Select(x => new SelectListItem($"{x.Court.Name} - {x.Name} - {x.Price:N0}", x.Price.ToString())).ToListAsync();
            return model;
        }

        private async Task<List<SelectListItem>> UserOptionsAsync(int? selected = null) => await _dbContext.Users.AsNoTracking().Where(x => x.IsActive).OrderBy(x => x.FirstName).Select(x => new SelectListItem(x.FirstName + " " + x.LastName, x.Id.ToString(), x.Id == selected)).ToListAsync();
        private async Task<List<SelectListItem>> CustomerOptionsAsync(int? selected = null) => await _dbContext.Customers.AsNoTracking().OrderBy(x => x.FirstName).Select(x => new SelectListItem(x.FirstName + " " + x.LastName + " - " + x.CustomerCode, x.Id.ToString(), x.Id == selected)).ToListAsync();
        private async Task<List<SelectListItem>> OpportunityOptionsAsync(int? selected = null) => await _dbContext.Opportunities.AsNoTracking().OrderByDescending(x => x.CreatedAt).Select(x => new SelectListItem(x.OpportunityNumber + " - " + x.Name, x.Id.ToString(), x.Id == selected)).ToListAsync();

        private async Task<SystemSettings> GetSettingsAsync() => await _dbContext.SystemSettings.AsNoTracking().FirstOrDefaultAsync() ?? new SystemSettings { ArenaName = "GameHub Arena", Currency = "PKR", CurrencySymbol = "Rs", TaxPercentage = 0, QuotationPrefix = "SQ", DefaultQuotationValidityDays = 7 };

        private async Task<string> GenerateQuotationNumberAsync(string? prefix)
        {
            prefix = string.IsNullOrWhiteSpace(prefix) ? "SQ" : prefix.Trim().ToUpperInvariant();
            var latest = await _dbContext.SalesQuotations.Where(x => x.QuotationNumber.StartsWith(prefix + "-")).OrderByDescending(x => x.QuotationNumber).Select(x => x.QuotationNumber).FirstOrDefaultAsync();
            var next = 1;
            if (!string.IsNullOrWhiteSpace(latest) && int.TryParse(latest.Split('-').LastOrDefault(), out var value)) next = value + 1;
            return $"{prefix}-{next:000000}";
        }

        private async Task AddOpportunityActivityAsync(int opportunityId, string subject, string description)
        {
            _dbContext.OpportunityActivities.Add(new OpportunityActivity { OpportunityId = opportunityId, ActivityType = OpportunityActivityType.Proposal, Subject = subject, Description = description, ActivityDate = DateTime.UtcNow, CreatedAt = DateTime.UtcNow, CreatedByUserId = CurrentUserId(), IsCompleted = true });
            await Task.CompletedTask;
        }

        private async Task SynchronizeExpiredQuotationsAsync(int? id = null)
        {
            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            var query = _dbContext.SalesQuotations.Where(x => (x.Status == SalesQuotationStatus.Sent || x.Status == SalesQuotationStatus.Viewed) && x.ValidUntil < today);
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

        private static void Normalize(SalesQuotationFormViewModel model)
        {
            model.Subject = Clean(model.Subject);
            model.Introduction = Clean(model.Introduction);
            model.TermsAndConditions = Clean(model.TermsAndConditions);
            model.CustomerNotes = Clean(model.CustomerNotes);
            model.InternalNotes = Clean(model.InternalNotes);
            model.CustomerReference = Clean(model.CustomerReference);
            foreach (var item in model.Items)
            {
                item.Description = item.Description?.Trim() ?? string.Empty;
                item.UnitOfMeasure = string.IsNullOrWhiteSpace(item.UnitOfMeasure) ? "Unit" : item.UnitOfMeasure.Trim();
            }
        }

        private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        private async Task RecordApprovalAsync(SalesQuotation quotation, ApprovalAction action, string previousStatus, string? comments, string? rejectionReason, bool isFinal)
        {
            _dbContext.DocumentApprovals.Add(new DocumentApproval
            {
                DocumentType = DocumentType.SalesQuotation, DocumentId = quotation.Id, DocumentNumber = quotation.QuotationNumber,
                RevisionNumber = quotation.RevisionNumber, Action = action, PreviousStatus = previousStatus, NewStatus = quotation.Status.ToString(),
                SubmittedByUserId = CurrentUserId(), ActionByUserId = CurrentUserId(), SubmittedAt = action is ApprovalAction.Submitted or ApprovalAction.Resubmitted ? DateTime.UtcNow : null,
                ActionAt = DateTime.UtcNow, Comments = comments, RejectionReason = rejectionReason, FinancialTotal = quotation.GrandTotal,
                Currency = quotation.Currency, IsFinal = isFinal, IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString(), CreatedAt = DateTime.UtcNow
            });
            await Task.CompletedTask;
        }
        private async Task RecordActivityAsync(int quotationId, ApprovalAction activityType, string description)
        {
            _dbContext.DocumentActivities.Add(new DocumentActivity { DocumentType = DocumentType.SalesQuotation, DocumentId = quotationId, ActivityType = activityType, Description = description, PerformedByUserId = CurrentUserId(), PerformedAt = DateTime.UtcNow });
            await Task.CompletedTask;
        }
        private async Task SaveRevisionAsync(DocumentType type, int id, string number, int revision, object document, string reason)
        {
            _dbContext.DocumentRevisions.Add(new DocumentRevision { DocumentType = type, DocumentId = id, DocumentNumber = number, RevisionNumber = revision, SnapshotJson = System.Text.Json.JsonSerializer.Serialize(document), RevisionReason = reason.Trim(), CreatedByUserId = CurrentUserId(), CreatedAt = DateTime.UtcNow });
            await Task.CompletedTask;
        }
        private static SalesItemType MapItemType(OpportunityType type) => type switch { OpportunityType.Membership => SalesItemType.Membership, OpportunityType.CorporatePackage => SalesItemType.CorporatePackage, OpportunityType.Tournament => SalesItemType.Tournament, OpportunityType.Event => SalesItemType.Event, OpportunityType.Coaching => SalesItemType.Coaching, _ => SalesItemType.CourtBooking };
        private static bool CanTransition(OpportunityStage from, OpportunityStage to) => from == to || from == OpportunityStage.OnHold || to == OpportunityStage.OnHold || from is not (OpportunityStage.Won or OpportunityStage.Lost);
        private int CurrentUserId() => int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : 0;
        private bool CanCreateQuotation() => RolePermissions.TryGetCurrentRole(User, out var role) && role is UserRole.SuperAdmin or UserRole.Admin or UserRole.BookingManager or UserRole.Receptionist;
        private bool CanManageLifecycle() => RolePermissions.TryGetCurrentRole(User, out var role) && role is UserRole.SuperAdmin or UserRole.Admin or UserRole.BookingManager;
        private bool CanApprove() => RolePermissions.TryGetCurrentRole(User, out var role) && role is UserRole.SuperAdmin or UserRole.Admin or UserRole.FinanceManager;
        private bool IsSuperAdmin() => RolePermissions.TryGetCurrentRole(User, out var role) && role == UserRole.SuperAdmin;
        private bool IsReadOnly() => RolePermissions.TryGetCurrentRole(User, out var role) && role is UserRole.Viewer or UserRole.FinanceManager;
        private static string NormalizeTab(string? tab) => tab?.ToLowerInvariant() is "quotations" or "orders" or "invoices" or "payments" ? tab!.ToLowerInvariant() : "overview";
    }
}
