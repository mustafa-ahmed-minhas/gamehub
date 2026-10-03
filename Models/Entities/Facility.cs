using System.ComponentModel.DataAnnotations;
using GameHub.Models.Enums;

namespace GameHub.Models.Entities
{
    public class Facility
    {
        public int Id { get; set; }

        [Required, MaxLength(100)]
        public string Name { get; set; } = string.Empty;

        [Required, MaxLength(20)]
        public string Code { get; set; } = string.Empty;

        [MaxLength(300)]
        public string? Description { get; set; }

        public FacilityType Type { get; set; }

        [MaxLength(150)]
        public string? LocationLabel { get; set; }

        public bool IsActive { get; set; } = true;
        public int DisplayOrder { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }

        public ICollection<Court> Courts { get; set; } = new List<Court>();
    }
}
