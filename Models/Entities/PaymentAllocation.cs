using System.ComponentModel.DataAnnotations;

namespace GameHub.Models.Entities
{
    public class PaymentAllocation
    {
        public int Id { get; set; }
        public int PaymentId { get; set; }
        public int InvoiceId { get; set; }
        public decimal Amount { get; set; }
        public DateTime CreatedAt { get; set; }

        public Payment Payment { get; set; } = null!;
        public SalesInvoice Invoice { get; set; } = null!;
    }
}
