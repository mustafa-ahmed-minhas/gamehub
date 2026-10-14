using System.ComponentModel.DataAnnotations;

namespace GameHub.Models.Enums
{
    public enum InterviewStatus
    {
        Scheduled,

        Completed,

        Cancelled,

        [Display(Name = "No Show")]
        NoShow
    }
}
