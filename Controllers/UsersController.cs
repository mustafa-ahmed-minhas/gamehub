using GameHub.Data;
using GameHub.Filters;
using GameHub.Helpers;
using GameHub.Models.Entities;
using GameHub.Models.Enums;
using GameHub.Models.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using System.Text.RegularExpressions;

namespace GameHub.Controllers
{
    [GameHubAuthorize(UserRole.SuperAdmin)]
    [ValidateActiveUser]
    public class UsersController : Controller
    {
        private static readonly int[] AllowedPageSizes = { 10, 25, 50 };
        private readonly ApplicationDbContext _dbContext;

        public UsersController(ApplicationDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        [HttpGet]
        public async Task<IActionResult> Index(
            string? search,
            UserRole? role,
            string? status,
            string? department,
            string sortBy = "newest",
            string sortDirection = "desc",
            int page = 1,
            int pageSize = 10)
        {
            page = Math.Max(page, 1);
            pageSize = AllowedPageSizes.Contains(pageSize) ? pageSize : 10;
            sortBy = NormalizeSortBy(sortBy);
            sortDirection = sortDirection?.Equals("asc", StringComparison.OrdinalIgnoreCase) == true ? "asc" : "desc";
            status = NormalizeStatus(status);
            search = search?.Trim();
            department = string.IsNullOrWhiteSpace(department) ? null : department.Trim();

            var allUsers = _dbContext.Users.AsNoTracking();
            var totalUsers = await allUsers.CountAsync();
            var activeUsers = await allUsers.CountAsync(user => user.IsActive);
            var inactiveUsers = await allUsers.CountAsync(user => !user.IsActive);
            var adminLevelUsers = await allUsers.CountAsync(user => user.Role == UserRole.SuperAdmin || user.Role == UserRole.Admin);

            var query = allUsers;

            if (!string.IsNullOrWhiteSpace(search))
            {
                var normalizedSearch = search.ToLower();
                var searchDigits = new string(search.Where(char.IsDigit).ToArray());
                query = query.Where(user =>
                    user.FirstName.ToLower().Contains(normalizedSearch) ||
                    user.LastName.ToLower().Contains(normalizedSearch) ||
                    (user.FirstName + " " + user.LastName).ToLower().Contains(normalizedSearch) ||
                    user.Email.ToLower().Contains(normalizedSearch) ||
                    (user.PhoneNumber != null && user.PhoneNumber.ToLower().Contains(normalizedSearch)) ||
                    (user.CnicNumber != null && searchDigits.Length > 0 && user.CnicNumber.Contains(searchDigits)) ||
                    (user.Department != null && user.Department.ToLower().Contains(normalizedSearch)) ||
                    (user.Designation != null && user.Designation.ToLower().Contains(normalizedSearch)));
            }

            if (role.HasValue)
            {
                query = query.Where(user => user.Role == role.Value);
            }

            if (!string.IsNullOrWhiteSpace(department))
            {
                var normalizedDepartment = department.ToLower();
                query = query.Where(user => user.Department != null && user.Department.ToLower() == normalizedDepartment);
            }

            if (status == "active")
            {
                query = query.Where(user => user.IsActive);
            }
            else if (status == "inactive")
            {
                query = query.Where(user => !user.IsActive);
            }

            query = ApplySorting(query, sortBy, sortDirection);

            var totalRecords = await query.CountAsync();
            var totalPages = totalRecords == 0 ? 1 : (int)Math.Ceiling(totalRecords / (double)pageSize);
            page = Math.Min(page, totalPages);

            var users = await query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(user => new UserListItemViewModel
                {
                    Id = user.Id,
                    FullName = user.FirstName + " " + user.LastName,
                    Initials = user.FirstName.Substring(0, 1) + user.LastName.Substring(0, 1),
                    Email = user.Email,
                    PhoneNumber = user.PhoneNumber,
                    CnicNumber = user.CnicNumber,
                    Gender = user.Gender,
                    Department = user.Department,
                    Designation = user.Designation,
                    Role = user.Role,
                    IsActive = user.IsActive,
                    CreatedAt = user.CreatedAt,
                    LastLoginAt = user.LastLoginAt
                })
                .ToListAsync();

            var model = new UserIndexViewModel
            {
                Users = users,
                Search = search,
                Role = role,
                Status = status,
                Department = department,
                Departments = HRLists.Departments,
                SortBy = sortBy,
                SortDirection = sortDirection,
                CurrentPage = page,
                PageSize = pageSize,
                TotalRecords = totalRecords,
                TotalPages = totalPages,
                TotalUsers = totalUsers,
                ActiveUsers = activeUsers,
                InactiveUsers = inactiveUsers,
                AdminLevelUsers = adminLevelUsers
            };

            return View(model);
        }

        [HttpGet]
        public IActionResult Create()
        {
            return View("Form", new UserFormViewModel { IsActive = true });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(UserFormViewModel model)
        {
            NormalizeForm(model);
            ApplyNormalizedValidation(model);

            if (await EmailExistsAsync(model.Email))
            {
                ModelState.AddModelError(nameof(model.Email), "Email address is already in use.");
            }

            if (await PhoneExistsAsync(model.PhoneNumber))
            {
                ModelState.AddModelError(nameof(model.PhoneNumber), "Phone number is already in use.");
            }

            if (await CnicExistsAsync(model.CnicNumber))
            {
                ModelState.AddModelError(nameof(model.CnicNumber), "CNIC is already in use.");
            }

            if (!ModelState.IsValid)
            {
                return View("Form", model);
            }

            var user = new User
            {
                FirstName = model.FirstName,
                LastName = model.LastName,
                Email = model.Email,
                PhoneNumber = model.PhoneNumber,
                CnicNumber = model.CnicNumber,
                Gender = model.Gender,
                DateOfBirth = model.DateOfBirth,
                Address = model.Address,
                Department = model.Department,
                Designation = model.Designation,
                Role = model.Role,
                IsActive = model.IsActive,
                ProfileImageUrl = model.ExistingProfileImageUrl,
                CreatedAt = DateTime.UtcNow
            };

            user.PasswordHash = PasswordHelper.HashPassword(user, model.Password!);

            try
            {
                _dbContext.Users.Add(user);
                await _dbContext.SaveChangesAsync();
            }
            catch (DbUpdateException)
            {
                // A concurrent request may have registered the same email, phone, or CNIC first.
                _dbContext.Entry(user).State = EntityState.Detached;
                var conflictFound = false;
                if (await EmailExistsAsync(model.Email))
                {
                    ModelState.AddModelError(nameof(model.Email), "Email address is already in use.");
                    conflictFound = true;
                }

                if (await PhoneExistsAsync(model.PhoneNumber))
                {
                    ModelState.AddModelError(nameof(model.PhoneNumber), "Phone number is already in use.");
                    conflictFound = true;
                }

                if (await CnicExistsAsync(model.CnicNumber))
                {
                    ModelState.AddModelError(nameof(model.CnicNumber), "CNIC is already in use.");
                    conflictFound = true;
                }

                if (!conflictFound)
                {
                    ModelState.AddModelError(string.Empty, "The user could not be saved. Please review the details and try again.");
                }

                return View("Form", model);
            }

            TempData.SetToast("success", "Success", "User created successfully.");
            return RedirectToAction(nameof(Index));
        }

        [HttpGet]
        public async Task<IActionResult> Edit(int id)
        {
            var user = await _dbContext.Users.AsNoTracking().FirstOrDefaultAsync(item => item.Id == id);
            if (user == null)
            {
                TempData.SetToast("error", "Something Went Wrong", "User not found.");
                return NotFound();
            }

            var model = new UserFormViewModel
            {
                Id = user.Id,
                FirstName = user.FirstName,
                LastName = user.LastName,
                Email = user.Email,
                PhoneNumber = DigitsOnly(user.PhoneNumber),
                CnicNumber = user.CnicNumber,
                Gender = user.Gender,
                DateOfBirth = user.DateOfBirth,
                Address = user.Address,
                Department = user.Department,
                Designation = user.Designation,
                Role = user.Role,
                IsActive = user.IsActive,
                ExistingProfileImageUrl = user.ProfileImageUrl
            };

            return View("Form", model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, UserFormViewModel model)
        {
            if (model.Id != id)
            {
                return NotFound();
            }

            NormalizeForm(model);
            ApplyNormalizedValidation(model);

            if (await EmailExistsAsync(model.Email, id))
            {
                ModelState.AddModelError(nameof(model.Email), "Email address is already in use.");
            }

            if (await PhoneExistsAsync(model.PhoneNumber, id))
            {
                ModelState.AddModelError(nameof(model.PhoneNumber), "Phone number is already in use.");
            }

            if (await CnicExistsAsync(model.CnicNumber, id))
            {
                ModelState.AddModelError(nameof(model.CnicNumber), "CNIC is already in use.");
            }

            if (!ModelState.IsValid)
            {
                return View("Form", model);
            }

            var user = await _dbContext.Users.FirstOrDefaultAsync(item => item.Id == id);
            if (user == null)
            {
                TempData.SetToast("error", "Something Went Wrong", "User not found.");
                return NotFound();
            }

            if (IsFinalActiveSuperAdmin(user) && (model.Role != UserRole.SuperAdmin || !model.IsActive))
            {
                ModelState.AddModelError(string.Empty, "The last active Super Admin cannot be deactivated or moved to another role.");
                TempData.SetToast("warning", "Attention Required", "The last active Super Admin cannot be deactivated.");
                return View("Form", model);
            }

            if (IsCurrentUser(user.Id) && user.IsActive && !model.IsActive)
            {
                ModelState.AddModelError(string.Empty, "You cannot deactivate your own account while signed in.");
                TempData.SetToast("warning", "Attention Required", "You cannot deactivate your own account while signed in.");
                return View("Form", model);
            }

            user.FirstName = model.FirstName;
            user.LastName = model.LastName;
            user.Email = model.Email;
            user.PhoneNumber = model.PhoneNumber;
            user.CnicNumber = model.CnicNumber;
            user.Gender = model.Gender;
            user.DateOfBirth = model.DateOfBirth;
            user.Address = model.Address;
            user.Department = model.Department;
            user.Designation = model.Designation;
            user.Role = model.Role;
            user.IsActive = model.IsActive;
            user.ProfileImageUrl = model.ExistingProfileImageUrl;
            user.UpdatedAt = DateTime.UtcNow;

            if (!string.IsNullOrWhiteSpace(model.Password))
            {
                user.PasswordHash = PasswordHelper.HashPassword(user, model.Password);
            }

            try
            {
                await _dbContext.SaveChangesAsync();
            }
            catch (DbUpdateException)
            {
                // A concurrent request may have registered the same email, phone, or CNIC first.
                var conflictFound = false;
                if (await EmailExistsAsync(model.Email, id))
                {
                    ModelState.AddModelError(nameof(model.Email), "Email address is already in use.");
                    conflictFound = true;
                }

                if (await PhoneExistsAsync(model.PhoneNumber, id))
                {
                    ModelState.AddModelError(nameof(model.PhoneNumber), "Phone number is already in use.");
                    conflictFound = true;
                }

                if (await CnicExistsAsync(model.CnicNumber, id))
                {
                    ModelState.AddModelError(nameof(model.CnicNumber), "CNIC is already in use.");
                    conflictFound = true;
                }

                if (!conflictFound)
                {
                    ModelState.AddModelError(string.Empty, "The user could not be saved. Please review the details and try again.");
                }

                return View("Form", model);
            }

            TempData.SetToast("success", "Success", "User updated successfully.");
            return RedirectToAction(nameof(Details), new { id = user.Id });
        }

        [HttpGet]
        public async Task<IActionResult> Details(int id)
        {
            var model = await _dbContext.Users
                .AsNoTracking()
                .Where(user => user.Id == id)
                .Select(user => new UserDetailsViewModel
                {
                    Id = user.Id,
                    FirstName = user.FirstName,
                    LastName = user.LastName,
                    FullName = user.FirstName + " " + user.LastName,
                    Initials = user.FirstName.Substring(0, 1) + user.LastName.Substring(0, 1),
                    Email = user.Email,
                    PhoneNumber = user.PhoneNumber,
                    CnicNumber = user.CnicNumber,
                    Gender = user.Gender,
                    DateOfBirth = user.DateOfBirth,
                    Address = user.Address,
                    Department = user.Department,
                    Designation = user.Designation,
                    Role = user.Role,
                    IsActive = user.IsActive,
                    ProfileImageUrl = user.ProfileImageUrl,
                    CreatedAt = user.CreatedAt,
                    UpdatedAt = user.UpdatedAt,
                    LastLoginAt = user.LastLoginAt
                })
                .FirstOrDefaultAsync();

            if (model == null)
            {
                TempData.SetToast("error", "Something Went Wrong", "User not found.");
                return NotFound();
            }

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleStatus(int id)
        {
            var user = await _dbContext.Users.FirstOrDefaultAsync(item => item.Id == id);
            if (user == null)
            {
                return Json(new { success = false, message = "User not found" });
            }

            if (user.IsActive && IsCurrentUser(user.Id))
            {
                return Json(new { success = false, message = "You cannot deactivate your own account while signed in." });
            }

            if (user.IsActive && user.Role == UserRole.SuperAdmin)
            {
                var activeSuperAdmins = await _dbContext.Users.CountAsync(item => item.Role == UserRole.SuperAdmin && item.IsActive);
                if (activeSuperAdmins <= 1)
                {
                    return Json(new { success = false, message = "The last active Super Admin cannot be deactivated" });
                }
            }

            user.IsActive = !user.IsActive;
            user.UpdatedAt = DateTime.UtcNow;
            await _dbContext.SaveChangesAsync();

            var message = user.IsActive ? "User activated successfully" : "User deactivated successfully";
            TempData.SetToast("success", "Success", $"{message}.");

            return Json(new
            {
                success = true,
                isActive = user.IsActive,
                message,
                statusText = user.IsActive ? "Active" : "Inactive"
            });
        }

        [HttpGet]
        public async Task<IActionResult> CheckEmail(string? email, int? id)
        {
            email = NormalizeEmail(email);
            if (string.IsNullOrWhiteSpace(email) || !Regex.IsMatch(email, @"^[^@\s]+@[^@\s]+\.com$"))
            {
                return Json(new { available = false, message = "Email must be valid and end with .com" });
            }

            var exists = await EmailExistsAsync(email, id);
            return Json(new
            {
                available = !exists,
                message = exists ? "Email is already in use" : "Email is available"
            });
        }

        private static IQueryable<User> ApplySorting(IQueryable<User> query, string sortBy, string sortDirection)
        {
            var ascending = sortDirection == "asc";

            return sortBy switch
            {
                "name" => ascending
                    ? query.OrderBy(user => user.FirstName).ThenBy(user => user.LastName)
                    : query.OrderByDescending(user => user.FirstName).ThenByDescending(user => user.LastName),
                "email" => ascending
                    ? query.OrderBy(user => user.Email)
                    : query.OrderByDescending(user => user.Email),
                "role" => ascending
                    ? query.OrderBy(user => user.Role)
                    : query.OrderByDescending(user => user.Role),
                "oldest" => query.OrderBy(user => user.CreatedAt),
                "status" => ascending
                    ? query.OrderBy(user => user.IsActive).ThenBy(user => user.FirstName)
                    : query.OrderByDescending(user => user.IsActive).ThenBy(user => user.FirstName),
                _ => query.OrderByDescending(user => user.CreatedAt)
            };
        }

        private static string NormalizeSortBy(string? sortBy)
        {
            return sortBy?.ToLowerInvariant() switch
            {
                "name" => "name",
                "email" => "email",
                "role" => "role",
                "oldest" => "oldest",
                "status" => "status",
                _ => "newest"
            };
        }

        private static string? NormalizeStatus(string? status)
        {
            return status?.ToLowerInvariant() switch
            {
                "active" => "active",
                "inactive" => "inactive",
                _ => null
            };
        }

        private static string NormalizeEmail(string? email)
        {
            return email?.Trim().ToLowerInvariant() ?? string.Empty;
        }

        private static string DigitsOnly(string? value)
        {
            return string.IsNullOrWhiteSpace(value) ? string.Empty : new string(value.Where(char.IsDigit).ToArray());
        }

        private static void NormalizeForm(UserFormViewModel model)
        {
            model.FirstName = model.FirstName?.Trim() ?? string.Empty;
            model.LastName = model.LastName?.Trim() ?? string.Empty;
            model.Email = NormalizeEmail(model.Email);
            model.PhoneNumber = DigitsOnly(model.PhoneNumber);
            model.CnicNumber = DigitsOnly(model.CnicNumber);
            model.Address = string.IsNullOrWhiteSpace(model.Address) ? null : model.Address.Trim();
            model.Department = string.IsNullOrWhiteSpace(model.Department) ? null : model.Department.Trim();
            model.Designation = string.IsNullOrWhiteSpace(model.Designation) ? null : model.Designation.Trim();
        }

        /// <summary>
        /// Re-validates the normalized values of fields whose DataAnnotations ran against the raw
        /// posted input (e.g., a phone typed as "0300-1234567" is normalized to "03001234567").
        /// </summary>
        private void ApplyNormalizedValidation(UserFormViewModel model)
        {
            ModelState.Remove(nameof(model.FirstName));
            if (string.IsNullOrWhiteSpace(model.FirstName))
            {
                ModelState.AddModelError(nameof(model.FirstName), "First name is required.");
            }
            else if (!Regex.IsMatch(model.FirstName, @"^[A-Za-z ]+$"))
            {
                ModelState.AddModelError(nameof(model.FirstName), "First name can contain alphabetic characters and spaces only.");
            }

            ModelState.Remove(nameof(model.LastName));
            if (string.IsNullOrWhiteSpace(model.LastName))
            {
                ModelState.AddModelError(nameof(model.LastName), "Last name is required.");
            }
            else if (!Regex.IsMatch(model.LastName, @"^[A-Za-z ]+$"))
            {
                ModelState.AddModelError(nameof(model.LastName), "Last name can contain alphabetic characters and spaces only.");
            }

            ModelState.Remove(nameof(model.PhoneNumber));
            if (string.IsNullOrWhiteSpace(model.PhoneNumber))
            {
                ModelState.AddModelError(nameof(model.PhoneNumber), "Phone number is required.");
            }
            else if (!Regex.IsMatch(model.PhoneNumber, @"^0\d{10}$"))
            {
                ModelState.AddModelError(nameof(model.PhoneNumber), "Phone number must be exactly 11 digits and start with 0 (e.g., 03001234567).");
            }

            ModelState.Remove(nameof(model.CnicNumber));
            if (string.IsNullOrWhiteSpace(model.CnicNumber))
            {
                ModelState.AddModelError(nameof(model.CnicNumber), "CNIC is required.");
            }
            else if (!Regex.IsMatch(model.CnicNumber, @"^\d{13}$"))
            {
                ModelState.AddModelError(nameof(model.CnicNumber), "CNIC must be exactly 13 digits without hyphens (e.g., 3520212345678).");
            }
        }

        private Task<bool> EmailExistsAsync(string email, int? exceptId = null)
        {
            return _dbContext.Users.AnyAsync(user => user.Email == email && (!exceptId.HasValue || user.Id != exceptId.Value));
        }

        private Task<bool> PhoneExistsAsync(string? phoneNumber, int? exceptId = null)
        {
            if (string.IsNullOrWhiteSpace(phoneNumber))
            {
                return Task.FromResult(false);
            }

            return _dbContext.Users.AnyAsync(user => user.PhoneNumber == phoneNumber && (!exceptId.HasValue || user.Id != exceptId.Value));
        }

        private Task<bool> CnicExistsAsync(string? cnicNumber, int? exceptId = null)
        {
            if (string.IsNullOrWhiteSpace(cnicNumber))
            {
                return Task.FromResult(false);
            }

            return _dbContext.Users.AnyAsync(user => user.CnicNumber == cnicNumber && (!exceptId.HasValue || user.Id != exceptId.Value));
        }

        private bool IsCurrentUser(int userId)
        {
            return int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var currentUserId) &&
                   currentUserId == userId;
        }

        private bool IsFinalActiveSuperAdmin(User user)
        {
            if (!user.IsActive || user.Role != UserRole.SuperAdmin)
            {
                return false;
            }

            return _dbContext.Users.Count(item => item.Role == UserRole.SuperAdmin && item.IsActive) <= 1;
        }
    }
}
