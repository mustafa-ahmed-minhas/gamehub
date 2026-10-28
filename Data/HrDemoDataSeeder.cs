using GameHub.Models.Entities;
using GameHub.Models.Enums;
using GameHub.Services.Interfaces;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;

namespace GameHub.Data
{
    /// <summary>
    /// Creates a complete, interconnected HR demo dataset for development
    /// review. Records are written through the normal entity model, respect the
    /// module's field rules and workflow conventions, and are identifiable by
    /// the @gamehub-demo.com email domain. The seed is skipped once demo
    /// employees exist, so repeated starts never duplicate records.
    /// </summary>
    public static class HrDemoDataSeeder
    {
        private const string DemoDomain = "gamehub-demo.com";
        private const string CvFolder = "cvs";

        public static async Task SeedAsync(ApplicationDbContext db, IServiceProvider services, CancellationToken cancellationToken = default)
        {
            if (await db.Employees.AnyAsync(employee => employee.Email.EndsWith("@" + DemoDomain), cancellationToken))
            {
                return;
            }

            var now = DateTime.UtcNow;
            var context = new SeedContext
            {
                Db = db,
                Now = now,
                Today = DateOnly.FromDateTime(now),
                HrUserId = await ResolveUserIdAsync(db, UserRole.Admin, cancellationToken)
                           ?? await ResolveUserIdAsync(db, UserRole.SuperAdmin, cancellationToken),
                ApproverId = await ResolveUserIdAsync(db, UserRole.SuperAdmin, cancellationToken),
                EmployeeNumbers = new Sequence(await NextSequenceAsync(db.Employees.Select(employee => employee.EmployeeNumber), "EMP-", cancellationToken)),
                PostingNumbers = new Sequence(await NextSequenceAsync(db.JobPostings.Select(posting => posting.ReferenceNumber), "JOB-", cancellationToken)),
                ApplicationNumbers = new Sequence(await NextSequenceAsync(db.JobApplications.Select(application => application.ApplicationNumber), "APP-", cancellationToken)),
                OfferNumbers = new Sequence(await NextSequenceAsync(db.JobOffers.Select(offer => offer.ReferenceNumber), "OFF-", cancellationToken)),
                CaseNumbers = new Sequence(await NextSequenceAsync(db.PerformanceCases.Select(performanceCase => performanceCase.CaseNumber), "PC-", cancellationToken)),
                TerminationNumbers = new Sequence(await NextSequenceAsync(db.TerminationRequests.Select(request => request.ReferenceNumber), "TRM-", cancellationToken))
            };
            context.ApproverId ??= context.HrUserId;

            await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

            await SeedStaffAsync(context, cancellationToken);
            var postings = await SeedPostingsAsync(context, cancellationToken);
            var applications = await SeedApplicationsAsync(context, postings, cancellationToken);
            await SeedInterviewsAsync(context, applications, cancellationToken);
            await SeedOffersAsync(context, applications, cancellationToken);
            await SeedPerformanceAsync(context, cancellationToken);
            await SeedTerminationsAsync(context, cancellationToken);

            await transaction.CommitAsync(cancellationToken);

            await WriteCvFilesAsync(context, services, cancellationToken);
        }

        // ------------------------------------------------------------------
        // Employees
        // ------------------------------------------------------------------

        private static async Task SeedStaffAsync(SeedContext context, CancellationToken cancellationToken)
        {
            var managerSeeds = new[]
            {
                new EmployeeSeed("Imran", "Qureshi", "Operations", "Head of Operations", EmploymentType.FullTime, EmploymentStatus.Active, 44, 84, 385000m, Gender.Male, "Lahore"),
                new EmployeeSeed("Hira", "Sheikh", "Human Resources", "HR Manager", EmploymentType.FullTime, EmploymentStatus.Active, 37, 66, 320000m, Gender.Female, "Lahore"),
                new EmployeeSeed("Faisal", "Rehman", "Finance", "Finance Controller", EmploymentType.FullTime, EmploymentStatus.Active, 41, 72, 350000m, Gender.Male, "Islamabad"),
                new EmployeeSeed("Zara", "Haider", "Sales & Marketing", "Marketing Manager", EmploymentType.FullTime, EmploymentStatus.Active, 34, 48, 280000m, Gender.Female, "Lahore"),
                new EmployeeSeed("Bilal", "Farooq", "Sports & Recreation", "Sports Program Manager", EmploymentType.FullTime, EmploymentStatus.Active, 39, 60, 300000m, Gender.Male, "Lahore"),
                new EmployeeSeed("Sana", "Mirza", "Customer Experience", "Customer Experience Lead", EmploymentType.FullTime, EmploymentStatus.Active, 32, 42, 240000m, Gender.Female, "Karachi")
            };

            var managers = new List<Employee>();
            for (var index = 0; index < managerSeeds.Length; index++)
            {
                managers.Add(BuildEmployee(context, managerSeeds[index], index, managerId: null));
            }

            context.Db.Employees.AddRange(managers);
            await context.Db.SaveChangesAsync(cancellationToken);

            for (var index = 1; index < managers.Count; index++)
            {
                managers[index].ReportingManagerId = managers[0].Id;
            }

            await context.Db.SaveChangesAsync(cancellationToken);

            context.Managers.AddRange(managers);
            context.Employees.AddRange(managers);
            foreach (var manager in managers)
            {
                context.ManagerByDepartment[manager.Department] = manager;
            }

            var staffSeeds = new[]
            {
                new EmployeeSeed("Ali", "Nawaz", "Operations", "Facility Supervisor", EmploymentType.FullTime, EmploymentStatus.Active, 30, 30, 120000m, Gender.Male, "Lahore"),
                new EmployeeSeed("Nimra", "Butt", "Operations", "Front Desk Executive", EmploymentType.FullTime, EmploymentStatus.Active, 26, 18, 85000m, Gender.Female, "Lahore"),
                new EmployeeSeed("Usman", "Malik", "Operations", "Security Supervisor", EmploymentType.FullTime, EmploymentStatus.OnLeave, 36, 40, 95000m, Gender.Male, "Karachi"),
                new EmployeeSeed("Mehwish", "Raza", "Operations", "Housekeeping Supervisor", EmploymentType.PartTime, EmploymentStatus.Active, 29, 22, 55000m, Gender.Female, "Lahore"),
                new EmployeeSeed("Hamza", "Yousaf", "Finance", "Accounts Executive", EmploymentType.FullTime, EmploymentStatus.Active, 28, 26, 110000m, Gender.Male, "Islamabad"),
                new EmployeeSeed("Iqra", "Sattar", "Finance", "Payroll Officer", EmploymentType.FullTime, EmploymentStatus.Active, 31, 34, 125000m, Gender.Female, "Lahore"),
                new EmployeeSeed("Adnan", "Akhtar", "Finance", "Billing Analyst", EmploymentType.Contract, EmploymentStatus.Active, 27, 14, 95000m, Gender.Male, "Rawalpindi"),
                new EmployeeSeed("Fatima", "Abbas", "Sales & Marketing", "Corporate Sales Executive", EmploymentType.FullTime, EmploymentStatus.Active, 27, 20, 105000m, Gender.Female, "Lahore"),
                new EmployeeSeed("Danish", "Qureshi", "Sales & Marketing", "Digital Marketing Specialist", EmploymentType.FullTime, EmploymentStatus.Active, 25, 16, 90000m, Gender.Male, "Islamabad"),
                new EmployeeSeed("Mahnoor", "Baig", "Sales & Marketing", "Membership Sales Officer", EmploymentType.PartTime, EmploymentStatus.Active, 24, 9, 60000m, Gender.Female, "Karachi"),
                new EmployeeSeed("Talha", "Khan", "Sports & Recreation", "Senior Cricket Coach", EmploymentType.FullTime, EmploymentStatus.Active, 35, 52, 165000m, Gender.Male, "Lahore"),
                new EmployeeSeed("Anum", "Shah", "Sports & Recreation", "Badminton Coach", EmploymentType.PartTime, EmploymentStatus.Active, 29, 28, 70000m, Gender.Female, "Karachi"),
                new EmployeeSeed("Shahzaib", "Haider", "Sports & Recreation", "Fitness Trainer", EmploymentType.Contract, EmploymentStatus.Active, 26, 12, 75000m, Gender.Male, "Lahore"),
                new EmployeeSeed("Kiran", "Aslam", "Customer Experience", "Customer Support Officer", EmploymentType.FullTime, EmploymentStatus.Active, 25, 15, 70000m, Gender.Female, "Karachi"),
                new EmployeeSeed("Saad", "Jameel", "Customer Experience", "Booking Coordinator", EmploymentType.FullTime, EmploymentStatus.OnLeave, 30, 32, 92000m, Gender.Male, "Lahore"),
                new EmployeeSeed("Laiba", "Siddiqui", "Human Resources", "HR Officer", EmploymentType.FullTime, EmploymentStatus.Active, 27, 21, 95000m, Gender.Female, "Lahore"),
                new EmployeeSeed("Waqar", "Abbas", "Human Resources", "Recruitment Officer", EmploymentType.FullTime, EmploymentStatus.Active, 33, 38, 115000m, Gender.Male, "Islamabad"),
                new EmployeeSeed("Rehan", "Sattar", "IT", "IT Support Engineer", EmploymentType.FullTime, EmploymentStatus.Active, 29, 24, 130000m, Gender.Male, "Lahore"),
                new EmployeeSeed("Ayesha", "Farooq", "IT", "Systems Administrator", EmploymentType.FullTime, EmploymentStatus.Active, 32, 36, 155000m, Gender.Female, "Karachi"),
                new EmployeeSeed("Umer", "Iqbal", "Facilities", "Maintenance Engineer", EmploymentType.FullTime, EmploymentStatus.Active, 58, 96, 145000m, Gender.Male, "Lahore"),
                new EmployeeSeed("Sidra", "Nawaz", "Food & Beverage", "Cafeteria Supervisor", EmploymentType.FullTime, EmploymentStatus.Active, 56, 84, 88000m, Gender.Female, "Lahore")
            };

            var staff = new List<Employee>();
            for (var index = 0; index < staffSeeds.Length; index++)
            {
                var seed = staffSeeds[index];
                var manager = context.ManagerByDepartment.TryGetValue(seed.Department, out var departmentManager)
                    ? departmentManager
                    : managers[0];
                staff.Add(BuildEmployee(context, seed, managers.Count + index, manager.Id));
            }

            context.Db.Employees.AddRange(staff);
            await context.Db.SaveChangesAsync(cancellationToken);
            context.Employees.AddRange(staff);
        }

        private static Employee BuildEmployee(SeedContext context, EmployeeSeed seed, int index, int? managerId)
        {
            var city = seed.City;
            var dateOfBirth = context.Today.AddYears(-seed.Age).AddDays(-(index * 17 % 250));
            var joiningDate = context.Today.AddMonths(-seed.TenureMonths).AddDays(-(index * 13 % 25));

            return new Employee
            {
                EmployeeNumber = $"EMP-{context.EmployeeNumbers.Take():000000}",
                FirstName = seed.FirstName,
                LastName = seed.LastName,
                Email = $"{Slug(seed.FirstName)}.{Slug(seed.LastName)}@{DemoDomain}",
                PhoneNumber = context.NextStaffPhone(),
                NationalIdNumber = context.NextStaffNationalId(),
                DateOfBirth = dateOfBirth,
                Gender = seed.Gender,
                Department = seed.Department,
                Designation = seed.Designation,
                EmploymentType = seed.EmploymentType,
                EmploymentStatus = seed.EmploymentStatus,
                JoiningDate = joiningDate,
                ReportingManagerId = managerId,
                EmploymentAddress = $"{EmergencyAddresses[index % EmergencyAddresses.Length]}, {city}",
                EmergencyContactName = EmergencyContacts[index % EmergencyContacts.Length],
                EmergencyContactNumber = context.NextStaffPhone(),
                BasicSalary = seed.Salary,
                IsActive = true,
                CreatedAt = At(joiningDate, 9, 0),
                CreatedByUserId = context.HrUserId
            };
        }

        // ------------------------------------------------------------------
        // Job postings
        // ------------------------------------------------------------------

        private static async Task<List<JobPosting>> SeedPostingsAsync(SeedContext context, CancellationToken cancellationToken)
        {
            var seeds = PostingSeeds;
            var postings = new List<JobPosting>();
            var openIndex = 0;
            var draftIndex = 0;
            var closedIndex = 0;

            for (var index = 0; index < seeds.Length; index++)
            {
                var seed = seeds[index];
                DateOnly createdDate;
                DateOnly deadline;

                switch (seed.Status)
                {
                    case JobPostingStatus.Open:
                        createdDate = context.Today.AddDays(-(45 + openIndex * 4));
                        deadline = context.Today.AddDays(5 + openIndex * 4);
                        openIndex++;
                        break;
                    case JobPostingStatus.Draft:
                        createdDate = context.Today.AddDays(-(5 + draftIndex * 3));
                        deadline = context.Today.AddDays(30 + draftIndex * 6);
                        draftIndex++;
                        break;
                    default:
                        createdDate = context.Today.AddDays(-(210 + closedIndex * 6));
                        deadline = context.Today.AddDays(-(100 + closedIndex * 5));
                        closedIndex++;
                        break;
                }

                var posting = new JobPosting
                {
                    ReferenceNumber = $"JOB-{context.PostingNumbers.Take():000000}",
                    JobTitle = seed.Title,
                    Department = seed.Department,
                    EmploymentType = seed.EmploymentType,
                    WorkplaceType = seed.WorkplaceType,
                    JobLocation = seed.Location,
                    Vacancies = seed.Vacancies,
                    MinimumSalary = seed.MinimumSalary,
                    MaximumSalary = seed.MaximumSalary,
                    Currency = "PKR",
                    ExperienceRequired = seed.Experience,
                    MinimumEducation = seed.Education,
                    JobDescription = $"{seed.Summary} The role sits within our {seed.Department} team in {seed.Location} and reports to the department lead. We are looking for someone who takes ownership, communicates clearly, and delivers a consistently high standard of service.",
                    KeyResponsibilities = seed.Responsibilities,
                    Qualifications = seed.Qualifications,
                    ApplicationDeadline = deadline,
                    Status = seed.Status,
                    HiringManagerUserId = context.HrUserId,
                    Benefits = Benefits[index % Benefits.Length],
                    IsActive = true,
                    CreatedAt = At(createdDate, 9, 30),
                    CreatedByUserId = context.HrUserId
                };

                foreach (var skill in seed.Skills)
                {
                    posting.Skills.Add(new JobPostingSkill { Skill = skill });
                }

                foreach (var question in seed.Questions)
                {
                    posting.ScreeningQuestions.Add(new JobPostingScreeningQuestion { Question = question });
                }

                postings.Add(posting);
            }

            context.Db.JobPostings.AddRange(postings);
            await context.Db.SaveChangesAsync(cancellationToken);
            return postings;
        }

        // ------------------------------------------------------------------
        // Applications
        // ------------------------------------------------------------------

        private static async Task<List<JobApplication>> SeedApplicationsAsync(SeedContext context, List<JobPosting> postings, CancellationToken cancellationToken)
        {
            var closedPostings = postings.Where(posting => posting.Status == JobPostingStatus.Closed).ToList();
            var openPostings = postings.Where(posting => posting.Status == JobPostingStatus.Open).ToList();
            var candidates = BuildCandidates(context);
            var applications = new List<JobApplication>();
            var candidateIndex = 0;

            for (var index = 0; index < 8; index++)
            {
                var posting = closedPostings[index % closedPostings.Count];
                var applicationDate = posting.ApplicationDeadline.AddDays(-(12 + index % 4));
                applications.Add(BuildApplication(context, candidates[candidateIndex++], posting, JobApplicationStatus.Hired, applicationDate));
            }

            for (var index = 0; index < 6; index++)
            {
                var posting = closedPostings[index % closedPostings.Count];
                var applicationDate = posting.ApplicationDeadline.AddDays(-(8 + index % 3));
                applications.Add(BuildApplication(context, candidates[candidateIndex++], posting, JobApplicationStatus.Rejected, applicationDate));
            }

            var openStatuses = BuildOpenApplicationStatuses();
            for (var index = 0; index < openStatuses.Count; index++)
            {
                var posting = openPostings[index % openPostings.Count];
                var createdDate = DateOnly.FromDateTime(posting.CreatedAt);
                var maxDaysAgo = Math.Min(86, context.Today.DayNumber - createdDate.DayNumber - 3);
                var minDaysAgo = Math.Min(20, maxDaysAgo);
                var span = Math.Max(1, maxDaysAgo - minDaysAgo + 1);
                var daysAgo = minDaysAgo + (index * 7 % span);
                var applicationDate = context.Today.AddDays(-daysAgo);

                applications.Add(BuildApplication(context, candidates[candidateIndex++], posting, openStatuses[index], applicationDate));
            }

            context.Db.JobApplications.AddRange(applications);
            await context.Db.SaveChangesAsync(cancellationToken);
            return applications;
        }

        private static JobApplication BuildApplication(SeedContext context, Candidate candidate, JobPosting posting, JobApplicationStatus status, DateOnly applicationDate)
        {
            var expectedSalary = posting.MinimumSalary.HasValue && posting.MaximumSalary.HasValue
                ? Math.Round((posting.MinimumSalary.Value + (posting.MaximumSalary.Value - posting.MinimumSalary.Value) * 0.7m) / 1000m) * 1000m
                : 90000m;

            var updatedDate = status switch
            {
                JobApplicationStatus.Submitted => applicationDate.AddDays(1),
                JobApplicationStatus.Screening => applicationDate.AddDays(2),
                JobApplicationStatus.Shortlisted => applicationDate.AddDays(4),
                JobApplicationStatus.Interview => applicationDate.AddDays(6),
                JobApplicationStatus.Offer => applicationDate.AddDays(9),
                JobApplicationStatus.Hired => applicationDate.AddDays(12),
                JobApplicationStatus.Rejected => applicationDate.AddDays(3),
                _ => applicationDate.AddDays(1)
            };
            if (updatedDate > context.Today)
            {
                updatedDate = context.Today;
            }

            var application = new JobApplication
            {
                ApplicationNumber = $"APP-{context.ApplicationNumbers.Take():000000}",
                JobPostingId = posting.Id,
                JobPosting = posting,
                FirstName = candidate.FirstName,
                LastName = candidate.LastName,
                Email = candidate.Email,
                Phone = candidate.Phone,
                NationalIdNumber = candidate.NationalIdNumber,
                DateOfBirth = candidate.DateOfBirth,
                Gender = candidate.Gender,
                City = candidate.City,
                Country = "Pakistan",
                CurrentAddress = candidate.Address,
                ApplicationDate = applicationDate,
                Status = status,
                ExperienceYears = candidate.ExperienceYears,
                HighestEducation = candidate.Education,
                CurrentEmployer = candidate.CurrentEmployer,
                CurrentJobTitle = candidate.CurrentJobTitle,
                ExpectedSalary = expectedSalary,
                NoticePeriod = candidate.NoticePeriod,
                CoverLetter = $"I am applying for the {posting.JobTitle} position at GameHub Arena. {candidate.Summary} I am based in {candidate.City} and can join after {candidate.NoticePeriod.ToLowerInvariant()}. I would welcome the opportunity to contribute to the team.",
                LinkedInUrl = candidate.LinkedInUrl,
                PortfolioUrl = candidate.PortfolioUrl,
                CvFileName = candidate.CvStoredName,
                CvOriginalFileName = $"{candidate.FirstName}_{candidate.LastName}_CV.pdf",
                CvContentType = "application/pdf",
                CvFileSize = 120000 + candidate.Index * 317,
                IsActive = true,
                CreatedAt = At(applicationDate, 10, 0),
                UpdatedAt = At(updatedDate, 15, 0),
                CreatedByUserId = context.HrUserId
            };

            context.CvFiles.Add((candidate.CvStoredName, "Curriculum Vitae", new[]
            {
                $"Candidate: {candidate.FirstName} {candidate.LastName}",
                $"Email: {candidate.Email}",
                $"Phone: {candidate.Phone}",
                $"Location: {candidate.City}, Pakistan",
                $"Applying for: {posting.JobTitle} ({posting.Department})",
                $"Experience: {candidate.ExperienceYears} years",
                $"Education: {candidate.Education}",
                $"Current role: {candidate.CurrentJobTitle ?? "Not applicable"}",
                $"Profile: {candidate.Summary}",
                "References available on request."
            }));

            return application;
        }

        private static List<JobApplicationStatus> BuildOpenApplicationStatuses()
        {
            var pattern = new[]
            {
                JobApplicationStatus.Offer,
                JobApplicationStatus.Submitted,
                JobApplicationStatus.Screening,
                JobApplicationStatus.Offer,
                JobApplicationStatus.Shortlisted,
                JobApplicationStatus.Interview,
                JobApplicationStatus.Offer,
                JobApplicationStatus.Submitted,
                JobApplicationStatus.Shortlisted,
                JobApplicationStatus.Offer,
                JobApplicationStatus.Screening,
                JobApplicationStatus.Interview
            };

            var statuses = new List<JobApplicationStatus>();
            for (var repeat = 0; repeat < 3; repeat++)
            {
                statuses.AddRange(pattern);
            }

            statuses.AddRange(Enumerable.Repeat(JobApplicationStatus.Offer, 8));
            statuses.AddRange(Enumerable.Repeat(JobApplicationStatus.Submitted, 2));

            var random = new Random(20261028);
            for (var index = statuses.Count - 1; index > 0; index--)
            {
                var swap = random.Next(index + 1);
                (statuses[index], statuses[swap]) = (statuses[swap], statuses[index]);
            }

            return statuses;
        }

        private static List<Candidate> BuildCandidates(SeedContext context)
        {
            var maleFirstNames = new[]
            {
                "Ali", "Hamza", "Bilal", "Usman", "Zain", "Fahad", "Saad", "Danish", "Farhan", "Talha",
                "Umer", "Hassan", "Rizwan", "Shahzaib", "Adnan", "Kashif", "Waqar", "Imran", "Salman", "Arsalan",
                "Junaid", "Nabeel", "Rehan", "Shayan", "Ahsan", "Haris", "Muneeb", "Owais", "Sameer", "Taimoor"
            };

            var femaleFirstNames = new[]
            {
                "Ayesha", "Fatima", "Hira", "Sana", "Maria", "Zara", "Rabia", "Mehwish", "Iqra", "Nimra",
                "Sadia", "Amna", "Laiba", "Noor", "Mahnoor", "Sidra", "Alina", "Bushra", "Kiran", "Warda",
                "Anum", "Hania", "Areeba", "Eman", "Afshan", "Ghazal", "Huma", "Javeria", "Komal", "Lubna"
            };

            var lastNames = new[]
            {
                "Siddiqui", "Tariq", "Iqbal", "Aslam", "Jameel", "Khan", "Malik", "Sheikh", "Butt", "Chaudhry",
                "Qureshi", "Raza", "Abbas", "Haider", "Farooq", "Rehman", "Akhtar", "Nawaz", "Baig", "Mirza",
                "Shah", "Yousaf", "Sattar", "Hashmi", "Bano", "Riaz", "Zafar", "Sultan", "Anwar", "Kamran"
            };

            var cities = new[] { "Lahore", "Karachi", "Islamabad", "Rawalpindi", "Faisalabad", "Multan", "Peshawar", "Quetta" };
            var areas = new[] { "DHA Phase 5", "Gulberg III", "Model Town", "Bahria Town", "Clifton Block 4", "F-11 Markaz", "Satellite Town", "Cantt" };
            var educationLevels = new[] { "Bachelor's Degree", "Master's Degree", "Intermediate / A Levels", "Diploma" };
            var noticePeriods = new[] { "Immediate", "2 Weeks", "1 Month", "2 Months" };
            var employers = new[] { "Arena Sports Club", "FitZone Gym", "Metro Hospitality", "Summit Retail", "Blue Wave Events", "City Bank", "Crescent Foods", "Horizon Facilities" };
            var jobTitles = new[] { "Operations Assistant", "Front Desk Officer", "Accounts Assistant", "Sales Executive", "Coach", "Customer Support Officer", "Facilities Technician", "Marketing Associate" };
            var summaries = new[]
            {
                "I bring hands-on experience in fast-paced sports and leisure operations, with a strong focus on service quality and team coordination.",
                "My background combines customer-facing service with accurate reporting and dependable shift coverage.",
                "I have supported high-volume front-desk and booking operations and am comfortable with point-of-sale and scheduling systems.",
                "I have led small teams through busy event weekends and improved handover discipline across shifts.",
                "I am known for clear communication, calm problem-solving, and a detail-oriented approach to daily operations.",
                "I have worked across finance and billing processes, including reconciliations, invoicing, and month-end support.",
                "My experience covers coaching, player development, and safe session planning for groups of different skill levels.",
                "I enjoy building strong member relationships and turning enquiries into repeat bookings.",
                "I have maintained facilities and equipment to a high standard while keeping safety checks and logs up to date.",
                "I am eager to grow with a professional sports organisation and contribute to a positive guest experience."
            };

            var usedEmails = new HashSet<string>(context.Employees.Select(employee => employee.Email), StringComparer.OrdinalIgnoreCase);
            var candidates = new List<Candidate>();
            var firstNames = maleFirstNames.Concat(femaleFirstNames).ToList();

            for (var index = 0; index < firstNames.Count; index++)
            {
                var firstName = firstNames[index];
                var gender = index < maleFirstNames.Length ? Gender.Male : Gender.Female;
                var lastName = lastNames[(index * 7 + 3) % lastNames.Length];
                var email = $"{Slug(firstName)}.{Slug(lastName)}@{DemoDomain}";
                var attempt = 1;
                while (!usedEmails.Add(email))
                {
                    email = $"{Slug(firstName)}.{Slug(lastName)}{attempt++}@{DemoDomain}";
                }

                var city = cities[index % cities.Length];
                var experienceYears = index % 14;
                candidates.Add(new Candidate
                {
                    Index = index,
                    FirstName = firstName,
                    LastName = lastName,
                    Gender = gender,
                    Email = email,
                    Phone = context.NextCandidatePhone(),
                    NationalIdNumber = context.NextCandidateNationalId(),
                    DateOfBirth = context.Today.AddYears(-(22 + index * 3 % 19)).AddDays(-(index * 11 % 300)),
                    City = city,
                    Address = $"House {12 + index}, Street {4 + index % 9}, {areas[index % areas.Length]}, {city}",
                    ExperienceYears = experienceYears,
                    Education = educationLevels[index % educationLevels.Length],
                    CurrentEmployer = experienceYears == 0 ? null : employers[index % employers.Length],
                    CurrentJobTitle = experienceYears == 0 ? null : jobTitles[index % jobTitles.Length],
                    NoticePeriod = noticePeriods[index % noticePeriods.Length],
                    Summary = summaries[index % summaries.Length],
                    LinkedInUrl = index % 3 == 0 ? $"https://www.linkedin.com/in/{Slug(firstName)}-{Slug(lastName)}-demo" : null,
                    PortfolioUrl = index % 7 == 0 ? $"https://portfolio.example.com/{Slug(firstName)}-{Slug(lastName)}" : null,
                    CvStoredName = $"{Guid.NewGuid():N}.pdf"
                });
            }

            return candidates;
        }

        // ------------------------------------------------------------------
        // Interviews
        // ------------------------------------------------------------------

        private static async Task SeedInterviewsAsync(SeedContext context, List<JobApplication> applications, CancellationToken cancellationToken)
        {
            var interviews = new List<Interview>();
            var interviewApps = applications.Where(application => application.Status == JobApplicationStatus.Interview).ToList();
            var draftOfferApps = interviewApps.Take(4).ToHashSet();
            var shortlistedApps = applications.Where(application => application.Status == JobApplicationStatus.Shortlisted).ToList();
            var completedCounter = 0;

            foreach (var application in applications)
            {
                if (application.Status == JobApplicationStatus.Hired)
                {
                    AddCompletedInterview(context, interviews, application, application.ApplicationDate.AddDays(9), InterviewOutcome.Passed, completedCounter++);
                    AddCompletedInterview(context, interviews, application, application.ApplicationDate.AddDays(16), InterviewOutcome.Passed, completedCounter++);
                }
                else if (application.Status == JobApplicationStatus.Offer)
                {
                    AddCompletedInterview(context, interviews, application, application.ApplicationDate.AddDays(11), InterviewOutcome.Passed, completedCounter++);
                    if (completedCounter % 3 == 0)
                    {
                        AddCompletedInterview(context, interviews, application, application.ApplicationDate.AddDays(18), InterviewOutcome.Passed, completedCounter++);
                    }
                }
                else if (application.Status == JobApplicationStatus.Interview)
                {
                    if (draftOfferApps.Contains(application))
                    {
                        AddCompletedInterview(context, interviews, application, application.ApplicationDate.AddDays(10), InterviewOutcome.Passed, completedCounter++);
                    }
                }
            }

            var interviewWithoutOffer = interviewApps.Where(application => !draftOfferApps.Contains(application)).Take(2).ToList();
            if (interviewWithoutOffer.Count > 0)
            {
                AddScheduledInterview(context, interviews, interviewWithoutOffer[0], context.Today.AddDays(4), 11, 0);
            }
            if (interviewWithoutOffer.Count > 1)
            {
                AddScheduledInterview(context, interviews, interviewWithoutOffer[1], context.Today.AddDays(-3), 15, 30);
            }

            for (var index = 0; index < shortlistedApps.Count; index++)
            {
                var application = shortlistedApps[index];
                switch (index % 4)
                {
                    case 0:
                    case 1:
                        AddScheduledInterview(context, interviews, application, context.Today.AddDays(2 + index * 2), 10 + index % 5, 0);
                        break;
                    case 2:
                        AddCompletedInterview(context, interviews, application, application.ApplicationDate.AddDays(12), InterviewOutcome.Passed, completedCounter++);
                        break;
                    case 3:
                        interviews.Add(new Interview
                        {
                            JobApplicationId = application.Id,
                            JobApplication = application,
                            InterviewDate = context.Today.AddDays(-(4 + index % 3)),
                            InterviewTime = new TimeOnly(14, 0),
                            InterviewType = InterviewType.InPerson,
                            Interviewer = InterviewerFor(context, application),
                            Location = "GameHub Arena, Lahore",
                            Notes = "Candidate did not attend the scheduled panel interview.",
                            Status = InterviewStatus.NoShow,
                            Outcome = InterviewOutcome.Pending,
                            IsActive = true,
                            CreatedAt = At(context.Today.AddDays(-(8 + index % 3)), 9, 0),
                            CreatedByUserId = context.HrUserId
                        });
                        break;
                }
            }

            context.Db.Interviews.AddRange(interviews);
            await context.Db.SaveChangesAsync(cancellationToken);
        }

        private static void AddCompletedInterview(SeedContext context, List<Interview> interviews, JobApplication application, DateOnly interviewDate, InterviewOutcome outcome, int index)
        {
            if (interviewDate > context.Today)
            {
                interviewDate = context.Today.AddDays(-1);
            }

            var interviewType = (InterviewType)(index % 4);
            interviews.Add(new Interview
            {
                JobApplicationId = application.Id,
                JobApplication = application,
                InterviewDate = interviewDate,
                InterviewTime = new TimeOnly(10 + index % 6, index % 2 == 0 ? 0 : 30),
                InterviewType = interviewType,
                Interviewer = InterviewerFor(context, application),
                Location = interviewType == InterviewType.VideoCall
                    ? "Video call (Microsoft Teams)"
                    : interviewType == InterviewType.Phone
                        ? "Phone interview"
                        : "GameHub Arena, Lahore",
                Notes = "Structured competency interview covering role experience, service standards, and availability.",
                Status = InterviewStatus.Completed,
                Outcome = outcome,
                Feedback = outcome == InterviewOutcome.Passed
                    ? "Strong communication and relevant experience. The panel recommends progressing the candidate to the next stage."
                    : "The panel identified gaps in the required experience but encourages the candidate to apply for future openings.",
                IsActive = true,
                CreatedAt = At(interviewDate.AddDays(-3), 9, 30),
                CreatedByUserId = context.HrUserId
            });
        }

        private static void AddScheduledInterview(SeedContext context, List<Interview> interviews, JobApplication application, DateOnly interviewDate, int hour, int minute)
        {
            interviews.Add(new Interview
            {
                JobApplicationId = application.Id,
                JobApplication = application,
                InterviewDate = interviewDate,
                InterviewTime = new TimeOnly(hour % 24, minute),
                InterviewType = interviewDate < context.Today ? InterviewType.InPerson : InterviewType.VideoCall,
                Interviewer = InterviewerFor(context, application),
                Location = interviewDate < context.Today ? "GameHub Arena, Lahore" : "Video call (Microsoft Teams)",
                Notes = interviewDate < context.Today
                    ? "Interview was scheduled but the outcome has not yet been recorded."
                    : "Competency-based interview with the hiring panel.",
                Status = InterviewStatus.Scheduled,
                Outcome = InterviewOutcome.Pending,
                IsActive = true,
                CreatedAt = At(context.Today.AddDays(-(2 + application.Id % 4)), 11, 0),
                CreatedByUserId = context.HrUserId
            });
        }

        private static string InterviewerFor(SeedContext context, JobApplication application)
        {
            var department = application.JobPosting?.Department ?? string.Empty;
            if (context.ManagerByDepartment.TryGetValue(department, out var manager))
            {
                return $"{manager.FirstName} {manager.LastName}";
            }

            return "Hira Sheikh";
        }

        // ------------------------------------------------------------------
        // Offers and hiring
        // ------------------------------------------------------------------

        private static async Task SeedOffersAsync(SeedContext context, List<JobApplication> applications, CancellationToken cancellationToken)
        {
            var offerApps = applications.Where(application => application.Status == JobApplicationStatus.Offer).ToList();
            var hiredApps = applications.Where(application => application.Status == JobApplicationStatus.Hired).ToList();
            var interviewApps = applications.Where(application => application.Status == JobApplicationStatus.Interview).ToList();
            var draftApps = interviewApps.Take(4).ToList();

            var offerStatuses = new[]
            {
                JobOfferStatus.Accepted, JobOfferStatus.PendingAcceptance, JobOfferStatus.Accepted, JobOfferStatus.Rejected,
                JobOfferStatus.Accepted, JobOfferStatus.PendingAcceptance, JobOfferStatus.Accepted, JobOfferStatus.Withdrawn,
                JobOfferStatus.Accepted, JobOfferStatus.PendingAcceptance, JobOfferStatus.Accepted, JobOfferStatus.Expired,
                JobOfferStatus.Accepted, JobOfferStatus.PendingAcceptance, JobOfferStatus.Accepted, JobOfferStatus.Rejected,
                JobOfferStatus.Accepted, JobOfferStatus.Accepted, JobOfferStatus.Accepted, JobOfferStatus.Accepted
            };

            var offers = new List<(JobOffer Offer, JobApplication Application)>();

            for (var index = 0; index < offerApps.Count; index++)
            {
                var application = offerApps[index];
                var status = offerStatuses[index % offerStatuses.Length];
                var offerDate = status switch
                {
                    JobOfferStatus.PendingAcceptance => context.Today.AddDays(-(1 + index % 3)),
                    JobOfferStatus.Accepted => context.Today.AddDays(-(4 + index % 10)),
                    JobOfferStatus.Rejected or JobOfferStatus.Withdrawn or JobOfferStatus.Expired => context.Today.AddDays(-(15 + index % 5)),
                    _ => context.Today.AddDays(-(2 + index % 5))
                };
                if (offerDate < application.ApplicationDate.AddDays(2))
                {
                    offerDate = application.ApplicationDate.AddDays(2);
                }

                DateOnly expiryDate;
                if (status == JobOfferStatus.PendingAcceptance)
                {
                    expiryDate = context.Today.AddDays(3 + index % 7);
                }
                else if (status == JobOfferStatus.Expired)
                {
                    expiryDate = context.Today.AddDays(-(5 + index % 5));
                }
                else
                {
                    expiryDate = offerDate.AddDays(10);
                }
                if (expiryDate <= offerDate)
                {
                    expiryDate = offerDate.AddDays(5);
                }

                var joiningDate = status switch
                {
                    JobOfferStatus.PendingAcceptance => context.Today.AddDays(10 + index % 15),
                    JobOfferStatus.Accepted => context.Today.AddDays(5 + index % 25),
                    JobOfferStatus.Rejected or JobOfferStatus.Withdrawn or JobOfferStatus.Expired => offerDate.AddDays(20),
                    _ => offerDate.AddDays(21)
                };

                offers.Add((BuildOffer(context, application, status, offerDate, expiryDate, joiningDate, index), application));
            }

            foreach (var application in draftApps)
            {
                var offerDate = context.Today.AddDays(-(2 + application.Id % 5));
                offers.Add((BuildOffer(context, application, JobOfferStatus.Draft, offerDate, offerDate.AddDays(12), offerDate.AddDays(24), application.Id), application));
            }

            for (var index = 0; index < hiredApps.Count; index++)
            {
                var application = hiredApps[index];
                var offerDate = application.ApplicationDate.AddDays(7);
                var joiningDate = offerDate.AddDays(14 + index % 5);
                offers.Add((BuildOffer(context, application, JobOfferStatus.Hired, offerDate, offerDate.AddDays(10), joiningDate, index), application));
            }

            var hiredOffers = offers.Where(entry => entry.Offer.Status == JobOfferStatus.Hired).ToList();
            var hiredEmployees = new List<(JobOffer Offer, Employee Employee)>();
            foreach (var (offer, application) in hiredOffers)
            {
                var employee = BuildHiredEmployee(context, application, offer);
                context.Db.Employees.Add(employee);
                context.Employees.Add(employee);
                hiredEmployees.Add((offer, employee));
            }

            await context.Db.SaveChangesAsync(cancellationToken);

            foreach (var (offer, employee) in hiredEmployees)
            {
                offer.EmployeeId = employee.Id;
            }

            context.Db.JobOffers.AddRange(offers.Select(entry => entry.Offer));
            await context.Db.SaveChangesAsync(cancellationToken);
        }

        private static JobOffer BuildOffer(SeedContext context, JobApplication application, JobOfferStatus status, DateOnly offerDate, DateOnly expiryDate, DateOnly joiningDate, int index)
        {
            var posting = application.JobPosting!;
            var salary = posting.MinimumSalary.HasValue && posting.MaximumSalary.HasValue
                ? Math.Round((posting.MinimumSalary.Value + (posting.MaximumSalary.Value - posting.MinimumSalary.Value) * (0.5m + 0.1m * (index % 5))) / 1000m) * 1000m
                : 95000m;
            if (posting.MaximumSalary.HasValue && salary > posting.MaximumSalary.Value)
            {
                salary = posting.MaximumSalary.Value;
            }

            var manager = context.ManagerByDepartment.TryGetValue(posting.Department, out var departmentManager)
                ? departmentManager
                : context.Managers[0];

            var offer = new JobOffer
            {
                ReferenceNumber = $"OFF-{context.OfferNumbers.Take()}",
                JobApplicationId = application.Id,
                JobApplication = application,
                Designation = posting.JobTitle,
                Department = posting.Department,
                EmploymentType = posting.EmploymentType,
                WorkLocation = posting.JobLocation,
                OfferedSalary = salary,
                Currency = "PKR",
                SalaryFrequency = SalaryFrequency.Monthly,
                OfferDate = offerDate,
                ExpiryDate = expiryDate,
                JoiningDate = joiningDate,
                ProbationPeriodMonths = posting.EmploymentType == EmploymentType.FullTime ? 3 : null,
                Benefits = Benefits[index % Benefits.Length],
                Terms = "This offer is subject to verification of documents, reference checks, and the standard probation review. Employment terms are governed by the GameHub Arena HR policy.",
                Status = status,
                ReportingManagerId = manager.Id,
                IssuedAt = status == JobOfferStatus.Draft ? null : At(offerDate, 10, 0),
                RespondedAt = status is JobOfferStatus.Accepted or JobOfferStatus.Rejected or JobOfferStatus.Hired
                    ? At(offerDate.AddDays(1 + index % 3), 12, 0)
                    : null,
                WithdrawnAt = status == JobOfferStatus.Withdrawn ? At(offerDate.AddDays(2), 15, 0) : null,
                IsActive = true,
                CreatedAt = At(offerDate, 9, 45),
                CreatedByUserId = context.HrUserId
            };

            if (status == JobOfferStatus.Hired)
            {
                offer.JoiningRecordedAt = At(joiningDate.AddDays(1), 9, 0);
            }

            return offer;
        }

        private static Employee BuildHiredEmployee(SeedContext context, JobApplication application, JobOffer offer)
        {
            var manager = context.ManagerByDepartment.TryGetValue(offer.Department, out var departmentManager)
                ? departmentManager
                : context.Managers[0];

            return new Employee
            {
                EmployeeNumber = $"EMP-{context.EmployeeNumbers.Take():000000}",
                FirstName = application.FirstName,
                LastName = application.LastName,
                Email = application.Email,
                PhoneNumber = application.Phone,
                NationalIdNumber = application.NationalIdNumber,
                DateOfBirth = application.DateOfBirth ?? context.Today.AddYears(-30),
                Gender = application.Gender ?? Gender.PreferNotToSay,
                Department = offer.Department,
                Designation = offer.Designation,
                EmploymentType = offer.EmploymentType,
                EmploymentStatus = EmploymentStatus.Active,
                JoiningDate = offer.JoiningDate,
                ReportingManagerId = manager.Id,
                EmploymentAddress = application.CurrentAddress,
                EmergencyContactName = EmergencyContacts[(application.Id + 3) % EmergencyContacts.Length],
                EmergencyContactNumber = context.NextStaffPhone(),
                BasicSalary = offer.OfferedSalary,
                IsActive = true,
                CreatedAt = At(offer.JoiningDate, 9, 0),
                CreatedByUserId = context.HrUserId
            };
        }

        // ------------------------------------------------------------------
        // Performance cases
        // ------------------------------------------------------------------

        private static async Task SeedPerformanceAsync(SeedContext context, CancellationToken cancellationToken)
        {
            var caseSeeds = PerformanceCaseSeeds;
            var employees = context.Employees.Take(caseSeeds.Length).ToList();
            var cases = new List<PerformanceCase>();

            for (var index = 0; index < caseSeeds.Length; index++)
            {
                var seed = caseSeeds[index];
                var employee = employees[index];
                var reportedDate = context.Today.AddDays(-(40 + index * 7));
                var performanceCase = new PerformanceCase
                {
                    CaseNumber = $"PC-{context.CaseNumbers.Take()}",
                    EmployeeId = employee.Id,
                    Employee = employee,
                    IssueType = seed.IssueType,
                    Title = seed.Title,
                    Description = seed.Description,
                    Severity = seed.Severity,
                    ReportedDate = reportedDate,
                    AssignedHrUserId = context.HrUserId,
                    Status = seed.Status,
                    IsActive = true,
                    CreatedAt = At(reportedDate, 9, 0),
                    CreatedByUserId = context.HrUserId
                };

                switch (seed.Status)
                {
                    case PerformanceCaseStatus.Open:
                        break;

                    case PerformanceCaseStatus.UnderReview:
                        performanceCase.Meetings.Add(BuildMeeting(context, performanceCase, reportedDate.AddDays(8), PerformanceMeetingStatus.Completed, index));
                        performanceCase.Warnings.Add(BuildWarning(context, performanceCase, reportedDate.AddDays(14), WarningStatus.Acknowledged, index, isClosed: false));
                        break;

                    case PerformanceCaseStatus.ImprovementPlan:
                        performanceCase.Meetings.Add(BuildMeeting(context, performanceCase, reportedDate.AddDays(9), PerformanceMeetingStatus.Completed, index));
                        performanceCase.Meetings.Add(BuildMeeting(context, performanceCase, index % 2 == 0 ? context.Today.AddDays(3) : reportedDate.AddDays(20), index % 2 == 0 ? PerformanceMeetingStatus.Scheduled : PerformanceMeetingStatus.Completed, index + 1));
                        performanceCase.Warnings.Add(BuildWarning(context, performanceCase, reportedDate.AddDays(16), WarningStatus.Acknowledged, index, isClosed: false));
                        performanceCase.ImprovementPlans.Add(BuildPlan(context, performanceCase, reportedDate, ImprovementPlanStatus.Active, index));
                        performanceCase.ImprovementPlans.Add(BuildPlan(context, performanceCase, reportedDate, index % 2 == 0 ? ImprovementPlanStatus.UnderReview : ImprovementPlanStatus.Overdue, index + 1));
                        break;

                    case PerformanceCaseStatus.Warning:
                        performanceCase.Meetings.Add(BuildMeeting(context, performanceCase, reportedDate.AddDays(10), PerformanceMeetingStatus.Completed, index));
                        if (index % 2 == 0)
                        {
                            performanceCase.Meetings.Add(BuildMeeting(context, performanceCase, reportedDate.AddDays(22), PerformanceMeetingStatus.Completed, index + 1));
                        }
                        performanceCase.Warnings.Add(BuildWarning(context, performanceCase, reportedDate.AddDays(15), WarningStatus.Issued, index, isClosed: false));
                        performanceCase.Warnings.Add(BuildWarning(context, performanceCase, reportedDate.AddDays(24), (index % 3) switch
                        {
                            0 => WarningStatus.Escalated,
                            1 => WarningStatus.Contested,
                            _ => WarningStatus.Acknowledged
                        }, index + 1, isClosed: false));
                        performanceCase.ImprovementPlans.Add(BuildPlan(context, performanceCase, reportedDate, index % 2 == 0 ? ImprovementPlanStatus.Completed : ImprovementPlanStatus.Cancelled, index));
                        break;

                    case PerformanceCaseStatus.Resolved:
                        performanceCase.Meetings.Add(BuildMeeting(context, performanceCase, reportedDate.AddDays(12), PerformanceMeetingStatus.Completed, index));
                        performanceCase.Warnings.Add(BuildWarning(context, performanceCase, reportedDate.AddDays(18), WarningStatus.Closed, index, isClosed: true));
                        performanceCase.ImprovementPlans.Add(BuildPlan(context, performanceCase, reportedDate, ImprovementPlanStatus.Completed, index));
                        performanceCase.ResolutionNotes = "The employee met the agreed expectations over the review period. The case is resolved with a note to monitor progress during the next quarter.";
                        performanceCase.ResolvedAt = At(context.Today.AddDays(-(20 + index % 5)), 16, 0);
                        break;

                    case PerformanceCaseStatus.Closed:
                        performanceCase.Meetings.Add(BuildMeeting(context, performanceCase, reportedDate.AddDays(11), PerformanceMeetingStatus.Completed, index));
                        performanceCase.Warnings.Add(BuildWarning(context, performanceCase, reportedDate.AddDays(19), WarningStatus.Closed, index, isClosed: true));
                        performanceCase.ImprovementPlans.Add(BuildPlan(context, performanceCase, reportedDate, ImprovementPlanStatus.Completed, index));
                        performanceCase.ResolutionNotes = "Formal warning issued and improvement plan completed. No further concerns were recorded during the follow-up period, and the case is now closed.";
                        performanceCase.ResolvedAt = At(context.Today.AddDays(-(30 + index % 5)), 16, 0);
                        performanceCase.ClosedAt = At(context.Today.AddDays(-(15 + index % 3)), 17, 0);
                        break;
                }

                cases.Add(performanceCase);
            }

            context.Db.PerformanceCases.AddRange(cases);
            await context.Db.SaveChangesAsync(cancellationToken);
        }

        private static PerformanceMeeting BuildMeeting(SeedContext context, PerformanceCase performanceCase, DateOnly meetingDate, PerformanceMeetingStatus status, int index)
        {
            var isCompleted = status == PerformanceMeetingStatus.Completed;
            var notes = new[]
            {
                $"Reviewed the concerns raised in the case \"{performanceCase.Title}\". The employee described the contributing factors, and the meeting agreed on clear expectations, check-in dates, and the support available from the department lead.",
                $"The meeting discussed the background to \"{performanceCase.Title}\" and its impact on the team. Both sides agreed on the corrective actions, the evidence to be collected, and the review timeline.",
                $"The employee and line manager walked through the findings related to \"{performanceCase.Title}\", confirmed the expectations for the role, and documented the support and follow-up schedule."
            };

            return new PerformanceMeeting
            {
                PerformanceCase = performanceCase,
                MeetingDate = meetingDate,
                MeetingTime = new TimeOnly(10 + index % 6, index % 2 == 0 ? 0 : 30),
                DiscussionNotes = notes[index % notes.Length],
                Outcome = isCompleted ? "Agreed on a 30-day improvement plan with weekly check-ins and written progress notes." : null,
                FollowUpDate = isCompleted ? meetingDate.AddDays(7) : null,
                Status = status,
                IsActive = true,
                CreatedAt = At(meetingDate.AddDays(-2), 9, 30),
                CreatedByUserId = context.HrUserId
            };
        }

        private static PerformanceWarning BuildWarning(SeedContext context, PerformanceCase performanceCase, DateOnly issueDate, WarningStatus status, int index, bool isClosed)
        {
            if (issueDate > context.Today)
            {
                issueDate = context.Today;
            }

            var reasons = new[]
            {
                $"A formal written warning was issued after \"{performanceCase.Title}\" was reviewed. The policy expectations and the standard required going forward were explained in detail.",
                $"This warning records the formal discussion regarding \"{performanceCase.Title}\". The employee was advised of the specific behaviour that must change and the consequences of continued non-compliance.",
                $"Following the review of \"{performanceCase.Title}\", a written warning was placed on file. The department lead reiterated the relevant SOP and scheduled a follow-up check-in.",
                $"A written warning was issued for \"{performanceCase.Title}\". The employee received a copy of the policy, acknowledged the expected standards, and agreed to immediate corrective action.",
                $"This formal warning relates to \"{performanceCase.Title}\". HR and the line manager met with the employee to document the concerns and the improvement required within the next review cycle.",
                $"Warning issued in connection with \"{performanceCase.Title}\". The employee was reminded of the code of conduct and informed that repeated occurrences may lead to further disciplinary action."
            };

            var response = status switch
            {
                WarningStatus.Acknowledged => new[]
                {
                    "The employee acknowledged the warning and confirmed the actions required to improve. A follow-up review was scheduled with the department lead.",
                    "The warning was signed and returned. The employee accepted the findings and agreed to the proposed corrective steps.",
                    "The employee acknowledged the warning after discussing the matter with the department lead and requested additional coaching support."
                }[index % 3],
                WarningStatus.Contested => "The employee submitted a written response disputing part of the findings. HR is reviewing the response with the department lead before the next review meeting.",
                WarningStatus.Escalated => "The employee did not demonstrate the required improvement within the follow-up window, so the warning was escalated to the department head and HR for further action.",
                WarningStatus.Closed => "The warning was closed after the case was resolved and the follow-up period passed without further concerns.",
                _ => null
            };

            return new PerformanceWarning
            {
                PerformanceCase = performanceCase,
                Reason = reasons[index % reasons.Length],
                IssueDate = issueDate,
                ResponseRemarks = response,
                Status = status,
                IsActive = true,
                CreatedAt = At(issueDate, 11, 0),
                CreatedByUserId = context.HrUserId
            };
        }

        private static ImprovementPlan BuildPlan(SeedContext context, PerformanceCase performanceCase, DateOnly reportedDate, ImprovementPlanStatus status, int index)
        {
            var startDate = reportedDate.AddDays(7);
            var reviewDate = startDate.AddDays(14);
            var endDate = startDate.AddDays(28);
            var progress = 50;

            switch (status)
            {
                case ImprovementPlanStatus.Active:
                    reviewDate = context.Today.AddDays(-5);
                    endDate = context.Today.AddDays(14);
                    progress = 35 + index % 4 * 10;
                    break;
                case ImprovementPlanStatus.UnderReview:
                    reviewDate = context.Today.AddDays(-2);
                    endDate = context.Today.AddDays(7);
                    progress = 65 + index % 3 * 5;
                    break;
                case ImprovementPlanStatus.Overdue:
                    reviewDate = context.Today.AddDays(-20);
                    endDate = context.Today.AddDays(-10);
                    progress = 70;
                    break;
                case ImprovementPlanStatus.Completed:
                    reviewDate = startDate.AddDays(14);
                    endDate = startDate.AddDays(28);
                    progress = 100;
                    break;
                case ImprovementPlanStatus.Cancelled:
                    reviewDate = startDate.AddDays(10);
                    endDate = startDate.AddDays(20);
                    progress = 20;
                    break;
            }

            if (endDate < reviewDate)
            {
                endDate = reviewDate.AddDays(7);
            }

            var objectives = new[]
            {
                $"Address the concerns raised in \"{performanceCase.Title}\" through a structured, measurable improvement plan that restores the expected standard of performance and conduct.",
                $"Deliver a sustained improvement in the areas covered by \"{performanceCase.Title}\", with weekly evidence reviewed by the line manager.",
                $"Rebuild consistent performance against the role expectations identified in \"{performanceCase.Title}\" over the plan period, with coaching support from the department lead.",
                $"Demonstrate measurable progress on \"{performanceCase.Title}\" by meeting the agreed targets at each weekly check-in and at the final review."
            };
            var expectations = new[]
            {
                "Meet the agreed weekly targets, attend all scheduled check-ins, submit progress notes on time, and complete the assigned training or coaching support within the review window.",
                "Complete all assigned tasks on time, keep absence and lateness within policy limits, and provide documented evidence of progress at each scheduled review.",
                "Achieve the agreed quality and turnaround targets for four consecutive weeks, attend every coaching session, and act on written feedback without repeat findings."
            };
            var reviewNotes = new[]
            {
                "Progress was reviewed with the employee and the department lead. The agreed evidence was checked against the plan targets.",
                "The weekly check-in covered task completion, attendance, and feedback from colleagues. Progress remains on track against the plan.",
                "The review confirmed the recorded evidence and identified one area that needs closer follow-up before the next check-in."
            };

            return new ImprovementPlan
            {
                PerformanceCase = performanceCase,
                Objectives = objectives[index % objectives.Length],
                MeasurableExpectations = expectations[index % expectations.Length],
                StartDate = startDate,
                ReviewDate = reviewDate,
                EndDate = endDate,
                ProgressPercent = progress,
                ReviewNotes = status is ImprovementPlanStatus.Completed or ImprovementPlanStatus.Overdue or ImprovementPlanStatus.UnderReview
                    ? reviewNotes[index % reviewNotes.Length]
                    : null,
                Outcome = status switch
                {
                    ImprovementPlanStatus.Completed => index % 2 == 0
                        ? "The employee met the measurable expectations and sustained the improvement through the review period."
                        : "All agreed targets were achieved across the review period and the line manager confirmed a return to the expected standard.",
                    ImprovementPlanStatus.Cancelled => "The plan was cancelled after the case was closed and the employee returned to the standard rota.",
                    _ => null
                },
                Status = status,
                IsActive = true,
                CreatedAt = At(startDate, 10, 0),
                CreatedByUserId = context.HrUserId
            };
        }

        // ------------------------------------------------------------------
        // Terminations and clearance
        // ------------------------------------------------------------------

        private static async Task SeedTerminationsAsync(SeedContext context, CancellationToken cancellationToken)
        {
            var employees = context.Employees.Take(27).Skip(5).ToList();
            var requests = new List<TerminationRequest>();
            var statuses = new[]
            {
                TerminationStatus.Draft, TerminationStatus.Draft, TerminationStatus.Draft,
                TerminationStatus.PendingApproval, TerminationStatus.PendingApproval, TerminationStatus.PendingApproval, TerminationStatus.PendingApproval,
                TerminationStatus.Approved, TerminationStatus.Approved, TerminationStatus.Approved, TerminationStatus.Approved,
                TerminationStatus.Rejected, TerminationStatus.Rejected, TerminationStatus.Rejected,
                TerminationStatus.InProgress, TerminationStatus.InProgress, TerminationStatus.InProgress, TerminationStatus.InProgress,
                TerminationStatus.Completed, TerminationStatus.Completed, TerminationStatus.Completed, TerminationStatus.Completed
            };
            var types = new[]
            {
                TerminationType.Resignation, TerminationType.ContractEnd, TerminationType.Redundancy, TerminationType.MutualAgreement,
                TerminationType.Dismissal, TerminationType.Other
            };

            for (var index = 0; index < employees.Count && index < statuses.Length; index++)
            {
                var employee = employees[index];
                var status = statuses[index];
                // Retirement is reserved for the two employees at retirement age at the end of the list.
                var type = index is 20 or 21 ? TerminationType.Retirement : types[index % types.Length];
                var requestDate = status switch
                {
                    TerminationStatus.Draft => context.Today.AddDays(-(5 + index)),
                    TerminationStatus.PendingApproval => context.Today.AddDays(-(10 + index * 2)),
                    TerminationStatus.Approved => context.Today.AddDays(-(25 + index * 2)),
                    TerminationStatus.Rejected => context.Today.AddDays(-(30 + index * 2)),
                    TerminationStatus.InProgress => context.Today.AddDays(-(45 + index)),
                    TerminationStatus.Completed => context.Today.AddDays(-(120 + index * 3)),
                    _ => context.Today.AddDays(-20)
                };

                var lastWorkingDay = status switch
                {
                    TerminationStatus.Completed => requestDate.AddDays(45),
                    TerminationStatus.InProgress => context.Today.AddDays(5 + index % 10),
                    TerminationStatus.Approved => context.Today.AddDays(20 + index % 15),
                    TerminationStatus.PendingApproval => context.Today.AddDays(30 + index % 15),
                    TerminationStatus.Draft => context.Today.AddDays(40 + index % 20),
                    TerminationStatus.Rejected => requestDate.AddDays(30),
                    _ => context.Today.AddDays(30)
                };
                if (lastWorkingDay < requestDate)
                {
                    lastWorkingDay = requestDate.AddDays(7);
                }

                var (reason, explanation) = TerminationNarrative(type, $"{employee.FirstName} {employee.LastName}");
                var decided = status is TerminationStatus.Approved or TerminationStatus.Rejected or TerminationStatus.InProgress or TerminationStatus.Completed;
                var decisionAt = decided
                    ? At(requestDate.AddDays(status == TerminationStatus.Rejected ? 4 : 6), 14, 0)
                    : (DateTime?)null;
                var completedAt = status == TerminationStatus.Completed
                    ? At(lastWorkingDay.AddDays(2), 16, 0)
                    : (DateTime?)null;

                var request = new TerminationRequest
                {
                    ReferenceNumber = $"TRM-{context.TerminationNumbers.Take()}",
                    EmployeeId = employee.Id,
                    Employee = employee,
                    TerminationType = type,
                    Reason = reason,
                    Explanation = explanation,
                    RequestDate = requestDate,
                    ProposedLastWorkingDay = lastWorkingDay,
                    NoticePeriod = NoticePeriodFor(type),
                    Status = status,
                    Remarks = status == TerminationStatus.PendingApproval ? "Supporting documentation has been attached for review." : null,
                    DecisionRemarks = status switch
                    {
                        TerminationStatus.Approved or TerminationStatus.InProgress or TerminationStatus.Completed => "Approved by HR and the department head. The clearance checklist has been issued to all responsible departments.",
                        TerminationStatus.Rejected => "Rejected because the notice period and handover requirements were not met. The employee remains active pending a revised request.",
                        _ => null
                    },
                    DecisionAt = decisionAt,
                    DecidedByUserId = decided ? context.ApproverId : null,
                    CompletedAt = completedAt,
                    IsActive = true,
                    CreatedAt = At(requestDate, 11, 0),
                    CreatedByUserId = context.HrUserId
                };

                request.StatusHistory.Add(new TerminationStatusHistory
                {
                    FromStatus = null,
                    ToStatus = TerminationStatus.Draft,
                    Note = "Termination request created.",
                    ChangedByUserId = context.HrUserId,
                    ChangedAt = At(requestDate, 11, 0)
                });

                if (status != TerminationStatus.Draft)
                {
                    request.StatusHistory.Add(new TerminationStatusHistory
                    {
                        FromStatus = TerminationStatus.Draft,
                        ToStatus = TerminationStatus.PendingApproval,
                        Note = "Submitted for approval.",
                        ChangedByUserId = context.HrUserId,
                        ChangedAt = At(requestDate.AddDays(1), 10, 0)
                    });
                }

                if (decided && decisionAt.HasValue)
                {
                    request.StatusHistory.Add(new TerminationStatusHistory
                    {
                        FromStatus = TerminationStatus.PendingApproval,
                        ToStatus = status == TerminationStatus.Rejected ? TerminationStatus.Rejected : TerminationStatus.Approved,
                        Note = status == TerminationStatus.Rejected
                            ? "Request rejected. See decision remarks."
                            : "Request approved and clearance checklist issued.",
                        ChangedByUserId = context.ApproverId,
                        ChangedAt = decisionAt.Value
                    });
                }

                if (status is TerminationStatus.InProgress or TerminationStatus.Completed && decisionAt.HasValue)
                {
                    request.StatusHistory.Add(new TerminationStatusHistory
                    {
                        FromStatus = TerminationStatus.Approved,
                        ToStatus = TerminationStatus.InProgress,
                        Note = "Clearance process started.",
                        ChangedByUserId = context.HrUserId,
                        ChangedAt = decisionAt.Value.AddDays(1)
                    });
                }

                if (status == TerminationStatus.Completed && completedAt.HasValue)
                {
                    request.StatusHistory.Add(new TerminationStatusHistory
                    {
                        FromStatus = TerminationStatus.InProgress,
                        ToStatus = TerminationStatus.Completed,
                        Note = "All mandatory clearance items cleared. Offboarding completed.",
                        ChangedByUserId = context.HrUserId,
                        ChangedAt = completedAt.Value
                    });
                }

                if (status is TerminationStatus.Approved or TerminationStatus.InProgress or TerminationStatus.Completed && decisionAt.HasValue)
                {
                    AddClearanceItems(context, request, status, employee, decisionAt.Value);
                }

                if (status == TerminationStatus.Completed)
                {
                    employee.EmploymentStatus = EmploymentStatus.Inactive;
                    employee.UpdatedAt = completedAt;
                }

                requests.Add(request);
            }

            context.Db.TerminationRequests.AddRange(requests);
            await context.Db.SaveChangesAsync(cancellationToken);
        }

        private static void AddClearanceItems(SeedContext context, TerminationRequest request, TerminationStatus status, Employee employee, DateTime decisionAt)
        {
            var departmentManager = context.ManagerByDepartment.TryGetValue(employee.Department, out var manager)
                ? manager
                : context.Managers[0];
            var handoverManager = $"{departmentManager.FirstName} {departmentManager.LastName}";
            var items = new[]
            {
                (ClearanceCategory.ITAssets, "Rehan Sattar", "IT"),
                (ClearanceCategory.DepartmentHandover, handoverManager, employee.Department),
                (ClearanceCategory.Finance, "Faisal Rehman", "Finance"),
                (ClearanceCategory.Administration, "Imran Qureshi", "Administration"),
                (ClearanceCategory.HR, "Hira Sheikh", "Human Resources")
            };

            for (var index = 0; index < items.Length; index++)
            {
                var (category, responsiblePerson, responsibleDepartment) = items[index];
                ClearanceStatus itemStatus;
                DateOnly? completedDate = null;
                string? outstandingItems = null;
                string? remarks;

                if (status == TerminationStatus.Completed)
                {
                    itemStatus = ClearanceStatus.Cleared;
                    completedDate = DateOnly.FromDateTime(decisionAt).AddDays(35 + index % 3);
                    if (completedDate > context.Today)
                    {
                        completedDate = context.Today.AddDays(-(index % 3));
                    }
                    remarks = index switch
                    {
                        0 => "Laptop, access card, and headset returned and checked.",
                        1 => "Department handover notes and shared drive access completed.",
                        2 => "No outstanding dues or advances.",
                        3 => "Locker key and uniform returned.",
                        _ => "Exit interview completed and employee file updated."
                    };
                }
                else if (status == TerminationStatus.InProgress)
                {
                    itemStatus = index switch
                    {
                        0 => ClearanceStatus.Cleared,
                        1 => ClearanceStatus.InProgress,
                        2 => ClearanceStatus.Cleared,
                        3 => ClearanceStatus.Pending,
                        _ => ClearanceStatus.InProgress
                    };
                    if (itemStatus == ClearanceStatus.Cleared)
                    {
                        completedDate = context.Today.AddDays(-(2 + index % 2));
                        remarks = index == 0 ? "Laptop and access card returned." : "No outstanding dues or advances.";
                    }
                    else
                    {
                        outstandingItems = index switch
                        {
                            1 => "Handover of open tasks and shared drive access still in progress.",
                            3 => "Locker key and uniform are yet to be returned.",
                            _ => "Exit interview and final HR file review are pending."
                        };
                        remarks = "Department has been reminded to complete the checklist.";
                    }
                }
                else
                {
                    itemStatus = index == 0 ? ClearanceStatus.InProgress : ClearanceStatus.Pending;
                    outstandingItems = "Checklist issued after approval; department sign-off pending.";
                    remarks = "Awaiting department sign-off.";
                }

                request.ClearanceItems.Add(new ClearanceItem
                {
                    Category = category,
                    ResponsiblePerson = responsiblePerson,
                    ResponsibleDepartment = responsibleDepartment,
                    IsMandatory = true,
                    Status = itemStatus,
                    OutstandingItems = outstandingItems,
                    Remarks = remarks,
                    CompletedDate = completedDate,
                    IsActive = true,
                    CreatedAt = decisionAt.AddHours(1 + index),
                    CreatedByUserId = context.HrUserId
                });
            }
        }

        private static (string Reason, string Explanation) TerminationNarrative(TerminationType type, string employeeName)
            => type switch
            {
                TerminationType.Resignation => (
                    "Resignation due to family relocation",
                    $"{employeeName} has submitted a resignation to relocate with family. The department has been informed and a structured handover plan is in progress."),
                TerminationType.Dismissal => (
                    "Dismissal following repeated policy breaches",
                    $"{employeeName} has been dismissed after repeated breaches of the attendance and workplace conduct policy. The decision follows a documented disciplinary process and a final warning."),
                TerminationType.Redundancy => (
                    "Role affected by operational restructuring",
                    $"The role held by {employeeName} has been made redundant following an operational restructuring of the department. Consultation has taken place and redundancy entitlements will be settled through payroll."),
                TerminationType.ContractEnd => (
                    "Fixed-term contract concluded",
                    $"The fixed-term contract for {employeeName} has reached its agreed end date. The department confirmed that the role will not be extended and a handover has been scheduled."),
                TerminationType.Retirement => (
                    "Retirement at superannuation age",
                    $"{employeeName} is retiring at the agreed superannuation age after a long period of service. Final settlement and benefits will be processed in line with the HR policy."),
                TerminationType.MutualAgreement => (
                    "Mutual separation agreement",
                    $"{employeeName} and the organisation have agreed to a mutual separation. The terms have been documented, and the employee will support a short handover before the last working day."),
                _ => (
                    "Separation requested by employee",
                    $"{employeeName} has requested separation from the organisation for personal reasons. The department has been informed and the standard offboarding process will apply.")
            };

        private static string? NoticePeriodFor(TerminationType type) => type switch
        {
            TerminationType.Dismissal => "Immediate",
            TerminationType.Resignation => "1 Month",
            TerminationType.Redundancy => "2 Months",
            TerminationType.ContractEnd => "As per contract",
            TerminationType.Retirement => "3 Months",
            TerminationType.MutualAgreement => "1 Month",
            _ => "2 Weeks"
        };

        // ------------------------------------------------------------------
        // Shared helpers
        // ------------------------------------------------------------------

        private static async Task<int?> ResolveUserIdAsync(ApplicationDbContext db, UserRole role, CancellationToken cancellationToken)
            => await db.Users.AsNoTracking()
                .Where(user => user.Role == role && user.IsActive)
                .OrderBy(user => user.Id)
                .Select(user => (int?)user.Id)
                .FirstOrDefaultAsync(cancellationToken);

        private static async Task<int> NextSequenceAsync(IQueryable<string> values, string prefix, CancellationToken cancellationToken)
        {
            var matching = await values.Where(value => value.StartsWith(prefix)).ToListAsync(cancellationToken);
            var maximum = 0;
            foreach (var value in matching)
            {
                var suffix = value.Substring(prefix.Length);
                if (int.TryParse(suffix, out var parsed) && parsed > maximum)
                {
                    maximum = parsed;
                }
            }

            return maximum + 1;
        }

        private static async Task WriteCvFilesAsync(SeedContext context, IServiceProvider services, CancellationToken cancellationToken)
        {
            if (context.CvFiles.Count == 0)
            {
                return;
            }

            var generator = services.GetService<ISimplePdfGenerator>();
            var environment = services.GetService<IWebHostEnvironment>();
            if (generator == null || environment == null)
            {
                return;
            }

            var folder = Path.Combine(environment.ContentRootPath, "Storage", "hr", CvFolder);
            Directory.CreateDirectory(folder);

            foreach (var (storedName, title, lines) in context.CvFiles)
            {
                try
                {
                    var bytes = generator.Generate(title, lines);
                    await File.WriteAllBytesAsync(Path.Combine(folder, storedName), bytes, cancellationToken);
                }
                catch (IOException)
                {
                    // Demo documents are best effort; a missing file only affects the download link.
                }
                catch (UnauthorizedAccessException)
                {
                }
            }
        }

        private static DateTime At(DateOnly date, int hour, int minute = 0)
            => DateTime.SpecifyKind(date.ToDateTime(new TimeOnly(hour, minute)), DateTimeKind.Utc);

        private static string Slug(string value)
            => new string(value.Where(char.IsLetterOrDigit).ToArray()).ToLowerInvariant();

        private static readonly string[] Benefits =
        {
            "Health insurance, annual performance bonus, and paid leave.",
            "Health insurance, gym membership, and tournament travel allowance.",
            "Health insurance, provident fund, and annual performance bonus.",
            "Health insurance, meal allowance, and paid training opportunities.",
            "Health insurance, performance bonus, and staff membership benefits."
        };

        private static readonly string[] EmergencyContacts =
        {
            "Adeel Raza", "Nadia Khan", "Faisal Abbas", "Rabia Malik", "Sohail Tariq",
            "Saima Yousaf", "Kamran Iqbal", "Mehreen Shah", "Naveed Akhtar", "Farah Baig"
        };

        private static readonly string[] EmergencyAddresses =
        {
            "House 18, Street 7", "House 42, Street 12", "House 6, Lane 3", "House 27, Block C",
            "House 9, Street 2", "House 55, Lane 8", "House 31, Block B", "House 14, Street 5"
        };

        private sealed class SeedContext
        {
            public ApplicationDbContext Db { get; init; } = null!;
            public DateTime Now { get; init; }
            public DateOnly Today { get; init; }
            public int? HrUserId { get; set; }
            public int? ApproverId { get; set; }
            public Sequence EmployeeNumbers { get; init; } = null!;
            public Sequence PostingNumbers { get; init; } = null!;
            public Sequence ApplicationNumbers { get; init; } = null!;
            public Sequence OfferNumbers { get; init; } = null!;
            public Sequence CaseNumbers { get; init; } = null!;
            public Sequence TerminationNumbers { get; init; } = null!;
            public List<Employee> Managers { get; } = new();
            public List<Employee> Employees { get; } = new();
            public Dictionary<string, Employee> ManagerByDepartment { get; } = new(StringComparer.OrdinalIgnoreCase);
            public List<(string StoredName, string Title, IReadOnlyList<string> Lines)> CvFiles { get; } = new();

            private int _staffPhone;
            private int _candidatePhone;
            private int _staffNationalId;
            private int _candidateNationalId;

            public string NextStaffPhone() => $"030{10000000 + _staffPhone++:00000000}";
            public string NextCandidatePhone() => $"030{20000000 + _candidatePhone++:00000000}";
            public string NextStaffNationalId() => $"35202{10000000 + _staffNationalId++:00000000}";
            public string NextCandidateNationalId() => $"35202{20000000 + _candidateNationalId++:00000000}";
        }

        private sealed class Sequence
        {
            private int _next;

            public Sequence(int next) => _next = next;

            public int Take() => _next++;
        }

        private sealed record EmployeeSeed(
            string FirstName,
            string LastName,
            string Department,
            string Designation,
            EmploymentType EmploymentType,
            EmploymentStatus EmploymentStatus,
            int Age,
            int TenureMonths,
            decimal Salary,
            Gender Gender,
            string City);

        private sealed class Candidate
        {
            public int Index { get; init; }
            public string FirstName { get; init; } = string.Empty;
            public string LastName { get; init; } = string.Empty;
            public Gender Gender { get; init; }
            public string Email { get; init; } = string.Empty;
            public string Phone { get; init; } = string.Empty;
            public string NationalIdNumber { get; init; } = string.Empty;
            public DateOnly DateOfBirth { get; init; }
            public string City { get; init; } = string.Empty;
            public string Address { get; init; } = string.Empty;
            public int ExperienceYears { get; init; }
            public string Education { get; init; } = string.Empty;
            public string? CurrentEmployer { get; init; }
            public string? CurrentJobTitle { get; init; }
            public string NoticePeriod { get; init; } = string.Empty;
            public string Summary { get; init; } = string.Empty;
            public string? LinkedInUrl { get; init; }
            public string? PortfolioUrl { get; init; }
            public string CvStoredName { get; init; } = string.Empty;
        }

        private sealed record PostingSeed(
            string Title,
            string Department,
            EmploymentType EmploymentType,
            WorkplaceType WorkplaceType,
            string Location,
            int Vacancies,
            decimal? MinimumSalary,
            decimal? MaximumSalary,
            string Experience,
            string Education,
            JobPostingStatus Status,
            string Summary,
            string Responsibilities,
            string Qualifications,
            string[] Skills,
            string[] Questions);

        private sealed record PerformanceCaseSeed(
            string Title,
            string Description,
            PerformanceIssueType IssueType,
            PerformanceSeverity Severity,
            PerformanceCaseStatus Status);

        private static readonly PostingSeed[] PostingSeeds =
        {
            new("Front Desk Executive", "Operations", EmploymentType.FullTime, WorkplaceType.OnSite, "Lahore, Pakistan", 2, 70000m, 95000m,
                "Entry Level (0-1 years)", "Intermediate / A Levels", JobPostingStatus.Open,
                "GameHub Arena is looking for a welcoming front desk executive to be the first point of contact for members and guests.",
                "Welcome members and guests, manage court check-ins, handle phone and walk-in enquiries, process daily payments, and keep the reception area organised.",
                "Fluent in English and Urdu, confident with basic computer systems, and available for evening and weekend shifts.",
                new[] { "Customer Service", "Communication", "Point of Sale", "Scheduling" },
                new[] { "How many years of front-desk or customer service experience do you have?", "Are you comfortable working evening and weekend shifts?" }),

            new("Facility Supervisor", "Operations", EmploymentType.FullTime, WorkplaceType.OnSite, "Lahore, Pakistan", 1, 110000m, 145000m,
                "Mid Level (3-5 years)", "Bachelor's Degree", JobPostingStatus.Open,
                "We are hiring a facility supervisor to keep our courts, changing rooms, and shared spaces safe, clean, and ready for use.",
                "Supervise the facilities team, run daily safety and cleanliness checks, coordinate maintenance tickets, and maintain equipment and consumable stock records.",
                "Three or more years of facilities or operations supervision, strong record-keeping, and the ability to lead a shift-based team.",
                new[] { "Facilities Management", "Team Supervision", "Safety Compliance", "Inventory Control" },
                new[] { "Describe your experience supervising a shift-based facilities team." }),

            new("Security Guard", "Operations", EmploymentType.FullTime, WorkplaceType.OnSite, "Karachi, Pakistan", 4, 45000m, 60000m,
                "Entry Level (0-1 years)", "Matriculation", JobPostingStatus.Closed,
                "GameHub Arena requires alert and courteous security guards to protect members, staff, and assets across the evening shift.",
                "Control entry and exit points, monitor the premises, support emergency procedures, and record incidents accurately in the security log.",
                "Physically fit, able to work rotating shifts, and familiar with basic security and emergency procedures.",
                new[] { "Security Operations", "Emergency Response", "Incident Reporting" },
                new[] { "Are you available for rotating night shifts?" }),

            new("Accounts Executive", "Finance", EmploymentType.FullTime, WorkplaceType.Hybrid, "Islamabad, Pakistan", 2, 120000m, 170000m,
                "Mid Level (3-5 years)", "Bachelor's Degree", JobPostingStatus.Open,
                "The finance team is looking for an accounts executive to manage day-to-day bookkeeping, invoicing, and reconciliations.",
                "Post daily sales and membership receipts, reconcile bank and payment gateway statements, prepare invoices, and support the month-end close.",
                "Bachelor's degree in finance or accounting, three or more years of accounts experience, and strong Excel and ERP skills.",
                new[] { "Bookkeeping", "Reconciliation", "Excel", "Accounts Payable" },
                new[] { "Which accounting systems have you used in your previous roles?" }),

            new("Payroll Officer", "Finance", EmploymentType.FullTime, WorkplaceType.OnSite, "Lahore, Pakistan", 1, 115000m, 150000m,
                "Mid Level (3-5 years)", "Bachelor's Degree", JobPostingStatus.Closed,
                "We are looking for a payroll officer to run accurate and timely payroll for our full-time, part-time, and contract staff.",
                "Prepare monthly payroll, validate attendance and overtime, process deductions and benefits, and maintain confidential payroll records.",
                "Experience with payroll processing and tax deductions, strong attention to detail, and a commitment to confidentiality.",
                new[] { "Payroll Processing", "Tax Compliance", "Excel", "Confidentiality" },
                new[] { "What payroll systems and headcount sizes have you supported?" }),

            new("Corporate Sales Executive", "Sales & Marketing", EmploymentType.FullTime, WorkplaceType.Hybrid, "Lahore, Pakistan", 3, 95000m, 140000m,
                "Mid Level (3-5 years)", "Bachelor's Degree", JobPostingStatus.Open,
                "Our corporate sales team is expanding to grow tournament, event, and membership partnerships across the region.",
                "Build a corporate pipeline, run site visits and proposals, negotiate packages, and maintain accurate activity and forecast records in the CRM.",
                "A proven sales record in B2B services, strong presentation skills, and the ability to manage a multi-stage pipeline.",
                new[] { "B2B Sales", "Negotiation", "CRM", "Proposal Writing" },
                new[] { "Describe a corporate partnership you developed from first contact to close." }),

            new("Digital Marketing Specialist", "Sales & Marketing", EmploymentType.FullTime, WorkplaceType.Remote, "Islamabad, Pakistan", 1, 85000m, 125000m,
                "Mid Level (3-5 years)", "Bachelor's Degree", JobPostingStatus.Open,
                "We need a digital marketing specialist to grow our online audience and drive qualified bookings through paid and organic channels.",
                "Plan and run social and search campaigns, manage the content calendar, report on performance, and work with the arena team on seasonal promotions.",
                "Hands-on experience with Meta and Google Ads, strong copy and analytics skills, and a track record of improving conversion.",
                new[] { "Digital Advertising", "Content Marketing", "Analytics", "SEO" },
                new[] { "Which campaigns have you managed, and what results did they deliver?" }),

            new("Senior Cricket Coach", "Sports & Recreation", EmploymentType.FullTime, WorkplaceType.OnSite, "Lahore, Pakistan", 2, 150000m, 220000m,
                "Senior (5-8 years)", "Bachelor's Degree", JobPostingStatus.Open,
                "GameHub Arena is seeking a senior cricket coach to lead our competitive training programme and develop the next generation of players.",
                "Design training plans, run group and one-to-one sessions, prepare players for tournaments, and mentor assistant coaches.",
                "Five or more years of coaching experience, a recognised coaching certification, and a proven player-development record.",
                new[] { "Cricket Coaching", "Player Development", "Team Leadership", "Session Planning" },
                new[] { "How many years have you coached competitive cricket?" }),

            new("Badminton Coach", "Sports & Recreation", EmploymentType.PartTime, WorkplaceType.OnSite, "Karachi, Pakistan", 3, 55000m, 80000m,
                "Entry Level (0-1 years)", "Intermediate / A Levels", JobPostingStatus.Closed,
                "We are adding part-time badminton coaches to support our junior academy and adult group sessions.",
                "Deliver safe and engaging group sessions, track player progress, prepare equipment, and support academy events.",
                "A recognised badminton coaching qualification, strong communication skills, and availability on weekday evenings.",
                new[] { "Badminton Coaching", "Group Instruction", "Player Safety" },
                new[] { "What coaching qualifications do you hold?" }),

            new("Fitness Trainer", "Sports & Recreation", EmploymentType.Contract, WorkplaceType.OnSite, "Lahore, Pakistan", 2, 70000m, 100000m,
                "Mid Level (3-5 years)", "Diploma", JobPostingStatus.Open,
                "Our fitness studio is looking for certified trainers to deliver one-to-one and small-group programmes.",
                "Assess client needs, design safe programmes, coach sessions, and maintain accurate client progress records.",
                "A recognised personal training certification, current first-aid training, and experience coaching diverse fitness levels.",
                new[] { "Personal Training", "Programme Design", "Client Assessment", "First Aid" },
                new[] { "Which fitness certifications do you currently hold?" }),

            new("Customer Support Officer", "Customer Experience", EmploymentType.FullTime, WorkplaceType.OnSite, "Karachi, Pakistan", 4, 65000m, 90000m,
                "Entry Level (0-1 years)", "Bachelor's Degree", JobPostingStatus.Open,
                "We are hiring customer support officers to help members with bookings, memberships, and service enquiries.",
                "Respond to calls and messages, resolve booking issues, escalate complaints promptly, and keep customer records accurate.",
                "Clear communication, a calm approach to complaints, and confidence using booking and CRM systems.",
                new[] { "Customer Support", "Complaint Resolution", "CRM", "Communication" },
                new[] { "How do you handle a complaint when the requested outcome is not possible?" }),

            new("Booking Coordinator", "Customer Experience", EmploymentType.FullTime, WorkplaceType.OnSite, "Lahore, Pakistan", 2, 85000m, 110000m,
                "Mid Level (3-5 years)", "Bachelor's Degree", JobPostingStatus.Closed,
                "The booking team needs a coordinator to manage court schedules, group bookings, and event reservations.",
                "Maintain the booking calendar, coordinate with coaches and facility teams, resolve clashes, and confirm reservations with customers.",
                "Experience in scheduling or reservations, excellent organisation, and the ability to stay calm during peak demand.",
                new[] { "Scheduling", "Reservations", "Customer Communication", "Problem Solving" },
                new[] { "Describe how you would resolve a double-booking during peak hours." }),

            new("HR Officer", "Human Resources", EmploymentType.FullTime, WorkplaceType.OnSite, "Lahore, Pakistan", 1, 90000m, 120000m,
                "Mid Level (3-5 years)", "Bachelor's Degree", JobPostingStatus.Open,
                "We are looking for an HR officer to support recruitment, onboarding, employee records, and day-to-day HR administration.",
                "Coordinate interviews and onboarding, maintain employee files, support attendance and leave processes, and respond to routine HR queries.",
                "Two or more years of HR generalist experience, strong confidentiality, and familiarity with HR systems.",
                new[] { "HR Administration", "Recruitment Support", "Onboarding", "Employee Records" },
                new[] { "Which HR processes have you owned end to end?" }),

            new("Recruitment Officer", "Human Resources", EmploymentType.FullTime, WorkplaceType.Hybrid, "Islamabad, Pakistan", 2, 105000m, 135000m,
                "Mid Level (3-5 years)", "Bachelor's Degree", JobPostingStatus.Draft,
                "The HR team is preparing to add recruitment officers to support our growing hiring pipeline.",
                "Source candidates, screen applications, coordinate interviews, and keep the applicant tracking records accurate and timely.",
                "Experience in high-volume recruitment, strong candidate communication, and the ability to work to hiring deadlines.",
                new[] { "Talent Sourcing", "Screening", "Interview Coordination", "Applicant Tracking" },
                new[] { "What sourcing channels have delivered the best results for you?" }),

            new("IT Support Engineer", "IT", EmploymentType.FullTime, WorkplaceType.OnSite, "Lahore, Pakistan", 2, 120000m, 160000m,
                "Mid Level (3-5 years)", "Bachelor's Degree", JobPostingStatus.Open,
                "GameHub Arena is hiring an IT support engineer to keep our booking, payment, and access systems running smoothly.",
                "Resolve hardware and software issues, support the booking and POS systems, manage user accounts, and maintain the IT asset register.",
                "Three or more years of end-user support, strong troubleshooting skills, and experience with Windows and network fundamentals.",
                new[] { "Technical Support", "Windows", "Networking", "Asset Management" },
                new[] { "Which business systems have you supported in a live operations environment?" }),

            new("Systems Administrator", "IT", EmploymentType.FullTime, WorkplaceType.Hybrid, "Karachi, Pakistan", 1, 145000m, 190000m,
                "Senior (5-8 years)", "Bachelor's Degree", JobPostingStatus.Closed,
                "We are looking for a systems administrator to own our server, backup, and network administration.",
                "Maintain servers and backups, monitor system health, manage access controls, and lead incident recovery activities.",
                "Five or more years of systems administration, strong backup and recovery experience, and a security-first mindset.",
                new[] { "Server Administration", "Backup Recovery", "Network Security", "Access Control" },
                new[] { "Describe the most complex system recovery you have handled." }),

            new("Maintenance Engineer", "Facilities", EmploymentType.FullTime, WorkplaceType.OnSite, "Lahore, Pakistan", 2, 135000m, 175000m,
                "Senior (5-8 years)", "Bachelor's Degree", JobPostingStatus.Open,
                "The facilities team requires a maintenance engineer to plan and deliver preventive maintenance across the arena.",
                "Schedule preventive maintenance, diagnose equipment faults, supervise contractors, and maintain compliance and safety records.",
                "Five or more years of maintenance experience, strong electrical or mechanical knowledge, and contractor supervision skills.",
                new[] { "Preventive Maintenance", "Electrical Systems", "Contractor Supervision", "Safety Compliance" },
                new[] { "Which maintenance management systems have you used?" }),

            new("Cafeteria Supervisor", "Food & Beverage", EmploymentType.FullTime, WorkplaceType.OnSite, "Lahore, Pakistan", 1, 80000m, 105000m,
                "Mid Level (3-5 years)", "Diploma", JobPostingStatus.Closed,
                "Our cafeteria is looking for a supervisor to run daily service, stock control, and food safety standards.",
                "Supervise cafeteria staff, manage stock and supplier deliveries, enforce hygiene standards, and handle customer feedback.",
                "Experience in food service supervision, a food safety qualification, and strong stock-control discipline.",
                new[] { "Food Safety", "Stock Control", "Team Supervision", "Customer Service" },
                new[] { "What food safety standards have you managed in a previous role?" }),

            new("Shift Manager", "Operations", EmploymentType.FullTime, WorkplaceType.OnSite, "Karachi, Pakistan", 2, null, null,
                "Mid Level (3-5 years)", "Bachelor's Degree", JobPostingStatus.Draft,
                "We are preparing to add shift managers to lead evening operations across the Karachi arena.",
                "Lead the shift team, manage opening and closing procedures, resolve member issues, and ensure safety and service standards are met.",
                "Experience leading an operations shift, strong decision-making, and the ability to coordinate multiple departments.",
                new[] { "Shift Leadership", "Operations", "Customer Escalation", "Safety Compliance" },
                new[] { "Describe a difficult operational issue you resolved during a busy shift." }),

            new("Swimming Coach", "Sports & Recreation", EmploymentType.PartTime, WorkplaceType.OnSite, "Islamabad, Pakistan", 2, 60000m, 85000m,
                "Mid Level (3-5 years)", "Diploma", JobPostingStatus.Open,
                "Our swimming academy is hiring part-time coaches to deliver safe and structured lessons for children and adults.",
                "Plan and deliver swimming lessons, assess progress, enforce pool safety rules, and maintain lesson records.",
                "A recognised swimming coaching qualification, current lifeguard training, and experience teaching mixed-ability groups.",
                new[] { "Swimming Instruction", "Water Safety", "Lesson Planning", "First Aid" },
                new[] { "Which swimming levels and age groups have you coached?" }),

            new("Internal Auditor", "Finance", EmploymentType.FullTime, WorkplaceType.Hybrid, "Lahore, Pakistan", 1, 160000m, 210000m,
                "Senior (5-8 years)", "Master's Degree", JobPostingStatus.Draft,
                "The finance team is planning to add an internal auditor to strengthen controls across cash, procurement, and payroll.",
                "Plan and execute internal audits, test controls, document findings, and follow up on remediation actions.",
                "Five or more years of audit experience, a relevant professional qualification, and strong report-writing skills.",
                new[] { "Internal Audit", "Risk Assessment", "Controls Testing", "Report Writing" },
                new[] { "Which audit qualifications and sectors do you have experience in?" }),

            new("Guest Relations Officer", "Customer Experience", EmploymentType.FullTime, WorkplaceType.OnSite, "Islamabad, Pakistan", 2, null, null,
                "Entry Level (0-1 years)", "Bachelor's Degree", JobPostingStatus.Draft,
                "We are preparing a guest relations role to strengthen the welcome experience for members and tournament visitors.",
                "Welcome guests, manage VIP and event arrivals, resolve service issues, and gather feedback to improve the guest experience.",
                "Excellent interpersonal skills, a professional presentation, and the ability to handle pressure with composure.",
                new[] { "Guest Relations", "Communication", "Issue Resolution", "Event Support" },
                new[] { "How would you make a first-time visitor feel welcome at the arena?" })
        };

        private static readonly PerformanceCaseSeed[] PerformanceCaseSeeds =
        {
            new("Repeated late arrivals on opening shift", "The employee has arrived more than 20 minutes late for the opening shift on six occasions this quarter, delaying court preparation and front-desk handover.", PerformanceIssueType.Attendance, PerformanceSeverity.Medium, PerformanceCaseStatus.Open),
            new("Missed monthly membership renewal target", "Renewal conversions finished at 62% against an 80% target for the last two months, with no documented follow-up on lapsed members.", PerformanceIssueType.Performance, PerformanceSeverity.High, PerformanceCaseStatus.Open),
            new("Unapproved leave during tournament weekend", "The employee took two days of unapproved leave during the regional tournament, leaving the front desk short-staffed during peak footfall.", PerformanceIssueType.Conduct, PerformanceSeverity.High, PerformanceCaseStatus.UnderReview),
            new("Incomplete safety checklist for equipment", "Two pre-opening safety checks were skipped, and a damaged net post was left in use until a guest reported it.", PerformanceIssueType.Compliance, PerformanceSeverity.Critical, PerformanceCaseStatus.UnderReview),
            new("Delayed month-end reconciliation", "Bank and payment-gateway reconciliations were submitted four working days after the finance close deadline for two consecutive months.", PerformanceIssueType.Performance, PerformanceSeverity.Medium, PerformanceCaseStatus.UnderReview),
            new("Unprofessional tone with a member", "A member complaint describes a raised voice and dismissive response during a booking dispute; the interaction was captured in the front-desk log.", PerformanceIssueType.Conduct, PerformanceSeverity.High, PerformanceCaseStatus.ImprovementPlan),
            new("Frequent unplanned absences", "The employee has taken five unplanned absences in eight weeks, with three occurring on weekend shifts.", PerformanceIssueType.Attendance, PerformanceSeverity.High, PerformanceCaseStatus.ImprovementPlan),
            new("Cash-handling procedure deviation", "A cash drawer was left unlocked during a shift change and the daily deposit sheet was not countersigned.", PerformanceIssueType.Compliance, PerformanceSeverity.Critical, PerformanceCaseStatus.ImprovementPlan),
            new("Below-target coaching session ratings", "Average session feedback dropped to 3.1 out of 5 across 40 reviews, with repeated comments about late starts and unclear drills.", PerformanceIssueType.Performance, PerformanceSeverity.Medium, PerformanceCaseStatus.ImprovementPlan),
            new("Missed incident escalation", "A guest injury was not escalated to the duty manager within the required 15 minutes, and the incident log was completed the next day.", PerformanceIssueType.Compliance, PerformanceSeverity.High, PerformanceCaseStatus.ImprovementPlan),
            new("Peer conflict in shared workspace", "Two team members reported recurring disagreements over shift handover notes; the issue is affecting coordination in the shared office.", PerformanceIssueType.Other, PerformanceSeverity.Low, PerformanceCaseStatus.ImprovementPlan),
            new("Late submission of weekly sales report", "The weekly pipeline report has been submitted after the Monday deadline for three consecutive weeks.", PerformanceIssueType.Performance, PerformanceSeverity.Low, PerformanceCaseStatus.Warning),
            new("Repeated short-notice shift swaps", "The employee arranged four short-notice shift swaps in one month without manager approval, disrupting rota coverage.", PerformanceIssueType.Attendance, PerformanceSeverity.Medium, PerformanceCaseStatus.Warning),
            new("Unsafe storage of cleaning chemicals", "Cleaning chemicals were stored beside food supplies in the cafeteria store, contrary to the safety policy.", PerformanceIssueType.Compliance, PerformanceSeverity.Critical, PerformanceCaseStatus.Warning),
            new("Inconsistent follow-up on corporate leads", "Twelve corporate enquiries received no follow-up within 48 hours, and three were marked as contacted without a call log.", PerformanceIssueType.Performance, PerformanceSeverity.High, PerformanceCaseStatus.Warning),
            new("Disruptive behaviour during team briefing", "The employee repeatedly interrupted the weekly briefing and dismissed safety guidance in front of the team.", PerformanceIssueType.Conduct, PerformanceSeverity.Medium, PerformanceCaseStatus.Warning),
            new("Missed probation review milestones", "Two probation review checkpoints were missed and the required development notes were not completed.", PerformanceIssueType.Performance, PerformanceSeverity.Medium, PerformanceCaseStatus.Resolved),
            new("Unauthorized use of arena equipment", "Equipment was used outside approved hours without a booking, and the storage log was not updated.", PerformanceIssueType.Conduct, PerformanceSeverity.Low, PerformanceCaseStatus.Resolved),
            new("Delayed response to maintenance tickets", "Four urgent maintenance tickets exceeded the 24-hour response target, including a lighting fault on an active court.", PerformanceIssueType.Performance, PerformanceSeverity.Medium, PerformanceCaseStatus.Resolved),
            new("Attendance at mandatory training", "The employee missed the mandatory first-aid refresher and did not complete the online module within the grace period.", PerformanceIssueType.Compliance, PerformanceSeverity.Medium, PerformanceCaseStatus.Resolved),
            new("Customer complaint handling quality", "Three complaints were closed without a callback, and one response contained inaccurate booking information.", PerformanceIssueType.Conduct, PerformanceSeverity.High, PerformanceCaseStatus.Closed),
            new("Failure to complete handover notes", "Shift handover notes were incomplete on five occasions, causing booking conflicts for the next shift.", PerformanceIssueType.Performance, PerformanceSeverity.Low, PerformanceCaseStatus.Closed)
        };
    }
}
