using GameHub.Models.Enums;

namespace GameHub.Models.ViewModels
{
    public class PublicCourtListItemViewModel
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Code { get; set; } = string.Empty;
        public string SportName { get; set; } = string.Empty;
        public string SportCode { get; set; } = string.Empty;
        public string FacilityName { get; set; } = string.Empty;
        public string FacilityType { get; set; } = string.Empty;
        public int Capacity { get; set; }
        public string? ImageUrl { get; set; }
        public CourtStatus Status { get; set; }
        public int DefaultDurationMinutes { get; set; }
        public decimal? StartingPrice { get; set; }
    }
}
