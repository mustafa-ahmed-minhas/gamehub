using System.ComponentModel.DataAnnotations;
using GameHub.Models.Enums;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace GameHub.Models.ViewModels
{
    public class JobPostingFormViewModel : IValidatableObject
    {
        public int? Id { get; set; }

        // System generated on save; displayed read-only in the form.
        public string? ReferenceNumber { get; set; }

        [Required(ErrorMessage = "Job Title is required.")]
        [MinLength(3, ErrorMessage = "Job Title must be at least 3 characters.")]
        [MaxLength(100, ErrorMessage = "Job Title cannot exceed 100 characters.")]
        public string JobTitle { get; set; } = string.Empty;

        [Required(ErrorMessage = "Department is required.")]
        [MaxLength(80)]
        public string Department { get; set; } = string.Empty;

        public EmploymentType EmploymentType { get; set; } = EmploymentType.FullTime;

        public WorkplaceType WorkplaceType { get; set; } = WorkplaceType.OnSite;

        [Required(ErrorMessage = "Job Location is required.")]
        [MaxLength(150, ErrorMessage = "Job Location cannot exceed 150 characters.")]
        public string JobLocation { get; set; } = string.Empty;

        [Required(ErrorMessage = "Number of Vacancies is required.")]
        [Range(1, 100, ErrorMessage = "Vacancies must be between 1 and 100.")]
        public int? Vacancies { get; set; }

        [Range(0, 999999999.99, ErrorMessage = "Minimum Salary must be between 0 and 999,999,999.99.")]
        public decimal? MinimumSalary { get; set; }

        [Range(0, 999999999.99, ErrorMessage = "Maximum Salary must be between 0 and 999,999,999.99.")]
        public decimal? MaximumSalary { get; set; }

        [Required(ErrorMessage = "Currency is required.")]
        [MaxLength(10)]
        public string Currency { get; set; } = "PKR";

        [Required(ErrorMessage = "Experience Required is required.")]
        [MaxLength(80)]
        public string ExperienceRequired { get; set; } = string.Empty;

        [Required(ErrorMessage = "Minimum Education is required.")]
        [MaxLength(120)]
        public string MinimumEducation { get; set; } = string.Empty;

        [Required(ErrorMessage = "Job Description is required.")]
        [MinLength(50, ErrorMessage = "Job Description must be at least 50 characters.")]
        [MaxLength(5000, ErrorMessage = "Job Description cannot exceed 5,000 characters.")]
        public string JobDescription { get; set; } = string.Empty;

        [Required(ErrorMessage = "Key Responsibilities are required.")]
        [MinLength(30, ErrorMessage = "Key Responsibilities must be at least 30 characters.")]
        [MaxLength(3000, ErrorMessage = "Key Responsibilities cannot exceed 3,000 characters.")]
        public string KeyResponsibilities { get; set; } = string.Empty;

        public List<string> RequiredSkills { get; set; } = new();

        [Required(ErrorMessage = "Qualifications / Requirements are required.")]
        [MinLength(20, ErrorMessage = "Qualifications must be at least 20 characters.")]
        [MaxLength(3000, ErrorMessage = "Qualifications cannot exceed 3,000 characters.")]
        public string Qualifications { get; set; } = string.Empty;

        [Required(ErrorMessage = "Application Deadline is required.")]
        [DataType(DataType.Date)]
        public DateOnly? ApplicationDeadline { get; set; }

        public JobPostingStatus Status { get; set; } = JobPostingStatus.Draft;

        public int? HiringManagerUserId { get; set; }

        [MaxLength(2000, ErrorMessage = "Benefits cannot exceed 2,000 characters.")]
        public string? Benefits { get; set; }

        public List<string> ScreeningQuestions { get; set; } = new();

        public bool IsEditMode => Id.HasValue;

        public List<SelectListItem> Departments { get; set; } = new();

        public List<SelectListItem> Currencies { get; set; } = new();

        public List<SelectListItem> ExperienceLevels { get; set; } = new();

        public List<SelectListItem> EducationLevels { get; set; } = new();

        public List<SelectListItem> HiringManagers { get; set; } = new();

        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            if (MinimumSalary.HasValue != MaximumSalary.HasValue)
            {
                yield return new ValidationResult(
                    "Provide both Minimum Salary and Maximum Salary, or leave both blank.",
                    new[] { MinimumSalary.HasValue ? nameof(MaximumSalary) : nameof(MinimumSalary) });
            }

            if (MinimumSalary.HasValue && MaximumSalary.HasValue && MaximumSalary.Value < MinimumSalary.Value)
            {
                yield return new ValidationResult(
                    "Maximum Salary must be greater than or equal to Minimum Salary.",
                    new[] { nameof(MaximumSalary) });
            }

            if (ApplicationDeadline.HasValue && ApplicationDeadline.Value <= DateOnly.FromDateTime(DateTime.UtcNow))
            {
                yield return new ValidationResult(
                    "Application Deadline must be a future date.",
                    new[] { nameof(ApplicationDeadline) });
            }

            var skills = (RequiredSkills ?? new List<string>())
                .Where(skill => !string.IsNullOrWhiteSpace(skill))
                .Select(skill => skill.Trim())
                .ToList();

            if (skills.Count == 0)
            {
                yield return new ValidationResult(
                    "Add at least one required skill.",
                    new[] { nameof(RequiredSkills) });
            }
            else
            {
                var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var skill in skills)
                {
                    if (skill.Length > 80)
                    {
                        yield return new ValidationResult(
                            $"Skill \"{skill}\" cannot exceed 80 characters.",
                            new[] { nameof(RequiredSkills) });
                    }

                    if (!seen.Add(skill))
                    {
                        yield return new ValidationResult(
                            $"Skill \"{skill}\" is listed more than once.",
                            new[] { nameof(RequiredSkills) });
                    }
                }
            }

            var questions = (ScreeningQuestions ?? new List<string>())
                .Where(question => !string.IsNullOrWhiteSpace(question))
                .Select(question => question.Trim())
                .ToList();

            foreach (var question in questions)
            {
                if (question.Length > 500)
                {
                    yield return new ValidationResult(
                        "Each screening question cannot exceed 500 characters.",
                        new[] { nameof(ScreeningQuestions) });
                }
            }
        }
    }
}