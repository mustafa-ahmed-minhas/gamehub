using System.Security.Claims;
using System.Data;
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
    [GameHubAuthorize(UserRole.SuperAdmin, UserRole.Admin, UserRole.FinanceManager, UserRole.Receptionist, UserRole.Viewer)]
    [ValidateActiveUser]
    public class PaymentsController : Controller
    {
        private static readonly int[] PageSizes = [10, 25, 50];
        private static readonly InvoiceStatus[] EligibleInvoiceStatuses = [InvoiceStatus.Issued, InvoiceStatus.PartiallyPaid, InvoiceStatus.Overdue];
        private readonly ApplicationDbContext _db;
        public PaymentsController(ApplicationDbContext db) => _db = db;

        public async Task<IActionResult> Index(string? search = null, InvoicePaymentMethod? method = null, PaymentRecordStatus? status = null, int page = 1, int pageSize = 10, string sortBy = "newest")
        {
            if (!CanView()) return RedirectToAction("NotFound", "Error");
            pageSize = PageSizes.Contains(pageSize) ? pageSize : 10;
            var query = _db.Payments.AsNoTracking().Where(x => x.IsActive).Include(x => x.Invoice).Include(x => x.Customer).Include(x => x.ReceivedByUser).AsQueryable();
            if (!string.IsNullOrWhiteSpace(search)) { var term = search.Trim().ToLowerInvariant(); query = query.Where(x => x.PaymentNumber.ToLower().Contains(term) || x.Invoice.InvoiceNumber.ToLower().Contains(term) || (x.Customer.FirstName + " " + x.Customer.LastName).ToLower().Contains(term) || x.Customer.CustomerCode.ToLower().Contains(term) || (x.ReferenceNumber ?? "").ToLower().Contains(term)); }
            if (method.HasValue) query = query.Where(x => x.PaymentMethod == method);
            if (status.HasValue) query = query.Where(x => x.Status == status);
            query = sortBy switch { "oldest" => query.OrderBy(x => x.PaymentDate), "amount" => query.OrderByDescending(x => x.Amount), _ => query.OrderByDescending(x => x.CreatedAt) };
            var total = await query.CountAsync(); var pages = Math.Max(1, (int)Math.Ceiling(total / (double)pageSize)); page = Math.Min(Math.Max(1, page), pages);
            var items = await query.Skip((page - 1) * pageSize).Take(pageSize).Select(x => new PaymentListItemViewModel { Id = x.Id, PaymentNumber = x.PaymentNumber, InvoiceNumber = x.Invoice.InvoiceNumber, CustomerName = x.Customer.FirstName + " " + x.Customer.LastName, CustomerCode = x.Customer.CustomerCode, PaymentDate = x.PaymentDate, PaymentMethod = x.PaymentMethod, Amount = x.Amount, CurrencySymbol = x.CurrencySymbol, Status = x.Status, ReceivedBy = x.ReceivedByUser.FirstName + " " + x.ReceivedByUser.LastName }).ToListAsync();
            return View(new PaymentIndexViewModel { Items = items, Search = search, Method = method, Status = status, SortBy = sortBy, CurrentPage = page, PageSize = pageSize, TotalRecords = total, TotalPages = pages, CanRecord = CanRecord() });
        }

        [HttpGet]
        public async Task<IActionResult> Create(int? invoiceId = null)
        {
            if (!CanRecord()) return RedirectToAction("NotFound", "Error");
            return View("Form", await OptionsAsync(new PaymentFormViewModel { InvoiceId = invoiceId ?? 0 }));
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(PaymentFormViewModel model)
        {
            if (!CanRecord()) return RedirectToAction("NotFound", "Error");
            Normalize(model); var invoice = await EligibleInvoiceAsync(model.InvoiceId);
            if (invoice == null) ModelState.AddModelError(nameof(model.InvoiceId), "Select an issued invoice with an outstanding balance.");
            if (invoice != null && model.Amount > invoice.OutstandingAmount) ModelState.AddModelError(nameof(model.Amount), "Payment cannot exceed the current outstanding amount.");
            if (RequiresReference(model.PaymentMethod) && string.IsNullOrWhiteSpace(model.ReferenceNumber)) ModelState.AddModelError(nameof(model.ReferenceNumber), "A reference number is required for this payment method.");
            if (!ModelState.IsValid) { model.OutstandingAmount = invoice?.OutstandingAmount ?? 0; model.InvoiceTotal = invoice?.GrandTotal ?? 0; model.CurrencySymbol = invoice?.CurrencySymbol ?? "Rs"; return View("Form", await OptionsAsync(model)); }

            await using var tx = await _db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
            invoice = await _db.SalesInvoices.Include(x => x.Customer).FirstOrDefaultAsync(x => x.Id == model.InvoiceId && x.IsActive);
            if (invoice == null || invoice.Status is not (InvoiceStatus.Issued or InvoiceStatus.PartiallyPaid or InvoiceStatus.Overdue) || invoice.OutstandingAmount <= 0 || model.Amount > invoice.OutstandingAmount) { TempData.SetToast("warning", "Payment Not Recorded", "The invoice balance changed. Reload and try again."); return RedirectToAction(nameof(Create), new { invoiceId = model.InvoiceId }); }
            var now = DateTime.UtcNow; var payment = new Payment { PaymentNumber = await NextNumberAsync(), InvoiceId = invoice.Id, CustomerId = invoice.CustomerId, PaymentDate = model.PaymentDate, Amount = model.Amount, PaymentMethod = model.PaymentMethod, ReferenceNumber = model.ReferenceNumber, Notes = model.Notes, ReceivedByUserId = UserId(), CreatedByUserId = UserId(), Currency = invoice.Currency, CurrencySymbol = invoice.CurrencySymbol, CreatedAt = now };
            payment.Allocations.Add(new PaymentAllocation { InvoiceId = invoice.Id, Amount = model.Amount, CreatedAt = now });
            var previousStatus = invoice.Status; invoice.PaidAmount = Math.Round(invoice.PaidAmount + model.Amount, 2); invoice.OutstandingAmount = Math.Max(0, Math.Round(invoice.GrandTotal - invoice.PaidAmount, 2)); invoice.Status = invoice.OutstandingAmount <= 0 ? InvoiceStatus.Paid : InvoiceStatus.PartiallyPaid; invoice.UpdatedAt = now;
            invoice.Customer.OutstandingBalance = Math.Max(0, Math.Round(invoice.Customer.OutstandingBalance - model.Amount, 2));
            _db.Payments.Add(payment); _db.DocumentActivities.Add(new DocumentActivity { DocumentType = DocumentType.SalesInvoice, DocumentId = invoice.Id, ActivityType = ApprovalAction.PaymentRecorded, Description = $"Payment {payment.PaymentNumber} recorded for {invoice.CurrencySymbol} {payment.Amount:N2}.", PerformedByUserId = UserId(), PerformedAt = now });
            _db.DocumentApprovals.Add(new DocumentApproval { DocumentType = DocumentType.SalesInvoice, DocumentId = invoice.Id, DocumentNumber = invoice.InvoiceNumber, RevisionNumber = invoice.RevisionNumber, Action = ApprovalAction.PaymentRecorded, PreviousStatus = previousStatus.ToString(), NewStatus = invoice.Status.ToString(), SubmittedByUserId = UserId(), ActionByUserId = UserId(), ActionAt = now, Comments = $"Payment {payment.PaymentNumber} recorded.", FinancialTotal = payment.Amount, Currency = invoice.Currency, IsFinal = true, CreatedAt = now });
            try { await _db.SaveChangesAsync(); await tx.CommitAsync(); } catch (DbUpdateConcurrencyException) { await tx.RollbackAsync(); TempData.SetToast("warning", "Payment Not Recorded", "The invoice changed in another session. Reload and try again."); return RedirectToAction(nameof(Create), new { invoiceId = model.InvoiceId }); }
            TempData.SetToast("success", "Payment Recorded", $"{payment.PaymentNumber} was recorded successfully."); return RedirectToAction(nameof(Details), new { id = payment.Id });
        }

        public async Task<IActionResult> Details(int id)
        {
            if (!CanView()) return RedirectToAction("NotFound", "Error");
            var payment = await _db.Payments.AsNoTracking().Include(x => x.Invoice).ThenInclude(x => x.SalesOrder).Include(x => x.Customer).Include(x => x.ReceivedByUser).Include(x => x.CreatedByUser).FirstOrDefaultAsync(x => x.Id == id && x.IsActive);
            if (payment == null) return RedirectToAction("NotFound", "Error");
            var allocations = await _db.PaymentAllocations.AsNoTracking().Where(x => x.PaymentId == id).ToListAsync();
            return View(new PaymentDetailsViewModel { Payment = payment, Allocations = allocations, CanRecord = CanRecord() });
        }

        public async Task<IActionResult> Receipt(int id)
        {
            if (!CanView()) return RedirectToAction("NotFound", "Error");
            var payment = await _db.Payments.AsNoTracking().Include(x => x.Invoice).Include(x => x.Customer).Include(x => x.ReceivedByUser).FirstOrDefaultAsync(x => x.Id == id && x.IsActive);
            if (payment == null) return RedirectToAction("NotFound", "Error");
            var settings = await _db.SystemSettings.AsNoTracking().FirstOrDefaultAsync() ?? new SystemSettings { ArenaName = "GameHub Arena", Currency = payment.Currency, CurrencySymbol = payment.CurrencySymbol };
            return View(new PaymentReceiptViewModel { Payment = payment, Settings = settings });
        }

        private async Task<PaymentFormViewModel> OptionsAsync(PaymentFormViewModel model)
        {
            model.InvoiceOptions = await _db.SalesInvoices.AsNoTracking().Where(x => x.IsActive && x.Customer.IsActive && EligibleInvoiceStatuses.Contains(x.Status) && x.OutstandingAmount > 0).OrderByDescending(x => x.DueDate).Select(x => new SelectListItem($"{x.InvoiceNumber} · {x.Customer.FirstName} {x.Customer.LastName} · {x.CurrencySymbol} {x.OutstandingAmount:N0}", x.Id.ToString(), x.Id == model.InvoiceId)).ToListAsync();
            if (model.InvoiceId > 0) { var invoice = await _db.SalesInvoices.AsNoTracking().Include(x => x.Customer).FirstOrDefaultAsync(x => x.Id == model.InvoiceId); if (invoice != null) { model.InvoiceNumber = invoice.InvoiceNumber; model.CustomerName = invoice.Customer.FullName; model.OutstandingAmount = invoice.OutstandingAmount; model.InvoiceTotal = invoice.GrandTotal; model.CurrencySymbol = invoice.CurrencySymbol; } }
            return model;
        }

        private async Task<SalesInvoice?> EligibleInvoiceAsync(int id) => await _db.SalesInvoices.Include(x => x.Customer).FirstOrDefaultAsync(x => x.Id == id && x.IsActive && x.Customer.IsActive && EligibleInvoiceStatuses.Contains(x.Status) && x.OutstandingAmount > 0);
        private async Task<string> NextNumberAsync() { var last = await _db.Payments.OrderByDescending(x => x.PaymentNumber).Select(x => x.PaymentNumber).FirstOrDefaultAsync(); var next = last != null && int.TryParse(last.Split('-').Last(), out var number) ? number + 1 : 1; return $"PAY-{next:000000}"; }
        private static void Normalize(PaymentFormViewModel model) { model.ReferenceNumber = string.IsNullOrWhiteSpace(model.ReferenceNumber) ? null : model.ReferenceNumber.Trim(); model.Notes = string.IsNullOrWhiteSpace(model.Notes) ? null : model.Notes.Trim(); }
        private static bool RequiresReference(InvoicePaymentMethod method) => method is InvoicePaymentMethod.CreditCard or InvoicePaymentMethod.DebitCard or InvoicePaymentMethod.BankTransfer or InvoicePaymentMethod.OnlinePayment;
        private int UserId() => int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : 0;
        private bool CanView() => RolePermissions.CanAccessModule(User, "Payments");
        private bool CanRecord() => RolePermissions.TryGetCurrentRole(User, out var role) && role is UserRole.SuperAdmin or UserRole.Admin or UserRole.FinanceManager;
    }
}
