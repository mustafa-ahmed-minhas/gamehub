using System.ComponentModel.DataAnnotations;
using GameHub.Models.Enums;

namespace GameHub.Models.ViewModels
{
    /// <summary>
    /// Create / edit model for scheduling an interview against a job application.
    /// </summary>
    public class InterviewFormViewModel : IValidatableObject
    {
        public int? Id { get; set; }

        [Range(1, int.MaxValue, ErrorMessage = "Select a valid application.")]
        public int JobApplicationId { get; set; }

        // Read-only context shown at the top of the form.
        public string ApplicationNumber { get; set; } = string.Empty;
        public string CandidateName { get; set; } = string.Empty;
        public string JobTitle { get; set; } = string.Empty;

        [Required(ErrorMessage = "Interview Date is required.")]
        [DataType(DataType.Date)]
        public DateOnly? InterviewDate { get; set; }

        [Required(ErrorMessage = "Interview Time is required.")]
        [DataType(DataType.Time)]
        public TimeOnly? InterviewTime { get; set; }

        public InterviewType InterviewType { get; set; } = InterviewType.InPerson;

        [Required(ErrorMessage = "Interviewer is required.")]
        [MaxLength(150, ErrorMessage = "Interviewer cannot exceed 150 characters.")]
        [RegularExpression(@"^[A-Za-z][A-Za-z\s'.,()\-/&]*$", ErrorMessage = "Interviewer must contain letters only (spaces and name punctuation are allowed).")]
        public string Interviewer { get; set; } = string.Empty;

        [MaxLength(250, ErrorMessage = "Meeting location / link cannot exceed 250 characters.")]
        public string? Location { get; set; }

        [MaxLength(2000, ErrorMessage = "Notes cannot exceed 2,000 characters.")]
        public string? Notes { get; set; }

        public InterviewStatus Status { get; set; } = InterviewStatus.Scheduled;

        public InterviewOutcome Outcome { get; set; } = InterviewOutcome.Pending;

        [MaxLength(2000, ErrorMessage = "Feedback cannot exceed 2,000 characters.")]
        public string? Feedback { get; set; }

        public bool IsEditMode => Id.HasValue;

        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            var now = DateTime.UtcNow;
            var today = DateOnly.FromDateTime(now);
            var nowTime = TimeOnly.FromDateTime(now);

            if (!IsEditMode && InterviewDate.HasValue && InterviewDate.Value < today)
            {
                yield return new ValidationResult(
                    "Interview Date cannot be in the past.",
                    new[] { nameof(InterviewDate) });
            }

            if (!IsEditMode
                && InterviewDate.HasValue
                && InterviewTime.HasValue
                && InterviewDate.Value == today
                && InterviewTime.Value <= nowTime)
            {
                yield return new ValidationResult(
                    "Interview time has already passed today. Choose a later time.",
                    new[] { nameof(InterviewTime) });
            }

            if (InterviewDate.HasValue
                && InterviewTime.HasValue
                && (Status == InterviewStatus.Completed || Status == InterviewStatus.NoShow)
                && (InterviewDate.Value > today
                    || (InterviewDate.Value == today && InterviewTime.Value > nowTime)))
            {
                yield return new ValidationResult(
                    "An interview cannot be marked completed or no-show before its scheduled time.",
                    new[] { nameof(Status) });
            }

            if (Status == InterviewStatus.Completed && Outcome == InterviewOutcome.Pending)
            {
                yield return new ValidationResult(
                    "Select an interview outcome before marking the interview as completed.",
                    new[] { nameof(Outcome) });
            }
        }
    }
}
