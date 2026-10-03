using System.ComponentModel.DataAnnotations;
using GameHub.Models.Entities;
using GameHub.Models.Enums;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace GameHub.Models.ViewModels
{
    public class InvoiceIndexViewModel
    {
        public IReadOnlyList<InvoiceListItemViewModel> Items { get; set; } = Array.Empty<InvoiceListItemViewModel>();
        public string? Search { get; set; } public InvoiceSourceType? SourceType { get; set; } public InvoiceStatus? Status { get; set; }
        public int CurrentPage { get; set; } = 1; public int PageSize { get; set; } = 10; public int TotalRecords { get; set; } public int TotalPages { get; set; } = 1;
        public string SortBy { get; set; } = "newest"; public IReadOnlyList<SelectListItem> CustomerOptions { get; set; } = Array.Empty<SelectListItem>(); public bool CanCreate { get; set; }
    }
    public class InvoiceListItemViewModel
    {
        public int Id { get; set; } public string InvoiceNumber { get; set; } = string.Empty; public string? Subject { get; set; } public int RevisionNumber { get; set; }
        public InvoiceSourceType SourceType { get; set; } public string? SalesOrderNumber { get; set; } public string CustomerName { get; set; } = string.Empty; public string CustomerCode { get; set; } = string.Empty;
        public DateOnly InvoiceDate { get; set; } public DateOnly DueDate { get; set; } public decimal GrandTotal { get; set; } public decimal OutstandingAmount { get; set; } public decimal PaidAmount { get; set; } public string CurrencySymbol { get; set; } = "Rs"; public InvoiceStatus Status { get; set; }
    }
    public class InvoiceFormViewModel
    {
        public int? Id { get; set; } public int? SalesOrderId { get; set; } public int? SalesQuotationId { get; set; }
        [Required] public int CustomerId { get; set; } public int? OpportunityId { get; set; } public int? AssignedToUserId { get; set; }
        [Required] public DateOnly InvoiceDate { get; set; } = DateOnly.FromDateTime(DateTime.UtcNow); [Required] public DateOnly DueDate { get; set; } = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(7));
        public InvoiceSourceType SourceType { get; set; } = InvoiceSourceType.Direct; [MaxLength(180)] public string? Subject { get; set; } [MaxLength(80)] public string? CustomerPurchaseOrderNumber { get; set; } [MaxLength(80)] public string? CustomerReference { get; set; }
        public string? BillingAddress { get; set; } [Required] public string PaymentTerms { get; set; } = "Due on receipt"; public string? TermsAndConditions { get; set; } public string? CustomerNotes { get; set; } public string? InternalNotes { get; set; }
        public decimal AdjustmentAmount { get; set; } public string Currency { get; set; } = "PKR"; public string CurrencySymbol { get; set; } = "Rs"; public string? RevisionReason { get; set; } public byte[]? RowVersion { get; set; }
        public List<InvoiceItemViewModel> Items { get; set; } = new(); public IReadOnlyList<SelectListItem> CustomerOptions { get; set; } = Array.Empty<SelectListItem>(); public IReadOnlyList<SelectListItem> UserOptions { get; set; } = Array.Empty<SelectListItem>(); public IReadOnlyList<SelectListItem> OpportunityOptions { get; set; } = Array.Empty<SelectListItem>();
        public bool IsEditable { get; set; } = true; public string? SourceLabel { get; set; }
    }
    public class InvoiceItemViewModel
    {
        public int LineNumber { get; set; } public SalesItemType ItemType { get; set; } = SalesItemType.CustomService; [Required] public string Description { get; set; } = string.Empty; [Range(.01, 999999)] public decimal Quantity { get; set; } = 1; [Required] public string UnitOfMeasure { get; set; } = "Unit"; [Range(0, 99999999)] public decimal UnitPrice { get; set; } public decimal DiscountPercentage { get; set; } public decimal TaxPercentage { get; set; } public DateOnly? ServiceDate { get; set; } public TimeOnly? StartTime { get; set; } public TimeOnly? EndTime { get; set; }
    }
    public class InvoiceDetailsViewModel { public SalesInvoice Invoice { get; set; } = null!; public IReadOnlyList<DocumentApproval> ApprovalHistory { get; set; } = Array.Empty<DocumentApproval>(); public IReadOnlyList<DocumentRevision> RevisionHistory { get; set; } = Array.Empty<DocumentRevision>(); public bool CanApprove { get; set; } public bool CanEdit { get; set; } }
    public class InvoicePreviewViewModel { public SalesInvoice Invoice { get; set; } = null!; public SystemSettings Settings { get; set; } = new(); }
    public class InvoiceApprovalViewModel { public int Id { get; set; } public string? Comments { get; set; } public string? Reason { get; set; } }
    public class InvoiceRevisionViewModel { public int Id { get; set; } [Required] public string RevisionReason { get; set; } = string.Empty; }
}
