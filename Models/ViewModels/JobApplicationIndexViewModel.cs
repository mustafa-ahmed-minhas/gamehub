using GameHub.Models.Entities;

namespace GameHub.Models.ViewModels
{
    public class JobApplicationIndexViewModel
    {
        public int TotalApplications { get; set; }

        public int NewApplications { get; set; }

        public int InProcess { get; set; }

        public int Hired { get; set; }

        /// <summary>
        /// CVs and supporting documents contain sensitive applicant data and are only
        /// downloadable by HR management roles, who can also create applications and
        /// postings. Viewer accounts see the lists only.
        /// </summary>
        public bool CanManage { get; set; }

        public List<JobApplication> Applications { get; set; } = new();
    }
}