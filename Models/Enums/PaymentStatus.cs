using System.ComponentModel.DataAnnotations;

namespace GameHub.Models.Enums
{
    public enum PaymentStatus
    {
        Unpaid,
        [Display(Name = "Partially Paid")]
        PartiallyPaid,
        Paid,
        Refunded,
        Waived
    }
}
