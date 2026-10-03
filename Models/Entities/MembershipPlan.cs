using GameHub.Models.Enums;
using System.ComponentModel.DataAnnotations;

namespace GameHub.Models.Entities
{
    public class MembershipPlan
    {
        public int Id { get; set; }

        [Required, MaxLength(100)]
        public string Name { get; set; } = string.Empty;

        [Required, MaxLength(20)]
        public string Code { get; set; } = string.Empty;

        [MaxLength(500)]
        public string? Description { get; set; }

        public MembershipPlanType PlanType { get; set; }
        public int DurationMonths { get; set; }
        public decimal JoiningFee { get; set; }
        public decimal RenewalFee { get; set; }
        public decimal DiscountPercentage { get; set; }
        public int IncludedBookingHours { get; set; }
        public int PriorityBookingDays { get; set; }
        public bool AllowPeakHours { get; set; }
        public bool AllowOffPeakHours { get; set; }

        [MaxLength(300)]
        public string? AllowedSportCodes { get; set; }

        [MaxLength(1000)]
        public string? Benefits { get; set; }

        public int DisplayOrder { get; set; }
        public bool IsActive { get; set; } = true;
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }

        public ICollection<CustomerMembership> CustomerMemberships { get; set; } = new List<CustomerMembership>();
    }
}
