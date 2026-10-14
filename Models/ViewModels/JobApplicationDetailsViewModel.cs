using GameHub.Models.Entities;

namespace GameHub.Models.ViewModels
{
    /// <summary>
    /// Candidate / application details page (Recruitment Management).
    /// </summary>
    public class JobApplicationDetailsViewModel
    {
        public JobApplication Application { get; set; } = null!;

        public string Initials { get; set; } = string.Empty;

        public int? CalculatedAge { get; set; }

        public bool CanManage { get; set; }

        public List<Interview> Interviews { get; set; } = new();

        public Interview? NextInterview { get; set; }
    }
}
