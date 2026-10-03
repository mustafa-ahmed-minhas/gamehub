using System.ComponentModel.DataAnnotations;

namespace GameHub.Models.Enums
{
    public enum InvoicePaymentMethod
    {
        Cash,
        [Display(Name = "Credit Card")] CreditCard,
        [Display(Name = "Debit Card")] DebitCard,
        [Display(Name = "Bank Transfer")] BankTransfer,
        [Display(Name = "Online Payment")] OnlinePayment,
        Cheque,
        Other
    }

    public enum PaymentRecordStatus
    {
        Recorded
    }
}
