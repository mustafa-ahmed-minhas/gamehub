using System.ComponentModel.DataAnnotations;

namespace GameHub.Models.Enums
{
    public enum InterviewType
    {
        [Display(Name = "In Person")]
        InPerson,

        Phone,

        [Display(Name = "Video Call")]
        VideoCall,

        Panel
    }
}
