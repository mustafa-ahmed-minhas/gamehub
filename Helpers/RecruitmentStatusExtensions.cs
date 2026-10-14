using GameHub.Models.Enums;

namespace GameHub.Helpers
{
    /// <summary>
    /// Maps recruitment pipeline values (application / interview status and
    /// interview outcome) to the HR badge tone classes used across the
    /// Recruitment Management screens (see hr.css).
    /// </summary>
    public static class RecruitmentStatusExtensions
    {
        public static string GetBadgeClass(this JobApplicationStatus status) => status switch
        {
            JobApplicationStatus.Screening => "screening",
            JobApplicationStatus.Shortlisted => "medium",
            JobApplicationStatus.Interview => "interview",
            JobApplicationStatus.Offer => "offer",
            JobApplicationStatus.Hired => "hired",
            JobApplicationStatus.Rejected => "high",
            _ => "new"
        };

        public static string GetBadgeClass(this JobPostingStatus status) => status switch
        {
            JobPostingStatus.Open => "open",
            JobPostingStatus.Closed => "closed",
            _ => "draft"
        };

        public static string GetBadgeClass(this InterviewStatus status) => status switch
        {
            InterviewStatus.Scheduled => "interview",
            InterviewStatus.Completed => "offer",
            InterviewStatus.Cancelled => "high",
            InterviewStatus.NoShow => "high",
            _ => "new"
        };

        public static string GetBadgeClass(this InterviewOutcome outcome) => outcome switch
        {
            InterviewOutcome.Passed => "hired",
            InterviewOutcome.Failed => "high",
            InterviewOutcome.OnHold => "medium",
            _ => "assigned"
        };

        /// <summary>
        /// Offer badge tone (Screen 3). Reuses the shared HR palette so offers
        /// read consistently alongside the recruitment pipeline.
        /// </summary>
        public static string GetBadgeClass(this JobOfferStatus status) => status switch
        {
            JobOfferStatus.Draft => "draft",
            JobOfferStatus.PendingAcceptance => "medium",
            JobOfferStatus.Accepted => "open",
            JobOfferStatus.Rejected => "high",
            JobOfferStatus.Withdrawn => "closed",
            JobOfferStatus.Expired => "high",
            JobOfferStatus.Hired => "hired",
            _ => "new"
        };

        public static string GetBadgeClass(this SalaryFrequency frequency) => frequency switch
        {
            SalaryFrequency.Annual => "assigned",
            SalaryFrequency.Hourly => "review",
            _ => "interview"
        };
    }
}
