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
    public class SalesOrdersController : Controller
    {
        private static readonly int[] PageSizes = { 10, 25, 50 };
        private readonly ApplicationDbContext _dbContext;

        public SalesOrdersController(ApplicationDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        [HttpGet]
        public async Task<IActionResult> Index(string? search = null, SalesOrderStatus? status = null, int? customerId = null, int? assignedToUserId = null, DateOnly? dateFrom = null, DateOnly? dateTo = null, string? invoiceState = null, string sortBy = "newest", string sortDirection = "desc", int page = 1, int pageSize = 10)
        {
            if (!CanAccessSales()) return RedirectToAction("NotFound", "Error");

            page = Math.Max(1, page);
            pageSize = PageSizes.Contains(pageSize) ? pageSize : 10;
            var settings = await GetSettingsAsync();
            var query = _dbContext.SalesOrders.AsNoTracking()
                .Include(x => x.Customer)
                .Include(x => x.SalesQuotation)
                .Include(x => x.Opportunity)
                .Include(x => x.AssignedToUser)
                .Where(x => x.IsActive)
                .AsQueryable();

            query = ApplyFilters(query, search, status, customerId, assignedToUserId, dateFrom, dateTo, invoiceState);
            query = ApplySort(query, sortBy, sortDirection);

            var total = await query.CountAsync();
            var totalPages = Math.Max(1, (int)Math.Ceiling(total / (double)pageSize));
            page = Math.Min(page, totalPages);

            var items = await query.Skip((page - 1) * pageSize).Take(pageSize).Select(x => new SalesOrderListItemViewModel
            {
                Id = x.Id,
                SalesOrderNumber = x.SalesOrderNumber,
                Subject = x.Subject,
                OrderDate = x.OrderDate,
                CreatedAt = x.CreatedAt,
                Subtotal = x.Subtotal,
                GrandTotal = x.GrandTotal,
                CurrencySymbol = x.CurrencySymbol,
                Status = x.Status,
                InvoiceCreated = x.InvoiceCreated,
                CustomerName = x.Customer.FirstName + " " + x.Customer.LastName,
                CustomerCode = x.Customer.CustomerCode,
                OrganizationName = x.Customer.OrganizationName,
                QuotationNumber = x.SalesQuotation == null ? "-" : x.SalesQuotation.QuotationNumber,
                SourceType = x.SourceType,
                OpportunityName = x.Opportunity == null ? "No linked opportunity" : x.Opportunity.Name,
                Owner = x.AssignedToUser == null ? null : x.AssignedToUser.FirstName + " " + x.AssignedToUser.LastName,
                CanEdit = x.Status == SalesOrderStatus.Draft
            }).ToListAsync();

            var all = _dbContext.SalesOrders.AsNoTracking().Where(x => x.IsActive);
            var model = new SalesOrderIndexViewModel
            {
                Items = items,
                Search = search,
                Status = status,
                CustomerId = customerId,
                AssignedToUserId = assignedToUserId,
                DateFrom = dateFrom,
                DateTo = dateTo,
                InvoiceState = invoiceState,
                SortBy = sortBy,
                SortDirection = sortDirection,
                CurrentPage = page,
                PageSize = pageSize,
                TotalRecords = total,
                TotalPages = totalPages,
                CurrencySymbol = settings.CurrencySymbol ?? "Rs",
                CanCreate = CanCreateOrder(),
                CanEditDraft = CanEditDraft(),
                CanManageLifecycle = CanManageLifecycle(),
                IsReadOnly = IsReadOnly(),
                DraftCount = await all.CountAsync(x => x.Status == SalesOrderStatus.Draft),
                ConfirmedCount = await all.CountAsync(x => x.Status == SalesOrderStatus.Confirmed || x.Status == SalesOrderStatus.InProgress),
                ReadyForInvoiceCount = await all.CountAsync(x => x.Status == SalesOrderStatus.ReadyForInvoice),
                TotalOrderValue = await all.SumAsync(x => x.GrandTotal),
                CustomerOptions = await CustomerOptionsAsync(customerId),
                UserOptions = await UserOptionsAsync(assignedToUserId)
            };

            return View(model);
        }

        [HttpGet("SalesOrders/CreateFromQuotation/{quotationId:int}")]
        public async Task<IActionResult> CreateFromQuotation(int quotationId)
        {
            if (!CanCreateOrder()) return RedirectToAction("NotFound", "Error");
            var result = await ValidateQuotationForOrderAsync(quotationId);
            if (!result.Success)
            {
                TempData.SetToast("warning", "Sales Order Blocked", result.Message);
                return RedirectToAction("QuotationDetails", "Sales", new { id = quotationId });
            }

            var quotation = result.Quotation!;
            var model = MapCreateForm(quotation);
            return View("Form", await AddOptionsAsync(model));
        }

        [HttpGet("SalesOrders/CreateDirect")]
        public async Task<IActionResult> CreateDirect()
        {
            if (!CanCreateOrder()) return RedirectToAction("NotFound", "Error");
            var settings = await GetSettingsAsync();
            var model = new SalesOrderFormViewModel
            {
                SourceType = "Direct",
                Currency = settings.Currency,
                CurrencySymbol = settings.CurrencySymbol ?? "Rs",
                Items = new List<SalesOrderItemViewModel> { new() { LineNumber = 1, DisplayOrder = 1, Description = string.Empty } }
            };
            return View("Form", await AddOptionsAsync(model));
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(SalesOrderFormViewModel model, string? submitAction)
        {
            if (!CanCreateOrder()) return RedirectToAction("NotFound", "Error");
            Normalize(model);
            Recalculate(model);
            var isQuotationOrder = model.SalesQuotationId.HasValue;
            var validation = isQuotationOrder
                ? await ValidateQuotationForOrderAsync(model.SalesQuotationId!.Value)
                : (true, string.Empty, (SalesQuotation?)null);
            if (!validation.Item1) ModelState.AddModelError(string.Empty, validation.Item2);
            await ValidateFormAsync(model);
            if (!ModelState.IsValid) return View("Form", await AddOptionsAsync(model));

            var quotation = validation.Item3;
            await using var transaction = await _dbContext.Database.BeginTransactionAsync();
            var settings = await GetSettingsAsync();
            var order = new SalesOrder
            {
                SalesOrderNumber = await GenerateSalesOrderNumberAsync(settings.SalesOrderPrefix),
                SalesQuotationId = quotation?.Id,
                OpportunityId = quotation?.OpportunityId ?? model.OpportunityId,
                CustomerId = quotation?.CustomerId ?? model.CustomerId,
                AssignedToUserId = model.AssignedToUserId,
                OrderDate = model.OrderDate,
                ServiceStartDate = model.ServiceStartDate,
                ServiceEndDate = model.ServiceEndDate,
                Status = submitAction == "submit" ? SalesOrderStatus.SubmittedForApproval : SalesOrderStatus.Draft,
                CustomerPurchaseOrderNumber = model.CustomerPurchaseOrderNumber,
                CustomerReference = model.CustomerReference,
                Subject = model.Subject,
                TermsAndConditions = model.TermsAndConditions,
                CustomerNotes = model.CustomerNotes,
                InternalNotes = model.InternalNotes,
                Currency = quotation?.Currency ?? model.Currency,
                CurrencySymbol = quotation?.CurrencySymbol ?? model.CurrencySymbol,
                SourceType = quotation == null ? "Direct" : "Quotation",
                CreatedByUserId = CurrentUserId(),
                CreatedAt = DateTime.UtcNow,
                IsActive = true
            };
            ApplyTotals(model, order);
            MapItems(model, order);
            _dbContext.SalesOrders.Add(order);
            await _dbContext.SaveChangesAsync();

            if (quotation != null)
            {
                quotation.SalesOrderCreated = true;
                quotation.SalesOrderId = order.Id;
                quotation.Status = SalesQuotationStatus.ConvertedToSalesOrder;
                quotation.UpdatedAt = DateTime.UtcNow;
                quotation.UpdatedByUserId = CurrentUserId();
            }
            if (order.OpportunityId.HasValue) await AddOpportunityActivityAsync(order.OpportunityId.Value, "Sales order created", $"Sales Order {order.SalesOrderNumber} was created{(quotation == null ? " directly" : $" from quotation {quotation.QuotationNumber}")}.");
            await RecordActivityAsync(DocumentType.SalesOrder, order.Id, ApprovalAction.Created, $"Sales Order {order.SalesOrderNumber} was created.");
            await _dbContext.SaveChangesAsync();
            await transaction.CommitAsync();

            TempData.SetToast("success", "Sales Order Created", quotation == null ? "A direct draft sales order was created." : "Approved quotation was converted into a sales order.");
            return RedirectToAction(nameof(Details), new { id = order.Id });
        }

        [HttpGet]
        public async Task<IActionResult> Edit(int id)
        {
            if (!CanEditDraft()) return RedirectToAction("NotFound", "Error");
            var order = await LoadOrderQuery(tracked: false).FirstOrDefaultAsync(x => x.Id == id && x.IsActive);
            if (order == null) return RedirectToAction("NotFound", "Error");
            if (!order.IsEditable)
            {
                TempData.SetToast("warning", "Editing Locked", "Only draft sales orders can be edited.");
                return RedirectToAction(nameof(Details), new { id });
            }
            return View("Form", await AddOptionsAsync(MapForm(order)));
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, SalesOrderFormViewModel model, string? submitAction)
        {
            if (!CanEditDraft()) return RedirectToAction("NotFound", "Error");
            if (model.Id != id) return RedirectToAction("NotFound", "Error");
            var order = await LoadOrderQuery(tracked: true).FirstOrDefaultAsync(x => x.Id == id && x.IsActive);
            if (order == null) return RedirectToAction("NotFound", "Error");
            if (!order.IsEditable) return RedirectToAction("NotFound", "Error");

            Normalize(model);
            Recalculate(model);
            await ValidateFormAsync(model);
            if (!ModelState.IsValid) return View("Form", await AddOptionsAsync(model));

            order.AssignedToUserId = model.AssignedToUserId;
            order.OrderDate = model.OrderDate;
            order.ServiceStartDate = model.ServiceStartDate;
            order.ServiceEndDate = model.ServiceEndDate;
            order.CustomerPurchaseOrderNumber = model.CustomerPurchaseOrderNumber;
            order.CustomerReference = model.CustomerReference;
            order.Subject = model.Subject;
            order.TermsAndConditions = model.TermsAndConditions;
            order.CustomerNotes = model.CustomerNotes;
            order.InternalNotes = model.InternalNotes;
            order.UpdatedAt = DateTime.UtcNow;
            order.UpdatedByUserId = CurrentUserId();
            if (submitAction == "submit")
            {
                order.Status = SalesOrderStatus.SubmittedForApproval;
                order.SubmittedAt = DateTime.UtcNow;
                order.IsLocked = true;
            }
            ApplyTotals(model, order);
            _dbContext.SalesOrderItems.RemoveRange(order.Items);
            order.Items.Clear();
            MapItems(model, order);
            await _dbContext.SaveChangesAsync();

            TempData.SetToast("success", "Sales Order Updated", "Sales order changes were saved.");
            return RedirectToAction(nameof(Details), new { id = order.Id });
        }

        [HttpGet]
        public async Task<IActionResult> Details(int id)
        {
            if (!CanAccessSales()) return RedirectToAction("NotFound", "Error");
            var order = await LoadOrderQuery(tracked: false).FirstOrDefaultAsync(x => x.Id == id && x.IsActive);
            if (order == null) return RedirectToAction("NotFound", "Error");
            return View(new SalesOrderDetailsViewModel
            {
                SalesOrder = order,
                CanEdit = CanEditDraft() && order.IsEditable,
                CanManageLifecycle = CanManageLifecycle(),
                CanCancel = CanCancelOrder() && order.Status is SalesOrderStatus.Draft or SalesOrderStatus.Confirmed,
                IsReadOnly = IsReadOnly(),
                ApprovalHistory = await _dbContext.DocumentApprovals.AsNoTracking().Include(x => x.ActionByUser).Where(x => x.DocumentType == DocumentType.SalesOrder && x.DocumentId == id).OrderBy(x => x.ActionAt).ToListAsync(),
                RevisionHistory = await _dbContext.DocumentRevisions.AsNoTracking().Include(x => x.CreatedByUser).Where(x => x.DocumentType == DocumentType.SalesOrder && x.DocumentId == id).OrderBy(x => x.RevisionNumber).ToListAsync()
            });
        }

        [HttpGet]
        public async Task<IActionResult> Preview(int id)
        {
            if (!CanAccessSales()) return RedirectToAction("NotFound", "Error");
            var order = await LoadOrderQuery(tracked: false).FirstOrDefaultAsync(x => x.Id == id && x.IsActive);
            if (order == null) return RedirectToAction("NotFound", "Error");
            return View(new SalesOrderPreviewViewModel { SalesOrder = order, Settings = await GetSettingsAsync() });
        }

        [HttpPost, ValidateAntiForgeryToken] public async Task<IActionResult> SubmitForApproval(int id, string? comments) => await SubmitOrderAsync(id, comments, false);
        [HttpPost, ValidateAntiForgeryToken] public async Task<IActionResult> Resubmit(int id, string revisionReason) => await SubmitOrderAsync(id, revisionReason, true);
        [HttpPost, ValidateAntiForgeryToken] public async Task<IActionResult> Approve(int id, string? comments) => await ApproveOrderAsync(id, comments);
        [HttpPost, ValidateAntiForgeryToken] public async Task<IActionResult> Reject(int id, string rejectionReason) => await RejectOrderAsync(id, rejectionReason);
        [HttpPost, ValidateAntiForgeryToken] public async Task<IActionResult> Confirm(int id) => await ChangeStatusAsync(id, SalesOrderStatus.Approved, SalesOrderStatus.Confirmed, "Sales order confirmed.", o => o.ConfirmedAt = DateTime.UtcNow);
        [HttpPost, ValidateAntiForgeryToken] public async Task<IActionResult> StartProgress(int id) => await ChangeStatusAsync(id, SalesOrderStatus.Confirmed, SalesOrderStatus.InProgress, "Sales order started.");
        [HttpPost, ValidateAntiForgeryToken] public async Task<IActionResult> MarkFulfilled(int id) => await ChangeStatusAsync(id, SalesOrderStatus.InProgress, SalesOrderStatus.Fulfilled, "Sales order marked as fulfilled.", o => o.FulfilledAt = DateTime.UtcNow);
        [HttpPost, ValidateAntiForgeryToken] public async Task<IActionResult> MarkReadyForInvoice(int id)
        {
            var order = await _dbContext.SalesOrders.FirstOrDefaultAsync(x => x.Id == id && x.IsActive);
            if (order == null) return RedirectToAction("NotFound", "Error");
            if (order.Status == SalesOrderStatus.Fulfilled) return await ChangeStatusAsync(id, SalesOrderStatus.Fulfilled, SalesOrderStatus.ReadyForInvoice, "Sales order is ready for invoice.");
            return RedirectToAction("NotFound", "Error");
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Cancel(int id, string reason)
        {
            if (!CanCancelOrder()) return RedirectToAction("NotFound", "Error");
            if (string.IsNullOrWhiteSpace(reason))
            {
                TempData.SetToast("warning", "Reason Required", "Cancellation reason is required.");
                return RedirectToAction(nameof(Details), new { id });
            }
            var order = await _dbContext.SalesOrders.FirstOrDefaultAsync(x => x.Id == id && x.IsActive);
            if (order == null || order.Status is not (SalesOrderStatus.Draft or SalesOrderStatus.Confirmed)) return RedirectToAction("NotFound", "Error");
            order.Status = SalesOrderStatus.Cancelled;
            order.CancellationReason = reason.Trim();
            order.CancelledAt = DateTime.UtcNow;
            order.IsActive = false;
            order.UpdatedAt = DateTime.UtcNow;
            order.UpdatedByUserId = CurrentUserId();
            var quotation = order.SalesQuotationId.HasValue ? await _dbContext.SalesQuotations.FirstOrDefaultAsync(x => x.Id == order.SalesQuotationId.Value) : null;
            if (quotation != null)
            {
                quotation.SalesOrderCreated = false;
                quotation.SalesOrderId = null;
                quotation.UpdatedAt = DateTime.UtcNow;
                quotation.UpdatedByUserId = CurrentUserId();
            }
            if (order.OpportunityId.HasValue) await AddOpportunityActivityAsync(order.OpportunityId.Value, "Sales order cancelled", $"Sales Order {order.SalesOrderNumber} was cancelled. Reason: {order.CancellationReason}");
            await RecordApprovalAsync(DocumentType.SalesOrder, order.Id, order.SalesOrderNumber, order.RevisionNumber, ApprovalAction.Cancelled, "", SalesOrderStatus.Cancelled.ToString(), order.GrandTotal, order.Currency, reason, reason, true);
            await _dbContext.SaveChangesAsync();
            TempData.SetToast("success", "Sales Order Cancelled", "Sales order was cancelled.");
            return RedirectToAction(nameof(Index));
        }

        private async Task<IActionResult> ChangeStatusAsync(int id, SalesOrderStatus from, SalesOrderStatus to, string message, Action<SalesOrder>? mutate = null)
        {
            if (!CanManageLifecycle()) return RedirectToAction("NotFound", "Error");
            var order = await _dbContext.SalesOrders.FirstOrDefaultAsync(x => x.Id == id && x.IsActive);
            if (order == null || order.Status != from) return RedirectToAction("NotFound", "Error");
            order.Status = to;
            order.UpdatedAt = DateTime.UtcNow;
            order.UpdatedByUserId = CurrentUserId();
            mutate?.Invoke(order);
            if (order.OpportunityId.HasValue) await AddOpportunityActivityAsync(order.OpportunityId.Value, "Sales order updated", $"{order.SalesOrderNumber} moved to {to.GetDisplayName()}.");
            await RecordActivityAsync(DocumentType.SalesOrder, order.Id, to == SalesOrderStatus.Confirmed ? ApprovalAction.Confirmed : to == SalesOrderStatus.Fulfilled ? ApprovalAction.Fulfilled : ApprovalAction.Created, $"{order.SalesOrderNumber} moved to {to.GetDisplayName()}.");
            await _dbContext.SaveChangesAsync();
            TempData.SetToast("success", "Sales Order Updated", message);
            return RedirectToAction(nameof(Details), new { id });
        }

        private IQueryable<SalesOrder> LoadOrderQuery(bool tracked)
        {
            var query = _dbContext.SalesOrders
                .Include(x => x.SalesQuotation)
                .Include(x => x.Opportunity)
                .Include(x => x.Customer)
                .Include(x => x.AssignedToUser)
                .Include(x => x.Items).ThenInclude(x => x.Sport)
                .Include(x => x.Items).ThenInclude(x => x.Facility)
                .Include(x => x.Items).ThenInclude(x => x.Court)
                .Include(x => x.Items).ThenInclude(x => x.MembershipPlan)
                .AsQueryable();
            return tracked ? query : query.AsNoTracking();
        }

        private async Task<IActionResult> SubmitOrderAsync(int id, string? notes, bool revision)
        {
            if (!CanCreateOrder()) return RedirectToAction("NotFound", "Error");
            var order = await _dbContext.SalesOrders.Include(x => x.Items).FirstOrDefaultAsync(x => x.Id == id && x.IsActive);
            if (order == null || !order.IsEditable) return RedirectToAction("NotFound", "Error");
            if (revision && string.IsNullOrWhiteSpace(notes))
            {
                TempData.SetToast("warning", "Revision Notes Required", "Describe the changes made before resubmitting.");
                return RedirectToAction(nameof(Details), new { id });
            }
            await using var transaction = await _dbContext.Database.BeginTransactionAsync();
            if (revision)
            {
                await SaveRevisionAsync(DocumentType.SalesOrder, order.Id, order.SalesOrderNumber, order.RevisionNumber, order, notes!);
                order.RevisionNumber++;
                order.LastRevisionReason = notes!.Trim();
            }
            var previous = order.Status.ToString();
            order.Status = SalesOrderStatus.SubmittedForApproval;
            order.ApprovalStatus = ApprovalStatus.PendingApproval;
            order.SubmittedAt = DateTime.UtcNow;
            order.IsLocked = true;
            order.UpdatedAt = DateTime.UtcNow;
            await RecordApprovalAsync(DocumentType.SalesOrder, id, order.SalesOrderNumber, order.RevisionNumber, revision ? ApprovalAction.Resubmitted : ApprovalAction.Submitted, previous, order.Status.ToString(), order.GrandTotal, order.Currency, notes, null, false);
            await RecordActivityAsync(DocumentType.SalesOrder, id, revision ? ApprovalAction.Resubmitted : ApprovalAction.Submitted, "Sales order submitted for approval.");
            await _dbContext.SaveChangesAsync();
            await transaction.CommitAsync();
            TempData.SetToast("success", "Submitted for Approval", "Sales order is locked until an approver acts.");
            return RedirectToAction(nameof(Details), new { id });
        }

        private async Task<IActionResult> ApproveOrderAsync(int id, string? comments)
        {
            if (!CanApprove()) return RedirectToAction("NotFound", "Error");
            var order = await _dbContext.SalesOrders.FirstOrDefaultAsync(x => x.Id == id && x.IsActive);
            if (order == null || order.Status != SalesOrderStatus.SubmittedForApproval) return RedirectToAction("NotFound", "Error");
            if (order.CreatedByUserId == CurrentUserId() && !IsSuperAdmin())
            {
                TempData.SetToast("warning", "Approval Blocked", "You cannot approve your own sales order.");
                return RedirectToAction(nameof(Details), new { id });
            }
            order.Status = SalesOrderStatus.Approved;
            order.ApprovalStatus = ApprovalStatus.Approved;
            order.ApprovedAt = DateTime.UtcNow;
            order.ApprovedByUserId = CurrentUserId();
            order.IsLocked = true;
            await RecordApprovalAsync(DocumentType.SalesOrder, id, order.SalesOrderNumber, order.RevisionNumber, ApprovalAction.Approved, SalesOrderStatus.SubmittedForApproval.ToString(), order.Status.ToString(), order.GrandTotal, order.Currency, comments, null, true);
            await RecordActivityAsync(DocumentType.SalesOrder, id, ApprovalAction.Approved, "Sales order approved.");
            await _dbContext.SaveChangesAsync();
            TempData.SetToast("success", "Sales Order Approved", "The order can now be confirmed.");
            return RedirectToAction(nameof(Details), new { id });
        }

        private async Task<IActionResult> RejectOrderAsync(int id, string rejectionReason)
        {
            if (!CanApprove()) return RedirectToAction("NotFound", "Error");
            if (string.IsNullOrWhiteSpace(rejectionReason))
            {
                TempData.SetToast("warning", "Reason Required", "Provide a rejection reason.");
                return RedirectToAction(nameof(Details), new { id });
            }
            var order = await _dbContext.SalesOrders.FirstOrDefaultAsync(x => x.Id == id && x.IsActive);
            if (order == null || order.Status != SalesOrderStatus.SubmittedForApproval) return RedirectToAction("NotFound", "Error");
            order.Status = SalesOrderStatus.Rejected;
            order.ApprovalStatus = ApprovalStatus.Rejected;
            order.RejectedAt = DateTime.UtcNow;
            order.RejectedByUserId = CurrentUserId();
            order.RejectionReason = rejectionReason.Trim();
            order.IsLocked = false;
            await RecordApprovalAsync(DocumentType.SalesOrder, id, order.SalesOrderNumber, order.RevisionNumber, ApprovalAction.Rejected, SalesOrderStatus.SubmittedForApproval.ToString(), order.Status.ToString(), order.GrandTotal, order.Currency, null, order.RejectionReason, true);
            await RecordActivityAsync(DocumentType.SalesOrder, id, ApprovalAction.Rejected, $"Sales order rejected: {order.RejectionReason}");
            await _dbContext.SaveChangesAsync();
            TempData.SetToast("info", "Sales Order Rejected", "The sales order is editable for revision.");
            return RedirectToAction(nameof(Details), new { id });
        }

        private async Task<(bool Success, string Message, SalesQuotation? Quotation)> ValidateQuotationForOrderAsync(int quotationId)
        {
            var quotation = await _dbContext.SalesQuotations
                .Include(x => x.Customer)
                .Include(x => x.Opportunity)
                .Include(x => x.Items)
                .FirstOrDefaultAsync(x => x.Id == quotationId);
            if (quotation == null || !quotation.IsActive) return (false, "Quotation was not found.", null);
            if (quotation.Status is not (SalesQuotationStatus.Approved or SalesQuotationStatus.AcceptedByCustomer)) return (false, "Only approved or customer-accepted quotations can be converted into sales orders.", quotation);
            if (!quotation.Items.Any()) return (false, "Quotation must contain at least one line item.", quotation);
            if (quotation.Customer == null || !quotation.Customer.IsActive) return (false, "Customer is inactive or missing.", quotation);
            if (quotation.Customer.IsBlacklisted) return (false, "Blacklisted customers cannot be converted into sales orders.", quotation);
            if (quotation.Opportunity == null || !quotation.Opportunity.IsActive) return (false, "Opportunity is inactive or missing.", quotation);
            var exists = await _dbContext.SalesOrders.AnyAsync(x => x.SalesQuotationId == quotationId && x.IsActive);
            if (exists) return (false, "This quotation already has an active sales order.", quotation);
            return (true, string.Empty, quotation);
        }

        private async Task ValidateFormAsync(SalesOrderFormViewModel model)
        {
            var customer = await _dbContext.Customers.AsNoTracking().FirstOrDefaultAsync(x => x.Id == model.CustomerId);
            if (customer == null || !customer.IsActive || customer.IsBlacklisted) ModelState.AddModelError(nameof(model.CustomerId), "Select an active, non-blacklisted customer.");
            if (model.OpportunityId.HasValue && !await _dbContext.Opportunities.AsNoTracking().AnyAsync(x => x.Id == model.OpportunityId.Value && x.IsActive)) ModelState.AddModelError(nameof(model.OpportunityId), "Selected opportunity is not available.");
            if (model.ServiceEndDate.HasValue && model.ServiceStartDate.HasValue && model.ServiceEndDate < model.ServiceStartDate)
                ModelState.AddModelError(nameof(model.ServiceEndDate), "Service end date cannot be before start date.");
            if (!model.Items.Any(x => !string.IsNullOrWhiteSpace(x.Description))) ModelState.AddModelError(string.Empty, "At least one sales order line is required.");
            foreach (var item in model.Items.Where(x => !string.IsNullOrWhiteSpace(x.Description)))
            {
                if (item.Quantity <= 0) ModelState.AddModelError(string.Empty, "Line quantity must be greater than zero.");
                if (item.UnitPrice < 0) ModelState.AddModelError(string.Empty, "Unit price cannot be negative.");
                if (item.DiscountPercentage is < 0 or > 100) ModelState.AddModelError(string.Empty, "Discount must be between 0 and 100.");
                if (item.TaxPercentage is < 0 or > 100) ModelState.AddModelError(string.Empty, "Tax must be between 0 and 100.");
                if (item.StartTime.HasValue && item.EndTime.HasValue && item.EndTime <= item.StartTime)
                    ModelState.AddModelError(string.Empty, "Line end time must be after start time.");
                if (item.SportId.HasValue && !await _dbContext.Sports.AnyAsync(x => x.Id == item.SportId.Value && x.IsActive)) ModelState.AddModelError(string.Empty, "Selected sport is invalid.");
                if (item.FacilityId.HasValue && !await _dbContext.Facilities.AnyAsync(x => x.Id == item.FacilityId.Value && x.IsActive)) ModelState.AddModelError(string.Empty, "Selected facility is invalid.");
                if (item.CourtId.HasValue && !await _dbContext.Courts.AnyAsync(x => x.Id == item.CourtId.Value && x.IsActive)) ModelState.AddModelError(string.Empty, "Selected court is invalid.");
                if (item.MembershipPlanId.HasValue && !await _dbContext.MembershipPlans.AnyAsync(x => x.Id == item.MembershipPlanId.Value && x.IsActive)) ModelState.AddModelError(string.Empty, "Selected membership plan is invalid.");
            }
        }

        private SalesOrderFormViewModel MapCreateForm(SalesQuotation quotation)
        {
            var model = new SalesOrderFormViewModel
            {
                SalesQuotationId = quotation.Id,
                OpportunityId = quotation.OpportunityId,
                CustomerId = quotation.CustomerId,
                AssignedToUserId = quotation.AssignedToUserId,
                OrderDate = DateOnly.FromDateTime(DateTime.UtcNow),
                Subject = quotation.Subject ?? quotation.Opportunity.Name,
                CustomerReference = quotation.CustomerReference,
                TermsAndConditions = quotation.TermsAndConditions,
                CustomerNotes = quotation.CustomerNotes,
                InternalNotes = quotation.InternalNotes,
                Currency = quotation.Currency,
                CurrencySymbol = quotation.CurrencySymbol,
                QuotationLabel = $"{quotation.QuotationNumber} - {quotation.Subject ?? quotation.Opportunity.Name}",
                OpportunityLabel = $"{quotation.Opportunity.OpportunityNumber} - {quotation.Opportunity.Name}",
                CustomerLabel = $"{quotation.Customer.CustomerCode} - {quotation.Customer.FullName}",
                Items = quotation.Items.OrderBy(x => x.DisplayOrder).Select(x => new SalesOrderItemViewModel
                {
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
                    TaxPercentage = x.TaxPercentage,
                    DisplayOrder = x.DisplayOrder
                }).ToList()
            };
            Recalculate(model);
            return model;
        }

        private SalesOrderFormViewModel MapForm(SalesOrder order)
        {
            var model = new SalesOrderFormViewModel
            {
                Id = order.Id,
                SalesOrderNumber = order.SalesOrderNumber,
                SalesQuotationId = order.SalesQuotationId,
                OpportunityId = order.OpportunityId,
                CustomerId = order.CustomerId,
                AssignedToUserId = order.AssignedToUserId,
                OrderDate = order.OrderDate,
                ServiceStartDate = order.ServiceStartDate,
                ServiceEndDate = order.ServiceEndDate,
                Status = order.Status,
                CustomerPurchaseOrderNumber = order.CustomerPurchaseOrderNumber,
                CustomerReference = order.CustomerReference,
                Subject = order.Subject,
                TermsAndConditions = order.TermsAndConditions,
                CustomerNotes = order.CustomerNotes,
                InternalNotes = order.InternalNotes,
                Currency = order.Currency,
                CurrencySymbol = order.CurrencySymbol,
                QuotationLabel = order.SalesQuotation == null ? "Direct order" : $"{order.SalesQuotation.QuotationNumber} - {order.SalesQuotation.Subject ?? order.Opportunity?.Name}",
                OpportunityLabel = order.Opportunity == null ? "No linked opportunity" : $"{order.Opportunity.OpportunityNumber} - {order.Opportunity.Name}",
                CustomerLabel = $"{order.Customer.CustomerCode} - {order.Customer.FullName}",
                SourceType = order.SourceType,
                IsEditable = order.IsEditable,
                Items = order.Items.OrderBy(x => x.DisplayOrder).Select(x => new SalesOrderItemViewModel
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
                    TaxPercentage = x.TaxPercentage,
                    ServiceDate = x.ServiceDate,
                    StartTime = x.StartTime,
                    EndTime = x.EndTime,
                    DisplayOrder = x.DisplayOrder
                }).ToList()
            };
            Recalculate(model);
            return model;
        }

        private static void Recalculate(SalesOrderFormViewModel model)
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
            model.DiscountTotal = model.Items.Sum(x => x.DiscountAmount);
            model.TaxTotal = model.Items.Sum(x => x.TaxAmount);
            model.GrandTotal = Math.Max(0, model.Subtotal - model.DiscountTotal + model.TaxTotal);
        }

        private static void ApplyTotals(SalesOrderFormViewModel model, SalesOrder order)
        {
            order.Subtotal = model.Subtotal;
            order.DiscountTotal = model.DiscountTotal;
            order.TaxTotal = model.TaxTotal;
            order.GrandTotal = model.GrandTotal;
        }

        private static void MapItems(SalesOrderFormViewModel model, SalesOrder order)
        {
            foreach (var item in model.Items)
            {
                order.Items.Add(new SalesOrderItem
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
                    ServiceDate = item.ServiceDate,
                    StartTime = item.StartTime,
                    EndTime = item.EndTime,
                    DisplayOrder = item.DisplayOrder,
                    CreatedAt = DateTime.UtcNow
                });
            }
        }

        private async Task<SalesOrderFormViewModel> AddOptionsAsync(SalesOrderFormViewModel model)
        {
            model.UserOptions = await UserOptionsAsync(model.AssignedToUserId);
            model.CustomerOptions = await CustomerOptionsAsync(model.CustomerId);
            model.OpportunityOptions = await _dbContext.Opportunities.AsNoTracking().Where(x => x.IsActive).OrderByDescending(x => x.CreatedAt).Select(x => new SelectListItem(x.OpportunityNumber + " - " + x.Name, x.Id.ToString(), x.Id == model.OpportunityId)).ToListAsync();
            model.SportOptions = await _dbContext.Sports.AsNoTracking().Where(x => x.IsActive).OrderBy(x => x.DisplayOrder).Select(x => new SelectListItem(x.Name, x.Id.ToString())).ToListAsync();
            model.FacilityOptions = await _dbContext.Facilities.AsNoTracking().Where(x => x.IsActive).OrderBy(x => x.DisplayOrder).Select(x => new SelectListItem(x.Name, x.Id.ToString())).ToListAsync();
            model.CourtOptions = await _dbContext.Courts.AsNoTracking().Where(x => x.IsActive).OrderBy(x => x.DisplayOrder).Select(x => new SelectListItem(x.Name, x.Id.ToString())).ToListAsync();
            model.MembershipPlanOptions = await _dbContext.MembershipPlans.AsNoTracking().Where(x => x.IsActive).OrderBy(x => x.DisplayOrder).Select(x => new SelectListItem(x.Name, x.Id.ToString())).ToListAsync();
            return model;
        }

        private IQueryable<SalesOrder> ApplyFilters(IQueryable<SalesOrder> query, string? search, SalesOrderStatus? status, int? customerId, int? assignedToUserId, DateOnly? dateFrom, DateOnly? dateTo, string? invoiceState)
        {
            if (!string.IsNullOrWhiteSpace(search))
            {
                var term = search.Trim().ToLower();
                query = query.Where(x => x.SalesOrderNumber.ToLower().Contains(term) || (x.SalesQuotation != null && x.SalesQuotation.QuotationNumber.ToLower().Contains(term)) || (x.Subject ?? "").ToLower().Contains(term) || (x.CustomerReference ?? "").ToLower().Contains(term) || (x.CustomerPurchaseOrderNumber ?? "").ToLower().Contains(term) || x.Customer.CustomerCode.ToLower().Contains(term) || (x.Customer.FirstName + " " + x.Customer.LastName).ToLower().Contains(term) || (x.Opportunity != null && x.Opportunity.Name.ToLower().Contains(term)));
            }
            if (status.HasValue) query = query.Where(x => x.Status == status.Value);
            if (customerId.HasValue) query = query.Where(x => x.CustomerId == customerId.Value);
            if (assignedToUserId.HasValue) query = query.Where(x => x.AssignedToUserId == assignedToUserId.Value);
            if (dateFrom.HasValue) query = query.Where(x => x.OrderDate >= dateFrom.Value);
            if (dateTo.HasValue) query = query.Where(x => x.OrderDate <= dateTo.Value);
            query = invoiceState switch
            {
                "not-created" => query.Where(x => !x.InvoiceCreated && x.Status != SalesOrderStatus.ReadyForInvoice),
                "ready" => query.Where(x => !x.InvoiceCreated && x.Status == SalesOrderStatus.ReadyForInvoice),
                "created" => query.Where(x => x.InvoiceCreated),
                _ => query
            };
            return query;
        }

        private static IQueryable<SalesOrder> ApplySort(IQueryable<SalesOrder> query, string sortBy, string sortDirection)
        {
            var desc = !string.Equals(sortDirection, "asc", StringComparison.OrdinalIgnoreCase);
            return sortBy switch
            {
                "oldest" => query.OrderBy(x => x.CreatedAt),
                "date" => desc ? query.OrderByDescending(x => x.OrderDate) : query.OrderBy(x => x.OrderDate),
                "customer" => desc ? query.OrderByDescending(x => x.Customer.FirstName) : query.OrderBy(x => x.Customer.FirstName),
                "highest" => query.OrderByDescending(x => x.GrandTotal),
                "lowest" => query.OrderBy(x => x.GrandTotal),
                "status" => desc ? query.OrderByDescending(x => x.Status) : query.OrderBy(x => x.Status),
                _ => query.OrderByDescending(x => x.CreatedAt)
            };
        }

        private static void Normalize(SalesOrderFormViewModel model)
        {
            model.Subject = Clean(model.Subject);
            model.CustomerPurchaseOrderNumber = Clean(model.CustomerPurchaseOrderNumber);
            model.CustomerReference = Clean(model.CustomerReference);
            model.TermsAndConditions = Clean(model.TermsAndConditions);
            model.CustomerNotes = Clean(model.CustomerNotes);
            model.InternalNotes = Clean(model.InternalNotes);
            foreach (var item in model.Items)
            {
                item.Description = item.Description?.Trim() ?? string.Empty;
                item.UnitOfMeasure = string.IsNullOrWhiteSpace(item.UnitOfMeasure) ? "Unit" : item.UnitOfMeasure.Trim();
            }
        }

        private async Task AddOpportunityActivityAsync(int opportunityId, string subject, string description)
        {
            _dbContext.OpportunityActivities.Add(new OpportunityActivity
            {
                OpportunityId = opportunityId,
                ActivityType = OpportunityActivityType.Proposal,
                Subject = subject,
                Description = description,
                ActivityDate = DateTime.UtcNow,
                CreatedAt = DateTime.UtcNow,
                CreatedByUserId = CurrentUserId(),
                IsCompleted = true
            });
            await Task.CompletedTask;
        }

        private async Task RecordApprovalAsync(DocumentType type, int documentId, string number, int revision, ApprovalAction action, string previous, string current, decimal total, string currency, string? comments, string? rejectionReason, bool isFinal)
        {
            _dbContext.DocumentApprovals.Add(new DocumentApproval
            {
                DocumentType = type, DocumentId = documentId, DocumentNumber = number, RevisionNumber = revision, Action = action,
                PreviousStatus = previous, NewStatus = current, SubmittedByUserId = CurrentUserId(), ActionByUserId = CurrentUserId(),
                SubmittedAt = action is ApprovalAction.Submitted or ApprovalAction.Resubmitted ? DateTime.UtcNow : null,
                ActionAt = DateTime.UtcNow, Comments = Clean(comments), RejectionReason = Clean(rejectionReason), FinancialTotal = total,
                Currency = currency, IsFinal = isFinal, IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString(), CreatedAt = DateTime.UtcNow
            });
            await Task.CompletedTask;
        }

        private async Task RecordActivityAsync(DocumentType type, int documentId, ApprovalAction action, string description)
        {
            _dbContext.DocumentActivities.Add(new DocumentActivity { DocumentType = type, DocumentId = documentId, ActivityType = action, Description = description, PerformedByUserId = CurrentUserId(), PerformedAt = DateTime.UtcNow });
            await Task.CompletedTask;
        }

        private async Task SaveRevisionAsync(DocumentType type, int documentId, string number, int revision, object document, string reason)
        {
            _dbContext.DocumentRevisions.Add(new DocumentRevision { DocumentType = type, DocumentId = documentId, DocumentNumber = number, RevisionNumber = revision, SnapshotJson = System.Text.Json.JsonSerializer.Serialize(document), RevisionReason = reason.Trim(), CreatedByUserId = CurrentUserId(), CreatedAt = DateTime.UtcNow });
            await Task.CompletedTask;
        }

        private async Task<string> GenerateSalesOrderNumberAsync(string? prefix)
        {
            prefix = string.IsNullOrWhiteSpace(prefix) ? "SO" : prefix.Trim().ToUpperInvariant();
            var latest = await _dbContext.SalesOrders.Where(x => x.SalesOrderNumber.StartsWith(prefix + "-")).OrderByDescending(x => x.SalesOrderNumber).Select(x => x.SalesOrderNumber).FirstOrDefaultAsync();
            var next = 1;
            if (!string.IsNullOrWhiteSpace(latest) && int.TryParse(latest.Split('-').LastOrDefault(), out var value)) next = value + 1;
            return $"{prefix}-{next:000000}";
        }

        private async Task<SystemSettings> GetSettingsAsync() => await _dbContext.SystemSettings.AsNoTracking().FirstOrDefaultAsync() ?? new SystemSettings { ArenaName = "GameHub Arena", Currency = "PKR", CurrencySymbol = "Rs", SalesOrderPrefix = "SO" };
        private async Task<List<SelectListItem>> UserOptionsAsync(int? selected = null) => await _dbContext.Users.AsNoTracking().Where(x => x.IsActive).OrderBy(x => x.FirstName).Select(x => new SelectListItem(x.FirstName + " " + x.LastName, x.Id.ToString(), x.Id == selected)).ToListAsync();
        private async Task<List<SelectListItem>> CustomerOptionsAsync(int? selected = null) => await _dbContext.Customers.AsNoTracking().OrderBy(x => x.FirstName).Select(x => new SelectListItem(x.FirstName + " " + x.LastName + " - " + x.CustomerCode, x.Id.ToString(), x.Id == selected)).ToListAsync();
        private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        private int CurrentUserId() => int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : 0;
        private bool CanAccessSales() => RolePermissions.CanAccessModule(User, "Sales");
        private bool CanCreateOrder() => RolePermissions.TryGetCurrentRole(User, out var role) && role is UserRole.SuperAdmin or UserRole.Admin or UserRole.BookingManager or UserRole.Receptionist;
        private bool CanEditDraft() => RolePermissions.TryGetCurrentRole(User, out var role) && role is UserRole.SuperAdmin or UserRole.Admin or UserRole.BookingManager or UserRole.Receptionist;
        private bool CanManageLifecycle() => RolePermissions.TryGetCurrentRole(User, out var role) && role is UserRole.SuperAdmin or UserRole.Admin or UserRole.BookingManager;
        private bool CanApprove() => RolePermissions.TryGetCurrentRole(User, out var role) && role is UserRole.SuperAdmin or UserRole.Admin or UserRole.FinanceManager;
        private bool IsSuperAdmin() => RolePermissions.TryGetCurrentRole(User, out var role) && role == UserRole.SuperAdmin;
        private bool CanCancelOrder() => CanManageLifecycle();
        private bool IsReadOnly() => RolePermissions.TryGetCurrentRole(User, out var role) && role is UserRole.Viewer or UserRole.FinanceManager;
    }
}
