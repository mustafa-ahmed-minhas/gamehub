using GameHub.Models.Enums;

namespace GameHub.Models.ViewModels
{
    public class CustomerIndexViewModel
    {
        public IReadOnlyList<CustomerListItemViewModel> Customers { get; set; } = Array.Empty<CustomerListItemViewModel>();
        public string? Search { get; set; }
        public CustomerType? CustomerType { get; set; }
        public CustomerSource? CustomerSource { get; set; }
        public string? Status { get; set; }
        public string? Membership { get; set; }
        public string? Blacklist { get; set; }
        public string? City { get; set; }
        public string SortBy { get; set; } = "newest";
        public string SortDirection { get; set; } = "desc";
        public int CurrentPage { get; set; }
        public int PageSize { get; set; }
        public int TotalRecords { get; set; }
        public int TotalPages { get; set; }
        public int TotalCustomers { get; set; }
        public int ActiveCustomers { get; set; }
        public int Members { get; set; }
        public int VipCustomers { get; set; }
        public int BlacklistedCustomers { get; set; }
        public decimal OutstandingBalance { get; set; }
        public IReadOnlyList<string> Cities { get; set; } = Array.Empty<string>();
        public bool CanManage { get; set; }
    }
}
