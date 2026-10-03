using System.ComponentModel.DataAnnotations;

namespace GameHub.Models.ViewModels
{
    public class SportFormViewModel
    {
        public int? Id { get; set; }
        [Required, MaxLength(80)] public string Name { get; set; } = string.Empty;
        [Required, MaxLength(20)] public string Code { get; set; } = string.Empty;
        [MaxLength(300)] public string? Description { get; set; }
        [MaxLength(80)] public string? IconClass { get; set; }
        [MaxLength(300)] public string? ImageUrl { get; set; }
        [MaxLength(20)] public string? AccentColor { get; set; }
        [Range(1, 1440)] public int DefaultDurationMinutes { get; set; } = 60;
        [Range(0, 9999)] public int DisplayOrder { get; set; }
        public bool IsActive { get; set; } = true;
        public bool IsEditMode => Id.HasValue;
    }
}
