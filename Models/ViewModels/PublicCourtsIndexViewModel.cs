namespace GameHub.Models.ViewModels
{
    public class PublicCourtsIndexViewModel
    {
        public IReadOnlyList<PublicCourtListItemViewModel> Courts { get; set; } = Array.Empty<PublicCourtListItemViewModel>();
        public IReadOnlyList<string> Sports { get; set; } = Array.Empty<string>();
        public IReadOnlyList<string> Facilities { get; set; } = Array.Empty<string>();
        public string? Search { get; set; }
        public string? Sport { get; set; }
        public string? Facility { get; set; }
        public string? FacilityType { get; set; }
        public string CurrencySymbol { get; set; } = "Rs";
    }
}
