using System.ComponentModel.DataAnnotations;
using GameHub.Models.Entities;
using GameHub.Models.Enums;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace GameHub.Models.ViewModels
{
    public class SalesDashboardViewModel
    {
        public string ActiveTab { get; set; } = "overview";
        public SalesQuotationIndexViewModel Quotations { get; set; } = new();
        public int DraftCount { get; set; }
        public int AwaitingReviewCount { get; set; }
        public int SentCount { get; set; }
        public int AcceptedCount { get; set; }
        public int RejectedCount { get; set; }
        public int ExpiringSoonCount { get; set; }
        public decimal TotalQuotedValue { get; set; }
        public decimal AcceptedValue { get; set; }
        public string CurrencySymbol { get; set; } = "Rs";
        public bool CanCreate { get; set; }
        public bool CanManageLifecycle { get; set; }
        public bool IsReadOnly { get; set; }
    }

    public class SalesQuotationIndexViewModel
    {
        public IReadOnlyList<SalesQuotationListItemViewModel> Items { get; set; } = Array.Empty<SalesQuotationListItemViewModel>();
        public string? Search { get; set; }
        public SalesQuotationStatus? Status { get; set; }
        public string? ValidityState { get; set; }
        public string SortBy { get; set; } = "newest";
        public string SortDirection { get; set; } = "desc";
        public int CurrentPage { get; set; } = 1;
        public int PageSize { get; set; } = 10;
        public int TotalRecords { get; set; }
        public int TotalPages { get; set; } = 1;
        public IReadOnlyList<SelectListItem> CustomerOptions { get; set; } = Array.Empty<SelectListItem>();
        public IReadOnlyList<SelectListItem> OpportunityOptions { get; set; } = Array.Empty<SelectListItem>();
        public IReadOnlyList<SelectListItem> UserOptions { get; set; } = Array.Empty<SelectListItem>();
    }

    public class SalesQuotationListItemViewModel
    {
        public int Id { get; set; }
        public string QuotationNumber { get; set; } = string.Empty;
        public string? Subject { get; set; }
        public DateOnly QuotationDate { get; set; }
        public DateOnly ValidUntil { get; set; }
        public SalesQuotationStatus Status { get; set; }
        public decimal GrandTotal { get; set; }
        public string CurrencySymbol { get; set; } = "Rs";
        public string CustomerName { get; set; } = string.Empty;
        public string CustomerCode { get; set; } = string.Empty;
        public string? OrganizationName { get; set; }
        public string OpportunityNumber { get; set; } = string.Empty;
        public string OpportunityName { get; set; } = string.Empty;
        public OpportunityStage OpportunityStage { get; set; }
        public string? Owner { get; set; }
        public bool SalesOrderCreated { get; set; }
        public bool IsReadyForSalesOrder => Status == SalesQuotationStatus.Accepted && !SalesOrderCreated;
        public string ValidityState => Status is SalesQuotationStatus.Accepted or SalesQuotationStatus.Rejected or SalesQuotationStatus.Cancelled ? "Closed" : ValidUntil < DateOnly.FromDateTime(DateTime.UtcNow) ? "Expired" : ValidUntil <= DateOnly.FromDateTime(DateTime.UtcNow.AddDays(2)) ? "Expiring Soon" : "Valid";
    }

    public class SalesQuotationFormViewModel
    {
        public int? Id { get; set; }
        public string? QuotationNumber { get; set; }
        [Required] public int OpportunityId { get; set; }
        [Required] public int CustomerId { get; set; }
        public int? AssignedToUserId { get; set; }
        [Required] public DateOnly QuotationDate { get; set; } = DateOnly.FromDateTime(DateTime.UtcNow);
        [Required] public DateOnly ValidUntil { get; set; } = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(7));
        public SalesQuotationStatus Status { get; set; } = SalesQuotationStatus.Draft;
        [MaxLength(180)] public string? Subject { get; set; }
        [MaxLength(1500)] public string? Introduction { get; set; }
        [MaxLength(2000)] public string? TermsAndConditions { get; set; }
        [MaxLength(1500)] public string? CustomerNotes { get; set; }
        [MaxLength(2000)] public string? InternalNotes { get; set; }
        [MaxLength(80)] public string? CustomerReference { get; set; }
        [MaxLength(1000)] public string? RevisionReason { get; set; }
        [Range(0, 999999999)] public decimal ManualDiscountAmount { get; set; }
        public string Currency { get; set; } = "PKR";
        public string CurrencySymbol { get; set; } = "Rs";
        public decimal Subtotal { get; set; }
        public decimal LineDiscountTotal { get; set; }
        public decimal DiscountTotal { get; set; }
        public decimal TaxTotal { get; set; }
        public decimal GrandTotal { get; set; }
        public string? OpportunityLabel { get; set; }
        public string? CustomerLabel { get; set; }
        public List<SalesQuotationItemViewModel> Items { get; set; } = new();
        public IReadOnlyList<SelectListItem> UserOptions { get; set; } = Array.Empty<SelectListItem>();
        public IReadOnlyList<SelectListItem> SportOptions { get; set; } = Array.Empty<SelectListItem>();
        public IReadOnlyList<SelectListItem> FacilityOptions { get; set; } = Array.Empty<SelectListItem>();
        public IReadOnlyList<SelectListItem> CourtOptions { get; set; } = Array.Empty<SelectListItem>();
        public IReadOnlyList<SelectListItem> MembershipPlanOptions { get; set; } = Array.Empty<SelectListItem>();
        public IReadOnlyList<SelectListItem> PricingOptions { get; set; } = Array.Empty<SelectListItem>();
    }

    public class SalesQuotationItemViewModel
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
        public int DisplayOrder { get; set; }
    }

    public class SalesQuotationDetailsViewModel
    {
        public SalesQuotation Quotation { get; set; } = null!;
        public bool CanEdit { get; set; }
        public bool CanManageLifecycle { get; set; }
        public bool IsReadOnly { get; set; }
        public IReadOnlyList<DocumentApproval> ApprovalHistory { get; set; } = Array.Empty<DocumentApproval>();
        public IReadOnlyList<DocumentRevision> RevisionHistory { get; set; } = Array.Empty<DocumentRevision>();
    }

    public class SalesQuotationPreviewViewModel
    {
        public SalesQuotation Quotation { get; set; } = null!;
        public SystemSettings Settings { get; set; } = new();
        public bool IsCustomerView { get; set; }
    }
}
