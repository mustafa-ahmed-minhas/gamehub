using System.ComponentModel.DataAnnotations;
using GameHub.Models.Enums;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace GameHub.Models.ViewModels
{
    public class CourtFormViewModel
    {
        public int? Id { get; set; }
        [Required, MaxLength(100)]
        [RegularExpression(@"^[A-Za-z0-9 \-]+$", ErrorMessage = "Court name can contain letters, numbers, spaces and hyphens only.")]
        public string Name { get; set; } = string.Empty;
        [Required, MaxLength(30)]
        [RegularExpression(@"^[A-Za-z0-9\-]+$", ErrorMessage = "Court code can contain letters, numbers and hyphens only.")]
        public string Code { get; set; } = string.Empty;
        [Range(1, int.MaxValue, ErrorMessage = "Sport is required.")] public int SportId { get; set; }
        [Range(1, int.MaxValue, ErrorMessage = "Facility is required.")] public int FacilityId { get; set; }
        public CourtStatus Status { get; set; }
        [MaxLength(300)] public string? Description { get; set; }
        [Range(1, 9999)] public int Capacity { get; set; } = 4;
        [MaxLength(300)] public string? ImageUrl { get; set; }
        [MaxLength(500)] public string? Notes { get; set; }
        public bool IsActive { get; set; } = true;
        [Range(0, 9999)] public int DisplayOrder { get; set; }
        public IReadOnlyList<SelectListItem> SportOptions { get; set; } = Array.Empty<SelectListItem>();
        public IReadOnlyList<SelectListItem> FacilityOptions { get; set; } = Array.Empty<SelectListItem>();
        public bool IsEditMode => Id.HasValue;
    }
}
