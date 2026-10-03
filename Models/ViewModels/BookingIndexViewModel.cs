using GameHub.Models.Enums;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace GameHub.Models.ViewModels
{
    public class BookingIndexViewModel
    {
        public string View { get; set; } = "list";
        public string? Search { get; set; }
        public int? SportId { get; set; }
        public int? FacilityId { get; set; }
        public int? CourtId { get; set; }
        public int? CustomerId { get; set; }
        public BookingStatus? Status { get; set; }
        public PaymentStatus? PaymentStatus { get; set; }
        public BookingSource? Source { get; set; }
        public DateOnly? DateFrom { get; set; }
        public DateOnly? DateTo { get; set; }
        public string SortBy { get; set; } = "date_desc";
        public string SortDirection { get; set; } = "desc";
        public int CurrentPage { get; set; }
        public int PageSize { get; set; }
        public int TotalRecords { get; set; }
        public int TotalPages { get; set; }
        public int TodaysBookings { get; set; }
        public int Confirmed { get; set; }
        public int Pending { get; set; }
        public int InProgress { get; set; }
        public int CompletedToday { get; set; }
        public int Cancelled { get; set; }
        public decimal TodaysRevenue { get; set; }
        public decimal CourtOccupancy { get; set; }
        public bool CanManage { get; set; }
        public bool CanOperate { get; set; }
        public string CurrencySymbol { get; set; } = "Rs";
        public IReadOnlyList<BookingListItemViewModel> Bookings { get; set; } = Array.Empty<BookingListItemViewModel>();
        public IReadOnlyList<BookingCalendarEventViewModel> CalendarEvents { get; set; } = Array.Empty<BookingCalendarEventViewModel>();
        public List<SelectListItem> Sports { get; set; } = new();
        public List<SelectListItem> Facilities { get; set; } = new();
        public List<SelectListItem> Courts { get; set; } = new();
    }
}
