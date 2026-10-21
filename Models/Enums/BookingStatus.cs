using System.ComponentModel.DataAnnotations;

namespace GameHub.Models.Enums
{
    public enum BookingStatus
    {
        Draft,
        Pending,
        [Display(Name = "Pending Approval")]
        PendingApproval,
        Confirmed,
        [Display(Name = "Checked In")]
        CheckedIn,
        [Display(Name = "In Progress")]
        InProgress,
        Completed,
        Cancelled,
        [Display(Name = "No Show")]
        NoShow,
        Rescheduled
    }
}
