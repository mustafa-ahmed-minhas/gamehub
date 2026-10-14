using GameHub.Models.Entities;

namespace GameHub.Models.ViewModels
{
    public class JobPostingIndexViewModel
    {
        public int TotalPostings { get; set; }

        public int OpenPostings { get; set; }

        public int TotalApplications { get; set; }

        public int ExpiringSoon { get; set; }

        /// <summary>
        /// HR management roles can create postings and manage the pipeline;
        /// viewer accounts see the lists only.
        /// </summary>
        public bool CanManage { get; set; }

        public List<JobPosting> Postings { get; set; } = new();
    }
}