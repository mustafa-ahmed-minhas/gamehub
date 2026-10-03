using System.ComponentModel.DataAnnotations;

namespace GameHub.Models.Entities
{
    public class Sport
    {
        public int Id { get; set; }

        [Required, MaxLength(80)]
        public string Name { get; set; } = string.Empty;

        [Required, MaxLength(20)]
        public string Code { get; set; } = string.Empty;

        [MaxLength(300)]
        public string? Description { get; set; }

        [MaxLength(80)]
        public string? IconClass { get; set; }

        [MaxLength(300)]
        public string? ImageUrl { get; set; }

        [MaxLength(20)]
        public string? AccentColor { get; set; }

        public int DefaultDurationMinutes { get; set; }
        public int DisplayOrder { get; set; }
        public bool IsActive { get; set; } = true;
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }

        public ICollection<Court> Courts { get; set; } = new List<Court>();
    }
}
