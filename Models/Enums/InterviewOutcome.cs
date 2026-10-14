using System.ComponentModel.DataAnnotations;

namespace GameHub.Models.Enums
{
    public enum InterviewOutcome
    {
        Pending,

        Passed,

        Failed,

        [Display(Name = "On Hold")]
        OnHold
    }
}
