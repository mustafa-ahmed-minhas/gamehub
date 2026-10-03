namespace GameHub.Models.ViewModels
{
    public class BookingCalendarEventViewModel
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Start { get; set; } = string.Empty;
        public string End { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string Customer { get; set; } = string.Empty;
        public string Court { get; set; } = string.Empty;
        public string Sport { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public string Color { get; set; } = "#c99a50";
    }
}
