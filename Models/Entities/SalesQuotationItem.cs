using System.ComponentModel.DataAnnotations;
using GameHub.Models.Enums;

namespace GameHub.Models.Entities
{
    public class SalesQuotationItem
    {
        public int Id { get; set; }
        public int SalesQuotationId { get; set; }
        public int LineNumber { get; set; }
        public SalesItemType ItemType { get; set; }
        public int? SportId { get; set; }
        public int? FacilityId { get; set; }
        public int? CourtId { get; set; }
        public int? MembershipPlanId { get; set; }
        [Required, MaxLength(300)] public string Description { get; set; } = string.Empty;
        public decimal Quantity { get; set; }
        [Required, MaxLength(30)] public string UnitOfMeasure { get; set; } = "Unit";
        public decimal UnitPrice { get; set; }
        public decimal DiscountPercentage { get; set; }
        public decimal DiscountAmount { get; set; }
        public decimal TaxPercentage { get; set; }
        public decimal TaxAmount { get; set; }
        public decimal LineSubtotal { get; set; }
        public decimal LineTotal { get; set; }
        public int DisplayOrder { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }

        public SalesQuotation SalesQuotation { get; set; } = null!;
        public Sport? Sport { get; set; }
        public Facility? Facility { get; set; }
        public Court? Court { get; set; }
        public MembershipPlan? MembershipPlan { get; set; }
    }
}
