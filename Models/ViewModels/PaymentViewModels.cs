using System.ComponentModel.DataAnnotations;
using GameHub.Models.Entities;
using GameHub.Models.Enums;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace GameHub.Models.ViewModels
{
    public class PaymentIndexViewModel
    {
        public IReadOnlyList<PaymentListItemViewModel> Items { get; set; } = Array.Empty<PaymentListItemViewModel>();
        public string? Search { get; set; }
        public InvoicePaymentMethod? Method { get; set; }
        public PaymentRecordStatus? Status { get; set; }
        public string SortBy { get; set; } = "newest";
        public int CurrentPage { get; set; } = 1;
        public int PageSize { get; set; } = 10;
        public int TotalRecords { get; set; }
        public int TotalPages { get; set; } = 1;
        public bool CanRecord { get; set; }
    }

    public class PaymentListItemViewModel
    {
        public int Id { get; set; }
        public string PaymentNumber { get; set; } = string.Empty;
        public string InvoiceNumber { get; set; } = string.Empty;
        public string CustomerName { get; set; } = string.Empty;
        public string CustomerCode { get; set; } = string.Empty;
        public DateOnly PaymentDate { get; set; }
        public InvoicePaymentMethod PaymentMethod { get; set; }
        public decimal Amount { get; set; }
        public string CurrencySymbol { get; set; } = "Rs";
        public PaymentRecordStatus Status { get; set; }
        public string ReceivedBy { get; set; } = string.Empty;
    }

    public class PaymentFormViewModel
    {
        public int? Id { get; set; }
        [Required] public int InvoiceId { get; set; }
        [Required] public DateOnly PaymentDate { get; set; } = DateOnly.FromDateTime(DateTime.UtcNow);
        [Range(typeof(decimal), "0.01", "999999999")] public decimal Amount { get; set; }
        [Required] public InvoicePaymentMethod PaymentMethod { get; set; }
        [MaxLength(120)] public string? ReferenceNumber { get; set; }
        [MaxLength(1500)] public string? Notes { get; set; }
        public byte[]? RowVersion { get; set; }
        public string? InvoiceNumber { get; set; }
        public string? CustomerName { get; set; }
        public decimal OutstandingAmount { get; set; }
        public decimal InvoiceTotal { get; set; }
        public string CurrencySymbol { get; set; } = "Rs";
        public IReadOnlyList<SelectListItem> InvoiceOptions { get; set; } = Array.Empty<SelectListItem>();
    }

    public class PaymentDetailsViewModel
    {
        public Payment Payment { get; set; } = null!;
        public IReadOnlyList<PaymentAllocation> Allocations { get; set; } = Array.Empty<PaymentAllocation>();
        public bool CanRecord { get; set; }
    }

    public class PaymentReceiptViewModel
    {
        public Payment Payment { get; set; } = null!;
        public SystemSettings Settings { get; set; } = new();
    }
}
