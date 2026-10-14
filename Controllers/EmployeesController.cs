using System.Security.Claims;
using GameHub.Data;
using GameHub.Filters;
using GameHub.Helpers;
using GameHub.Models.Entities;
using GameHub.Models.Enums;
using GameHub.Models.ViewModels;
using GameHub.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace GameHub.Controllers
{
    [GameHubAuthorize(UserRole.SuperAdmin, UserRole.Admin, UserRole.StaffManager, UserRole.Viewer)]
    [ValidateActiveUser]
    public class EmployeesController : Controller
    {
        private static readonly int[] PageSizes = { 10, 25, 50 };
        private static readonly string[] AllowedImageExtensions = { ".jpg", ".jpeg", ".png", ".webp" };
        private static readonly string[] AllowedImageContentTypes = { "image/jpeg", "image/png", "image/webp" };
        private const long MaxImageSize = 3 * 1024 * 1024;

        private readonly ApplicationDbContext _dbContext;
        private readonly IEmployeeNumberService _employeeNumberService;
        private readonly IWebHostEnvironment _environment;

        public EmployeesController(ApplicationDbContext dbContext, IEmployeeNumberService employeeNumberService, IWebHostEnvironment environment)
        {
            _dbContext = dbContext;
            _employeeNumberService = employeeNumberService;
            _environment = environment;
        }

        [HttpGet]
        public async Task<IActionResult> Index(string? search, string? department, string? designation, EmploymentStatus? employmentStatus, EmploymentType? employmentType, string sortBy = "newest", int page = 1, int pageSize = 10)
        {
            page = Math.Max(page, 1);
            pageSize = PageSizes.Contains(pageSize) ? pageSize : 10;
            search = search?.Trim();
            department = NormalizeOption(department);
            designation = NormalizeOption(designation);
            sortBy = NormalizeSort(sortBy);

            var allEmployees = _dbContext.Employees.AsNoTracking();

            var summary = new
            {
                Total = await allEmployees.CountAsync(),
                Active = await allEmployees.CountAsync(employee => employee.EmploymentStatus == EmploymentStatus.Active),
                OnLeave = await allEmployees.CountAsync(employee => employee.EmploymentStatus == EmploymentStatus.OnLeave),
                Inactive = await allEmployees.CountAsync(employee => employee.EmploymentStatus == EmploymentStatus.Inactive)
            };

            var query = allEmployees;

            if (!string.IsNullOrWhiteSpace(search))
            {
                var normalized = search.ToLower();
                query = query.Where(employee =>
                    employee.EmployeeNumber.ToLower().Contains(normalized) ||
                    employee.FirstName.ToLower().Contains(normalized) ||
                    employee.LastName.ToLower().Contains(normalized) ||
                    (employee.FirstName + " " + employee.LastName).ToLower().Contains(normalized) ||
                    employee.Email.ToLower().Contains(normalized) ||
                    employee.PhoneNumber.ToLower().Contains(normalized) ||
                    employee.NationalIdNumber.ToLower().Contains(normalized));
            }

            if (!string.IsNullOrWhiteSpace(department)) query = query.Where(employee => employee.Department == department);
            if (!string.IsNullOrWhiteSpace(designation)) query = query.Where(employee => employee.Designation == designation);
            if (employmentStatus.HasValue) query = query.Where(employee => employee.EmploymentStatus == employmentStatus.Value);
            if (employmentType.HasValue) query = query.Where(employee => employee.EmploymentType == employmentType.Value);

            query = ApplySorting(query, sortBy);

            var totalRecords = await query.CountAsync();
            var totalPages = totalRecords == 0 ? 1 : (int)Math.Ceiling(totalRecords / (double)pageSize);
            page = Math.Min(page, totalPages);

            var employees = await query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(employee => new EmployeeListItemViewModel
                {
                    Id = employee.Id,
                    EmployeeNumber = employee.EmployeeNumber,
                    FullName = employee.FirstName + " " + employee.LastName,
                    Initials = employee.FirstName.Substring(0, 1) + employee.LastName.Substring(0, 1),
                    ProfileImageUrl = employee.ProfileImageUrl,
                    Department = employee.Department,
                    Designation = employee.Designation,
                    EmploymentType = employee.EmploymentType,
                    EmploymentStatus = employee.EmploymentStatus,
                    JoiningDate = employee.JoiningDate,
                    Email = employee.Email,
                    PhoneNumber = employee.PhoneNumber,
                    ReportingManagerName = employee.ReportingManager != null
                        ? employee.ReportingManager.FirstName + " " + employee.ReportingManager.LastName
                        : null
                })
                .ToListAsync();

            return View(new EmployeeIndexViewModel
            {
                Employees = employees,
                Search = search,
                Department = department,
                Designation = designation,
                EmploymentStatus = employmentStatus,
                EmploymentType = employmentType,
                SortBy = sortBy,
                CurrentPage = page,
                PageSize = pageSize,
                TotalRecords = totalRecords,
                TotalPages = totalPages,
                TotalEmployees = summary.Total,
                ActiveEmployees = summary.Active,
                OnLeaveEmployees = summary.OnLeave,
                InactiveEmployees = summary.Inactive,
                Departments = HRLists.Departments.ToList(),
                Designations = HRLists.Designations.ToList(),
                CanManage = CanManage()
            });
        }

        [HttpGet]
        public async Task<IActionResult> Create(int? offerId = null)
        {
            if (EnsureCanManage() is IActionResult blocked) return blocked;

            var model = new EmployeeFormViewModel
            {
                EmployeeNumber = await _employeeNumberService.GenerateNextEmployeeNumberAsync()
            };

            if (offerId.HasValue)
            {
                await ApplyOfferPrefillAsync(model, offerId.Value);
            }

            return View("Form", await PopulateOptionsAsync(model));
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(EmployeeFormViewModel model)
        {
            if (EnsureCanManage() is IActionResult blocked) return blocked;

            await PrepareAndValidateAsync(model);

            if (!ModelState.IsValid)
            {
                model.EmployeeNumber = await _employeeNumberService.GenerateNextEmployeeNumberAsync();
                return View("Form", await PopulateOptionsAsync(model));
            }

            var employee = new Employee();
            MapToEntity(model, employee, isCreate: true);
            employee.EmployeeNumber = await _employeeNumberService.GenerateNextEmployeeNumberAsync();
            employee.CreatedAt = DateTime.UtcNow;
            employee.CreatedByUserId = CurrentUserId();
            employee.IsActive = true;

            var imageResult = await SaveProfileImageAsync(model);
            if (!imageResult.Success)
            {
                ModelState.AddModelError(nameof(model.ProfileImage), imageResult.Message);
                TempData.SetToast("error", "Upload Failed", "Profile photo could not be uploaded.");
                return View("Form", await PopulateOptionsAsync(model));
            }
            employee.ProfileImageUrl = imageResult.Url;

            _dbContext.Employees.Add(employee);
            await _dbContext.SaveChangesAsync();

            if (model.OfferId.HasValue)
            {
                await LinkOfferAsync(model.OfferId.Value, employee.Id);
            }

            TempData.SetToast("success", "Success", $"Employee {employee.EmployeeNumber} created successfully.");
            return RedirectToAction(nameof(Details), new { id = employee.Id });
        }

        [HttpGet]
        public async Task<IActionResult> Edit(int id)
        {
            if (EnsureCanManage() is IActionResult blocked) return blocked;
            var employee = await _dbContext.Employees.AsNoTracking().FirstOrDefaultAsync(employee => employee.Id == id);
            if (employee == null)
            {
                TempData.SetToast("error", "Not Found", "Employee not found.");
                return NotFound();
            }

            return View("Form", await PopulateOptionsAsync(MapForm(employee)));
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, EmployeeFormViewModel model)
        {
            if (EnsureCanManage() is IActionResult blocked) return blocked;
            if (model.Id != id) return NotFound();
            await PrepareAndValidateAsync(model, id);

            var employee = await _dbContext.Employees.FirstOrDefaultAsync(employee => employee.Id == id);
            if (employee == null)
            {
                TempData.SetToast("error", "Not Found", "Employee not found.");
                return NotFound();
            }

            if (!ModelState.IsValid)
            {
                return View("Form", await PopulateOptionsAsync(model));
            }

            var previousImage = employee.ProfileImageUrl;
            var imageResult = await SaveProfileImageAsync(model);
            if (!imageResult.Success)
            {
                ModelState.AddModelError(nameof(model.ProfileImage), imageResult.Message);
                TempData.SetToast("error", "Upload Failed", "Profile photo could not be uploaded.");
                return View("Form", await PopulateOptionsAsync(model));
            }

            MapToEntity(model, employee, isCreate: false);
            if (model.RemoveProfileImage)
            {
                employee.ProfileImageUrl = null;
            }
            else if (!string.IsNullOrWhiteSpace(imageResult.Url))
            {
                employee.ProfileImageUrl = imageResult.Url;
            }
            else
            {
                employee.ProfileImageUrl = previousImage;
            }
            employee.UpdatedAt = DateTime.UtcNow;

            await _dbContext.SaveChangesAsync();
            if (!string.IsNullOrWhiteSpace(imageResult.Url) || model.RemoveProfileImage)
            {
                DeleteProfileImage(previousImage);
            }

            TempData.SetToast("success", "Success", "Employee updated successfully.");
            return RedirectToAction(nameof(Details), new { id });
        }

        [HttpGet]
        public async Task<IActionResult> Details(int id)
        {
            var employee = await _dbContext.Employees
                .AsNoTracking()
                .Include(employee => employee.ReportingManager)
                .Include(employee => employee.CreatedByUser)
                .FirstOrDefaultAsync(employee => employee.Id == id);
            if (employee == null)
            {
                TempData.SetToast("error", "Not Found", "Employee not found.");
                return NotFound();
            }

            return View(MapDetails(employee));
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> ChangeStatus(int id, EmploymentStatus employmentStatus)
        {
            if (EnsureCanManage() is IActionResult blocked) return blocked;
            if (!ModelState.IsValid || !Enum.IsDefined(employmentStatus))
            {
                return Json(new { success = false, message = "Invalid employment status." });
            }

            var employee = await _dbContext.Employees.FirstOrDefaultAsync(employee => employee.Id == id);
            if (employee == null)
            {
                return Json(new { success = false, message = "Employee not found." });
            }

            employee.EmploymentStatus = employmentStatus;
            employee.UpdatedAt = DateTime.UtcNow;
            await _dbContext.SaveChangesAsync();

            var statusText = employmentStatus.GetDisplayName();
            return Json(new
            {
                success = true,
                status = statusText,
                message = $"{employee.FirstName} {employee.LastName} is now {statusText}."
            });
        }

        [HttpGet]
        public async Task<IActionResult> CheckEmail(string? email, int? id)
        {
            email = NormalizeEmail(email);
            if (string.IsNullOrWhiteSpace(email)) return Json(new { available = true, message = "Email is required." });
            if (!new System.ComponentModel.DataAnnotations.EmailAddressAttribute().IsValid(email)) return Json(new { available = false, message = "Invalid email format." });
            var exists = await _dbContext.Employees.AnyAsync(employee => employee.Email == email && employee.IsActive && (!id.HasValue || employee.Id != id.Value));
            return Json(new { available = !exists, message = exists ? "Email address is already registered to an active employee." : "Email is available." });
        }

        [HttpGet]
        public async Task<IActionResult> CheckPhone(string? phone, int? id)
        {
            var normalized = NormalizePhone(phone);
            if (!HasReasonablePhoneDigits(normalized)) return Json(new { available = false, message = "Invalid phone number." });
            var exists = await PhoneExistsAsync(normalized, id);
            return Json(new { available = !exists, message = exists ? "Phone number is already registered to an active employee." : "Phone number is available." });
        }

        [HttpGet]
        public async Task<IActionResult> CheckNationalId(string? nationalId, int? id)
        {
            nationalId = NormalizeOptional(nationalId);
            if (string.IsNullOrWhiteSpace(nationalId)) return Json(new { available = true, message = "National ID is required." });
            var exists = await _dbContext.Employees.AnyAsync(employee => employee.NationalIdNumber == nationalId && employee.IsActive && (!id.HasValue || employee.Id != id.Value));
            return Json(new { available = !exists, message = exists ? "National ID / CNIC is already registered to an active employee." : "National ID / CNIC is available." });
        }

        private async Task PrepareAndValidateAsync(EmployeeFormViewModel model, int? id = null)
        {
            NormalizeForm(model);

            if (await _dbContext.Employees.AnyAsync(employee => employee.Email == model.Email && employee.IsActive && (!id.HasValue || employee.Id != id.Value)))
            {
                ModelState.AddModelError(nameof(model.Email), "Email address is already registered.");
                ModelState.AddModelError(string.Empty, "Email address is already registered.");
            }

            if (await PhoneExistsAsync(model.PhoneNumber, id))
            {
                ModelState.AddModelError(nameof(model.PhoneNumber), "Phone number is already registered.");
                ModelState.AddModelError(string.Empty, "Phone number is already registered.");
            }

            if (await _dbContext.Employees.AnyAsync(employee => employee.NationalIdNumber == model.NationalIdNumber && employee.IsActive && (!id.HasValue || employee.Id != id.Value)))
            {
                ModelState.AddModelError(nameof(model.NationalIdNumber), "National ID / CNIC is already registered.");
                ModelState.AddModelError(string.Empty, "National ID / CNIC is already registered.");
            }
        }

        private void NormalizeForm(EmployeeFormViewModel model)
        {
            model.FirstName = NormalizeRequired(model.FirstName);
            model.LastName = NormalizeRequired(model.LastName);
            model.Email = NormalizeEmail(model.Email) ?? string.Empty;
            model.PhoneNumber = NormalizePhone(model.PhoneNumber);
            model.NationalIdNumber = NationalIdValidator.TryNormalize(model.NationalIdNumber, "Pakistan", out var nationalId, out _)
                ? nationalId
                : NormalizeRequired(model.NationalIdNumber);
            model.Department = NormalizeRequired(model.Department);
            model.Designation = NormalizeRequired(model.Designation);
            model.EmploymentAddress = NormalizeOptional(model.EmploymentAddress);
            model.EmergencyContactName = NormalizeOptional(model.EmergencyContactName);
            model.EmergencyContactNumber = NormalizeOptional(NormalizePhone(model.EmergencyContactNumber));
            model.Notes = NormalizeOptional(model.Notes);
        }

        private static void MapToEntity(EmployeeFormViewModel model, Employee employee, bool isCreate)
        {
            employee.FirstName = model.FirstName;
            employee.LastName = model.LastName;
            employee.Email = model.Email;
            employee.PhoneNumber = model.PhoneNumber;
            employee.NationalIdNumber = model.NationalIdNumber;
            employee.DateOfBirth = model.DateOfBirth ?? DateOnly.FromDateTime(DateTime.UtcNow);
            employee.Gender = model.Gender;
            employee.Department = model.Department;
            employee.Designation = model.Designation;
            employee.EmploymentType = model.EmploymentType;
            employee.EmploymentStatus = model.EmploymentStatus;
            employee.JoiningDate = model.JoiningDate ?? DateOnly.FromDateTime(DateTime.UtcNow);
            employee.ReportingManagerId = model.ReportingManagerId;
            employee.EmploymentAddress = model.EmploymentAddress;
            employee.EmergencyContactName = model.EmergencyContactName;
            employee.EmergencyContactNumber = model.EmergencyContactNumber;
            employee.BasicSalary = model.BasicSalary;
            employee.Notes = model.Notes;
            if (isCreate)
            {
                employee.IsActive = true;
            }
        }

        private static EmployeeFormViewModel MapForm(Employee employee)
        {
            return new EmployeeFormViewModel
            {
                Id = employee.Id,
                EmployeeNumber = employee.EmployeeNumber,
                ExistingProfileImageUrl = employee.ProfileImageUrl,
                FirstName = employee.FirstName,
                LastName = employee.LastName,
                Email = employee.Email,
                PhoneNumber = employee.PhoneNumber,
                NationalIdNumber = employee.NationalIdNumber,
                DateOfBirth = employee.DateOfBirth,
                Gender = employee.Gender,
                Department = employee.Department,
                Designation = employee.Designation,
                EmploymentType = employee.EmploymentType,
                EmploymentStatus = employee.EmploymentStatus,
                JoiningDate = employee.JoiningDate,
                ReportingManagerId = employee.ReportingManagerId,
                EmploymentAddress = employee.EmploymentAddress,
                EmergencyContactName = employee.EmergencyContactName,
                EmergencyContactNumber = employee.EmergencyContactNumber,
                BasicSalary = employee.BasicSalary,
                Notes = employee.Notes
            };
        }

        private EmployeeDetailsViewModel MapDetails(Employee employee)
        {
            var manager = employee.ReportingManager;
            return new EmployeeDetailsViewModel
            {
                Id = employee.Id,
                EmployeeNumber = employee.EmployeeNumber,
                FullName = employee.FirstName + " " + employee.LastName,
                Initials = employee.FirstName.Substring(0, 1) + employee.LastName.Substring(0, 1),
                ProfileImageUrl = employee.ProfileImageUrl,
                Email = employee.Email,
                PhoneNumber = employee.PhoneNumber,
                NationalIdNumber = employee.NationalIdNumber,
                DateOfBirth = employee.DateOfBirth,
                CalculatedAge = CalculateAge(employee.DateOfBirth),
                Gender = employee.Gender,
                Department = employee.Department,
                Designation = employee.Designation,
                EmploymentType = employee.EmploymentType,
                EmploymentStatus = employee.EmploymentStatus,
                JoiningDate = employee.JoiningDate,
                ReportingManagerId = employee.ReportingManagerId,
                ReportingManagerName = manager != null ? manager.FirstName + " " + manager.LastName : null,
                EmploymentAddress = employee.EmploymentAddress,
                EmergencyContactName = employee.EmergencyContactName,
                EmergencyContactNumber = employee.EmergencyContactNumber,
                BasicSalary = employee.BasicSalary,
                Notes = employee.Notes,
                CreatedAt = employee.CreatedAt,
                UpdatedAt = employee.UpdatedAt,
                CreatedByName = employee.CreatedByUser != null
                    ? (employee.CreatedByUser.FirstName + " " + employee.CreatedByUser.LastName).Trim()
                    : null,
                CanManage = CanManage()
            };
        }

        private async Task<EmployeeFormViewModel> PopulateOptionsAsync(EmployeeFormViewModel model)
        {
            model.Departments = HRLists.Departments
                .Select(department => new SelectListItem(department, department))
                .ToList();
            model.Designations = HRLists.Designations
                .Select(designation => new SelectListItem(designation, designation))
                .ToList();

            // A hire-from-offer may carry a department / designation the fixed
            // lists do not contain; keep it selectable instead of dropping it.
            if (!string.IsNullOrWhiteSpace(model.Department)
                && !HRLists.Departments.Contains(model.Department))
            {
                model.Departments.Add(new SelectListItem(model.Department, model.Department));
            }

            if (!string.IsNullOrWhiteSpace(model.Designation)
                && !HRLists.Designations.Contains(model.Designation))
            {
                model.Designations.Add(new SelectListItem(model.Designation, model.Designation));
            }

            var managers = await _dbContext.Employees
                .AsNoTracking()
                .Where(employee => employee.IsActive && (!model.Id.HasValue || employee.Id != model.Id.Value))
                .OrderBy(employee => employee.FirstName)
                .ThenBy(employee => employee.LastName)
                .Select(employee => new
                {
                    employee.Id,
                    Name = (employee.FirstName + " " + employee.LastName).Trim(),
                    employee.EmployeeNumber
                })
                .ToListAsync();

            model.ReportingManagers = managers
                .Select(manager => new SelectListItem(
                    $"{manager.Name} ({manager.EmployeeNumber})",
                    manager.Id.ToString(),
                    manager.Id == model.ReportingManagerId))
                .ToList();

            return model;
        }

        /// <summary>
        /// Prefills the employee form from an accepted job offer so the hire
        /// action reuses the standard employee creation flow.
        /// </summary>
        private async Task ApplyOfferPrefillAsync(EmployeeFormViewModel model, int offerId)
        {
            var offer = await _dbContext.JobOffers
                .AsNoTracking()
                .Include(entity => entity.JobApplication)
                .ThenInclude(application => application!.JobPosting)
                .FirstOrDefaultAsync(entity => entity.Id == offerId && entity.IsActive);

            if (offer == null
                || offer.EmployeeId.HasValue
                || offer.Status != JobOfferStatus.Accepted
                || offer.JobApplication == null)
            {
                return;
            }

            var application = offer.JobApplication;
            model.OfferId = offer.Id;
            model.OfferReference = offer.ReferenceNumber;
            model.FirstName = application.FirstName;
            model.LastName = application.LastName;
            model.Email = application.Email;
            model.PhoneNumber = application.Phone;
            model.NationalIdNumber = application.NationalIdNumber;
            model.DateOfBirth = application.DateOfBirth;
            if (application.Gender.HasValue)
            {
                model.Gender = application.Gender.Value;
            }

            model.Department = offer.Department;
            model.Designation = offer.Designation;
            model.EmploymentType = offer.EmploymentType;
            model.EmploymentStatus = EmploymentStatus.Active;
            model.JoiningDate = offer.JoiningDate;
            model.ReportingManagerId = offer.ReportingManagerId;
            model.BasicSalary = offer.OfferedSalary;
            model.Notes = $"Hired through offer {offer.ReferenceNumber} (application {application.ApplicationNumber}).";
        }

        /// <summary>
        /// Links a newly created employee back to its accepted offer, closing
        /// the recruiting loop. Guards against duplicate hiring.
        /// </summary>
        private async Task LinkOfferAsync(int offerId, int employeeId)
        {
            var offer = await _dbContext.JobOffers
                .Include(entity => entity.JobApplication)
                .FirstOrDefaultAsync(entity => entity.Id == offerId && entity.IsActive);

            if (offer == null || offer.EmployeeId.HasValue || offer.Status != JobOfferStatus.Accepted)
            {
                return;
            }

            offer.EmployeeId = employeeId;
            offer.Status = JobOfferStatus.Hired;
            offer.JoiningRecordedAt = DateTime.UtcNow;
            offer.UpdatedAt = DateTime.UtcNow;

            if (offer.JobApplication != null)
            {
                offer.JobApplication.Status = JobApplicationStatus.Hired;
                offer.JobApplication.UpdatedAt = DateTime.UtcNow;
            }

            await _dbContext.SaveChangesAsync();
        }

        private IQueryable<Employee> ApplySorting(IQueryable<Employee> query, string sortBy)
        {
            return sortBy switch
            {
                "oldest" => query.OrderBy(employee => employee.CreatedAt),
                "name" => query.OrderBy(employee => employee.FirstName).ThenBy(employee => employee.LastName),
                "employee-id" => query.OrderBy(employee => employee.EmployeeNumber),
                "joining-date" => query.OrderByDescending(employee => employee.JoiningDate),
                _ => query.OrderByDescending(employee => employee.CreatedAt)
            };
        }

        private async Task<(bool Success, string? Url, string Message)> SaveProfileImageAsync(EmployeeFormViewModel model)
        {
            if (model.ProfileImage == null || model.ProfileImage.Length == 0) return (true, null, string.Empty);
            var extension = Path.GetExtension(model.ProfileImage.FileName).ToLowerInvariant();
            if (!AllowedImageExtensions.Contains(extension) || !AllowedImageContentTypes.Contains(model.ProfileImage.ContentType))
            {
                return (false, null, "Only JPG, PNG, and WEBP images are allowed.");
            }
            if (model.ProfileImage.Length > MaxImageSize)
            {
                return (false, null, "Profile photo cannot exceed 3 MB.");
            }

            var folder = Path.Combine(_environment.WebRootPath, "uploads", "employees");
            Directory.CreateDirectory(folder);
            var fileName = $"{Guid.NewGuid():N}{extension}";
            var fullPath = Path.Combine(folder, fileName);
            await using var stream = System.IO.File.Create(fullPath);
            await model.ProfileImage.CopyToAsync(stream);
            return (true, $"/uploads/employees/{fileName}", string.Empty);
        }

        private void DeleteProfileImage(string? url)
        {
            if (string.IsNullOrWhiteSpace(url) || !url.StartsWith("/uploads/employees/", StringComparison.OrdinalIgnoreCase)) return;
            var fileName = Path.GetFileName(url);
            var fullPath = Path.Combine(_environment.WebRootPath, "uploads", "employees", fileName);
            if (System.IO.File.Exists(fullPath)) System.IO.File.Delete(fullPath);
        }

        private Task<bool> PhoneExistsAsync(string? phone, int? id = null)
        {
            if (string.IsNullOrWhiteSpace(phone)) return Task.FromResult(false);
            return _dbContext.Employees.AnyAsync(employee =>
                employee.PhoneNumber == phone && employee.IsActive && (!id.HasValue || employee.Id != id.Value));
        }

        private IActionResult? EnsureCanManage() => CanManage() ? null : RedirectToAction("NotFound", "Error");

        private bool CanManage() =>
            RolePermissions.TryGetCurrentRole(User, out var role)
            && role is UserRole.SuperAdmin or UserRole.Admin or UserRole.StaffManager;

        private int CurrentUserId() => int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : 0;

        private static string NormalizeRequired(string? value) => value?.Trim() ?? string.Empty;

        private static string? NormalizeOptional(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

        private static string? NormalizeEmail(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim().ToLowerInvariant();

        private static string NormalizePhone(string? value)
        {
            if (string.IsNullOrWhiteSpace(value)) return string.Empty;
            var trimmed = value.Trim();
            var prefix = trimmed.StartsWith("+", StringComparison.Ordinal) ? "+" : string.Empty;
            return prefix + new string(trimmed.Where(char.IsDigit).ToArray());
        }

        private static bool HasReasonablePhoneDigits(string? value)
        {
            if (string.IsNullOrWhiteSpace(value)) return false;
            var digits = new string(value.Where(char.IsDigit).ToArray());
            return digits.Length is >= 7 and <= 15;
        }

        private static string NormalizeOption(string? value) => value?.Trim().ToLowerInvariant() ?? string.Empty;

        private static string NormalizeSort(string? value)
        {
            var allowed = new[] { "name", "newest", "oldest", "employee-id", "joining-date" };
            return allowed.Contains(value) ? value! : "newest";
        }

        private static int? CalculateAge(DateOnly? dateOfBirth)
        {
            if (!dateOfBirth.HasValue) return null;
            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            var age = today.Year - dateOfBirth.Value.Year;
            if (dateOfBirth.Value > today.AddYears(-age)) age--;
            return age;
        }
    }
}