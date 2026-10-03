namespace GameHub.Models.ViewModels
{
    public class MyAccountViewModel
    {
        public string FirstName { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string Initials { get; set; } = string.Empty;
        public string CustomerCode { get; set; } = string.Empty;
        public string? Email { get; set; }
        public bool EmailVerified { get; set; }
        public string MembershipStatus { get; set; } = string.Empty;
        public int LoyaltyPoints { get; set; }
        public string? PreferredSports { get; set; }
        public string? ProfileImageUrl { get; set; }
        public int UpcomingBookings { get; set; }
        public int CompletedBookings { get; set; }
        public int CancelledBookings { get; set; }
        public bool HasActiveMembership { get; set; }
        public string? MembershipNumber { get; set; }
        public string? MembershipPlanName { get; set; }
        public DateOnly? MembershipExpiryDate { get; set; }
        public int MembershipDaysRemaining { get; set; }
        public bool HasActiveCheckout { get; set; }
        public int? CheckoutCourtId { get; set; }
        public string? CheckoutCourtName { get; set; }
        public string? CheckoutDateTime { get; set; }
        public DateTime? CheckoutExpiresAtUtc { get; set; }
    }
}
