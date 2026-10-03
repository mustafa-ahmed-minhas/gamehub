using System.ComponentModel.DataAnnotations;
using GameHub.Models.Entities;
using GameHub.Models.Enums;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace GameHub.Models.ViewModels
{
    public class SalesOrderIndexViewModel
    {
        public IReadOnlyList<SalesOrderListItemViewModel> Items { get; set; } = Array.Empty<SalesOrderListItemViewModel>();
        public string? Search { get; set; }
        public SalesOrderStatus? Status { get; set; }
        public int? CustomerId { get; set; }
        public int? AssignedToUserId { get; set; }
        public DateOnly? DateFrom { get; set; }
        public DateOnly? DateTo { get; set; }
        public string? InvoiceState { get; set; }
        public string SortBy { get; set; } = "newest";
        public string SortDirection { get; set; } = "desc";
        public int CurrentPage { get; set; } = 1;
        public int PageSize { get; set; } = 10;
        public int TotalRecords { get; set; }
        public int TotalPages { get; set; } = 1;
        public string CurrencySymbol { get; set; } = "Rs";
        public bool CanCreate { get; set; }
        public bool CanEditDraft { get; set; }
        public bool CanManageLifecycle { get; set; }
        public bool IsReadOnly { get; set; }
        public int DraftCount { get; set; }
        public int ConfirmedCount { get; set; }
        public int ReadyForInvoiceCount { get; set; }
        public decimal TotalOrderValue { get; set; }
        public IReadOnlyList<SelectListItem> CustomerOptions { get; set; } = Array.Empty<SelectListItem>();
        public IReadOnlyList<SelectListItem> UserOptions { get; set; } = Array.Empty<SelectListItem>();
    }

    public class SalesOrderListItemViewModel
    {
        public int Id { get; set; }
        public string SalesOrderNumber { get; set; } = string.Empty;
        public string? Subject { get; set; }
        public DateOnly OrderDate { get; set; }
        public DateTime CreatedAt { get; set; }
        public decimal Subtotal { get; set; }
        public decimal GrandTotal { get; set; }
        public string CurrencySymbol { get; set; } = "Rs";
        public SalesOrderStatus Status { get; set; }
        public bool InvoiceCreated { get; set; }
        public string CustomerName { get; set; } = string.Empty;
        public string CustomerCode { get; set; } = string.Empty;
        public string? OrganizationName { get; set; }
        public string QuotationNumber { get; set; } = string.Empty;
        public string SourceType { get; set; } = "Direct";
        public string OpportunityName { get; set; } = string.Empty;
        public string? Owner { get; set; }
        public bool CanEdit { get; set; }
        public string InvoiceState => InvoiceCreated ? "Created" : Status == SalesOrderStatus.ReadyForInvoice ? "Ready for Invoice" : "Not Created";
    }

    public class SalesOrderFormViewModel
    {
        public int? Id { get; set; }
        public string? SalesOrderNumber { get; set; }
        public int? SalesQuotationId { get; set; }
        public int? OpportunityId { get; set; }
        [Required] public int CustomerId { get; set; }
        public int? AssignedToUserId { get; set; }
        [Required] public DateOnly OrderDate { get; set; } = DateOnly.FromDateTime(DateTime.UtcNow);
        public DateOnly? ServiceStartDate { get; set; }
        public DateOnly? ServiceEndDate { get; set; }
        public SalesOrderStatus Status { get; set; } = SalesOrderStatus.Draft;
        [MaxLength(80)] public string? CustomerPurchaseOrderNumber { get; set; }
        [MaxLength(80)] public string? CustomerReference { get; set; }
        [MaxLength(1000)] public string? RevisionReason { get; set; }
        [MaxLength(180)] public string? Subject { get; set; }
        [MaxLength(2000)] public string? TermsAndConditions { get; set; }
        [MaxLength(1500)] public string? CustomerNotes { get; set; }
        [MaxLength(2000)] public string? InternalNotes { get; set; }
        public string Currency { get; set; } = "PKR";
        public string CurrencySymbol { get; set; } = "Rs";
        public decimal Subtotal { get; set; }
        public decimal DiscountTotal { get; set; }
        public decimal TaxTotal { get; set; }
        public decimal GrandTotal { get; set; }
        public string? QuotationLabel { get; set; }
        public string SourceType { get; set; } = "Direct";
        public string? OpportunityLabel { get; set; }
        public string? CustomerLabel { get; set; }
        public List<SalesOrderItemViewModel> Items { get; set; } = new();
        public IReadOnlyList<SelectListItem> UserOptions { get; set; } = Array.Empty<SelectListItem>();
        public IReadOnlyList<SelectListItem> CustomerOptions { get; set; } = Array.Empty<SelectListItem>();
        public IReadOnlyList<SelectListItem> OpportunityOptions { get; set; } = Array.Empty<SelectListItem>();
        public IReadOnlyList<SelectListItem> SportOptions { get; set; } = Array.Empty<SelectListItem>();
        public IReadOnlyList<SelectListItem> FacilityOptions { get; set; } = Array.Empty<SelectListItem>();
        public IReadOnlyList<SelectListItem> CourtOptions { get; set; } = Array.Empty<SelectListItem>();
        public IReadOnlyList<SelectListItem> MembershipPlanOptions { get; set; } = Array.Empty<SelectListItem>();
        public bool IsEditable { get; set; } = true;
    }

    public class SalesOrderItemViewModel
    {
        public int? Id { get; set; }
        public int LineNumber { get; set; }
        public SalesItemType ItemType { get; set; } = SalesItemType.CustomService;
        public int? SportId { get; set; }
        public int? FacilityId { get; set; }
        public int? CourtId { get; set; }
        public int? MembershipPlanId { get; set; }
        [Required, MaxLength(300)] public string Description { get; set; } = string.Empty;
        [Range(0.01, 999999)] public decimal Quantity { get; set; } = 1;
        [Required, MaxLength(30)] public string UnitOfMeasure { get; set; } = "Unit";
        [Range(0, 999999999)] public decimal UnitPrice { get; set; }
        [Range(0, 100)] public decimal DiscountPercentage { get; set; }
        [Range(0, 100)] public decimal TaxPercentage { get; set; }
        public decimal DiscountAmount { get; set; }
        public decimal TaxAmount { get; set; }
        public decimal LineSubtotal { get; set; }
        public decimal LineTotal { get; set; }
        public DateOnly? ServiceDate { get; set; }
        public TimeOnly? StartTime { get; set; }
        public TimeOnly? EndTime { get; set; }
        public int DisplayOrder { get; set; }
    }

    public class SalesOrderDetailsViewModel
    {
        public SalesOrder SalesOrder { get; set; } = null!;
        public bool CanEdit { get; set; }
        public bool CanManageLifecycle { get; set; }
        public bool CanCancel { get; set; }
        public bool IsReadOnly { get; set; }
        public IReadOnlyList<DocumentApproval> ApprovalHistory { get; set; } = Array.Empty<DocumentApproval>();
        public IReadOnlyList<DocumentRevision> RevisionHistory { get; set; } = Array.Empty<DocumentRevision>();
    }

    public class SalesOrderPreviewViewModel
    {
        public SalesOrder SalesOrder { get; set; } = null!;
        public SystemSettings Settings { get; set; } = new();
    }
}
