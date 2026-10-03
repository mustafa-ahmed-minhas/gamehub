using System.ComponentModel.DataAnnotations;
using GameHub.Models.Enums;

namespace GameHub.Models.Entities
{
    public class Court
    {
        public int Id { get; set; }

        [Required, MaxLength(100)]
        public string Name { get; set; } = string.Empty;

        [Required, MaxLength(30)]
        public string Code { get; set; } = string.Empty;

        public int SportId { get; set; }
        public int FacilityId { get; set; }
        public CourtStatus Status { get; set; }

        [MaxLength(300)]
        public string? Description { get; set; }

        public int Capacity { get; set; }

        [MaxLength(300)]
        public string? ImageUrl { get; set; }

        [MaxLength(500)]
        public string? Notes { get; set; }

        public bool IsActive { get; set; } = true;
        public int DisplayOrder { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }

        public Sport Sport { get; set; } = null!;
        public Facility Facility { get; set; } = null!;
        public ICollection<CourtPricing> Pricings { get; set; } = new List<CourtPricing>();
        public ICollection<CourtSchedule> Schedules { get; set; } = new List<CourtSchedule>();
    }
}
