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
    [GameHubAuthorize(UserRole.SuperAdmin, UserRole.Admin, UserRole.BookingManager, UserRole.Receptionist, UserRole.FinanceManager, UserRole.Viewer)]
    [ValidateActiveUser]
    public class CustomersController : Controller
    {
        private static readonly int[] PageSizes = { 10, 25, 50 };
        private static readonly string[] AllowedImageExtensions = { ".jpg", ".jpeg", ".png", ".webp" };
        private static readonly string[] AllowedImageContentTypes = { "image/jpeg", "image/png", "image/webp" };
        private const long MaxImageSize = 3 * 1024 * 1024;

        private readonly ApplicationDbContext _dbContext;
        private readonly ICustomerCodeService _customerCodeService;
        private readonly IWebHostEnvironment _environment;

        public CustomersController(ApplicationDbContext dbContext, ICustomerCodeService customerCodeService, IWebHostEnvironment environment)
        {
            _dbContext = dbContext;
            _customerCodeService = customerCodeService;
            _environment = environment;
        }

        [HttpGet]
        public async Task<IActionResult> Index(string? search, CustomerType? customerType, CustomerSource? source, string? status, string? membership, string? blacklist, string? city, string sortBy = "newest", string sortDirection = "desc", int page = 1, int pageSize = 10)
        {
            page = Math.Max(page, 1);
            pageSize = PageSizes.Contains(pageSize) ? pageSize : 10;
            search = search?.Trim();
            status = NormalizeOption(status);
            membership = NormalizeOption(membership);
            blacklist = NormalizeOption(blacklist);
            city = string.IsNullOrWhiteSpace(city) ? null : city.Trim();
            sortBy = NormalizeSort(sortBy);
            sortDirection = sortDirection?.Equals("asc", StringComparison.OrdinalIgnoreCase) == true ? "asc" : "desc";

            var allCustomers = _dbContext.Customers.AsNoTracking();
            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            var summaryTask = new
            {
                Total = await allCustomers.CountAsync(),
                Active = await allCustomers.CountAsync(x => x.IsActive),
                Members = await allCustomers.CountAsync(x => x.IsMember),
                Vip = await allCustomers.CountAsync(x => x.CustomerType == CustomerType.VIP),
                Blacklisted = await allCustomers.CountAsync(x => x.IsBlacklisted),
                Balance = await allCustomers.SumAsync(x => x.OutstandingBalance)
            };

            var cities = await allCustomers
                .Where(x => x.City != null && x.City != "")
                .Select(x => x.City!)
                .Distinct()
                .OrderBy(x => x)
                .ToListAsync();

            var query = allCustomers;

            if (!string.IsNullOrWhiteSpace(search))
            {
                var normalized = search.ToLower();
                query = query.Where(x =>
                    x.CustomerCode.ToLower().Contains(normalized) ||
                    x.FirstName.ToLower().Contains(normalized) ||
                    x.LastName.ToLower().Contains(normalized) ||
                    (x.FirstName + " " + x.LastName).ToLower().Contains(normalized) ||
                    (x.PreferredName != null && x.PreferredName.ToLower().Contains(normalized)) ||
                    x.PrimaryPhone.ToLower().Contains(normalized) ||
                    (x.SecondaryPhone != null && x.SecondaryPhone.ToLower().Contains(normalized)) ||
                    (x.WhatsAppNumber != null && x.WhatsAppNumber.ToLower().Contains(normalized)) ||
                    (x.Email != null && x.Email.ToLower().Contains(normalized)) ||
                    (x.NationalIdNumber != null && x.NationalIdNumber.ToLower().Contains(normalized)) ||
                    (x.OrganizationName != null && x.OrganizationName.ToLower().Contains(normalized)) ||
                    (x.City != null && x.City.ToLower().Contains(normalized)) ||
                    (x.CustomerTags != null && x.CustomerTags.ToLower().Contains(normalized)));
            }

            if (customerType.HasValue) query = query.Where(x => x.CustomerType == customerType.Value);
            if (source.HasValue) query = query.Where(x => x.CustomerSource == source.Value);
            if (status == "active") query = query.Where(x => x.IsActive);
            if (status == "inactive") query = query.Where(x => !x.IsActive);
            if (membership == "members") query = query.Where(x => x.IsMember);
            if (membership == "non-members") query = query.Where(x => !x.IsMember);
            if (membership == "expired") query = query.Where(x => x.IsMember && x.MembershipExpiryDate != null && x.MembershipExpiryDate < today);
            if (blacklist == "blacklisted") query = query.Where(x => x.IsBlacklisted);
            if (blacklist == "not-blacklisted") query = query.Where(x => !x.IsBlacklisted);
            if (!string.IsNullOrWhiteSpace(city)) query = query.Where(x => x.City == city);

            query = ApplySorting(query, sortBy, sortDirection);

            var totalRecords = await query.CountAsync();
            var totalPages = totalRecords == 0 ? 1 : (int)Math.Ceiling(totalRecords / (double)pageSize);
            page = Math.Min(page, totalPages);

            var customers = await query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(x => new CustomerListItemViewModel
                {
                    Id = x.Id,
                    CustomerCode = x.CustomerCode,
                    FullName = x.FirstName + " " + x.LastName,
                    Initials = x.FirstName.Substring(0, 1) + x.LastName.Substring(0, 1),
                    PrimaryPhone = x.PrimaryPhone,
                    WhatsAppNumber = x.WhatsAppNumber,
                    Email = x.Email,
                    City = x.City,
                    CustomerType = x.CustomerType,
                    CustomerSource = x.CustomerSource,
                    IsMember = x.IsMember,
                    MembershipExpiryDate = x.MembershipExpiryDate,
                    LoyaltyPoints = x.LoyaltyPoints,
                    OutstandingBalance = x.OutstandingBalance,
                    IsBlacklisted = x.IsBlacklisted,
                    IsActive = x.IsActive,
                    CreatedAt = x.CreatedAt,
                    LastVisitAt = x.LastVisitAt,
                    ProfileImageUrl = x.ProfileImageUrl
                })
                .ToListAsync();

            return View(new CustomerIndexViewModel
            {
                Customers = customers,
                Search = search,
                CustomerType = customerType,
                CustomerSource = source,
                Status = status,
                Membership = membership,
                Blacklist = blacklist,
                City = city,
                SortBy = sortBy,
                SortDirection = sortDirection,
                CurrentPage = page,
                PageSize = pageSize,
                TotalRecords = totalRecords,
                TotalPages = totalPages,
                TotalCustomers = summaryTask.Total,
                ActiveCustomers = summaryTask.Active,
                Members = summaryTask.Members,
                VipCustomers = summaryTask.Vip,
                BlacklistedCustomers = summaryTask.Blacklisted,
                OutstandingBalance = summaryTask.Balance,
                Cities = cities,
                CanManage = CanManage()
            });
        }

        [HttpGet]
        public async Task<IActionResult> Create()
        {
            if (EnsureCanManage() is IActionResult blocked) return blocked;
            return View("Form", await AddSportsAsync(new CustomerFormViewModel()));
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(CustomerFormViewModel model)
        {
            if (EnsureCanManage() is IActionResult blocked) return blocked;
            await PrepareAndValidateAsync(model);

            if (!ModelState.IsValid)
            {
                return View("Form", await AddSportsAsync(model));
            }

            await using var transaction = await _dbContext.Database.BeginTransactionAsync();
            var customer = new Customer();
            MapToCustomer(model, customer, isCreate: true);
            customer.CustomerCode = await _customerCodeService.GenerateNextCodeAsync();
            customer.CreatedAt = DateTime.UtcNow;
            customer.OutstandingBalance = 0;

            var imageResult = await SaveProfileImageAsync(model);
            if (!imageResult.Success)
            {
                ModelState.AddModelError(nameof(model.ProfileImage), imageResult.Message);
                TempData.SetToast("error", "Upload Failed", "Profile image could not be uploaded.");
                return View("Form", await AddSportsAsync(model));
            }
            customer.ProfileImageUrl = imageResult.Url;

            _dbContext.Customers.Add(customer);
            await _dbContext.SaveChangesAsync();
            await transaction.CommitAsync();

            TempData.SetToast("success", "Success", "Customer created successfully.");
            return RedirectToAction(nameof(Details), new { id = customer.Id });
        }

        [HttpGet]
        public async Task<IActionResult> Edit(int id)
        {
            if (EnsureCanManage() is IActionResult blocked) return blocked;
            var customer = await _dbContext.Customers.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
            if (customer == null)
            {
                TempData.SetToast("error", "Not Found", "Customer not found.");
                return NotFound();
            }

            return View("Form", await AddSportsAsync(MapForm(customer)));
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, CustomerFormViewModel model)
        {
            if (EnsureCanManage() is IActionResult blocked) return blocked;
            if (model.Id != id) return NotFound();
            await PrepareAndValidateAsync(model, id);

            var customer = await _dbContext.Customers.FirstOrDefaultAsync(x => x.Id == id);
            if (customer == null)
            {
                TempData.SetToast("error", "Not Found", "Customer not found.");
                return NotFound();
            }

            if (!ModelState.IsValid)
            {
                return View("Form", await AddSportsAsync(model));
            }

            var previousImage = customer.ProfileImageUrl;
            var imageResult = await SaveProfileImageAsync(model);
            if (!imageResult.Success)
            {
                ModelState.AddModelError(nameof(model.ProfileImage), imageResult.Message);
                TempData.SetToast("error", "Upload Failed", "Profile image could not be uploaded.");
                return View("Form", await AddSportsAsync(model));
            }

            MapToCustomer(model, customer, isCreate: false);
            if (model.RemoveProfileImage)
            {
                customer.ProfileImageUrl = null;
            }
            else if (!string.IsNullOrWhiteSpace(imageResult.Url))
            {
                customer.ProfileImageUrl = imageResult.Url;
            }
            else
            {
                customer.ProfileImageUrl = previousImage;
            }
            customer.UpdatedAt = DateTime.UtcNow;

            await _dbContext.SaveChangesAsync();
            if (!string.IsNullOrWhiteSpace(imageResult.Url) || model.RemoveProfileImage)
            {
                DeleteProfileImage(previousImage);
            }

            TempData.SetToast("success", "Success", "Customer updated successfully.");
            return RedirectToAction(nameof(Details), new { id });
        }

        [HttpGet]
        public async Task<IActionResult> Details(int id)
        {
            var customer = await _dbContext.Customers.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
            if (customer == null)
            {
                TempData.SetToast("error", "Not Found", "Customer not found.");
                return NotFound();
            }

            ViewBag.OpenLeads = await _dbContext.Leads.AsNoTracking().CountAsync(x => x.CustomerId == id && x.IsActive && x.Status != LeadStatus.Converted && x.Status != LeadStatus.Disqualified && x.Status != LeadStatus.Lost);
            ViewBag.Opportunities = await _dbContext.Opportunities.AsNoTracking().CountAsync(x => x.CustomerId == id && x.IsActive);
            ViewBag.LastInquiry = await _dbContext.Leads.AsNoTracking().Where(x => x.CustomerId == id).OrderByDescending(x => x.CreatedAt).Select(x => x.CreatedAt).FirstOrDefaultAsync();
            ViewBag.PipelineValue = await _dbContext.Opportunities.AsNoTracking().Where(x => x.CustomerId == id && x.IsActive).SumAsync(x => x.ExpectedValue);
            return View(MapDetails(customer));
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleStatus(int id)
        {
            if (EnsureCanManage() is IActionResult blocked) return blocked;
            var customer = await _dbContext.Customers.FirstOrDefaultAsync(x => x.Id == id);
            if (customer == null) return Json(new { success = false, message = "Customer not found." });
            customer.IsActive = !customer.IsActive;
            customer.UpdatedAt = DateTime.UtcNow;
            await _dbContext.SaveChangesAsync();
            var message = customer.IsActive ? "Customer activated successfully." : "Customer deactivated successfully.";
            return Json(new { success = true, isActive = customer.IsActive, statusText = customer.IsActive ? "Active" : "Inactive", message });
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleBlacklist(int id, string? reason)
        {
            if (EnsureCanManage() is IActionResult blocked) return blocked;
            var customer = await _dbContext.Customers.FirstOrDefaultAsync(x => x.Id == id);
            if (customer == null) return Json(new { success = false, message = "Customer not found." });

            if (!customer.IsBlacklisted && (string.IsNullOrWhiteSpace(reason) || reason.Trim().Length < 10))
            {
                return Json(new { success = false, message = "Blacklist reason must be at least 10 characters." });
            }

            customer.IsBlacklisted = !customer.IsBlacklisted;
            customer.BlacklistReason = customer.IsBlacklisted ? reason?.Trim() : null;
            customer.UpdatedAt = DateTime.UtcNow;
            await _dbContext.SaveChangesAsync();
            var message = customer.IsBlacklisted ? "Customer added to blacklist." : "Customer removed from blacklist.";
            return Json(new { success = true, isBlacklisted = customer.IsBlacklisted, message });
        }

        [HttpGet]
        public async Task<IActionResult> CheckEmail(string? email, int? id)
        {
            email = NormalizeEmail(email);
            if (string.IsNullOrWhiteSpace(email)) return Json(new { available = true, message = "Email is optional." });
            if (!new System.ComponentModel.DataAnnotations.EmailAddressAttribute().IsValid(email)) return Json(new { available = false, message = "Invalid email format." });
            var exists = await _dbContext.Customers.AnyAsync(x => x.Email == email && (!id.HasValue || x.Id != id.Value));
            return Json(new { available = !exists, message = exists ? "Email address is already registered." : "Email is available." });
        }

        [HttpGet]
        public async Task<IActionResult> CheckPhone(string? phone, int? id, string? field)
        {
            var normalized = NormalizePhone(phone);
            if (!HasReasonablePhoneDigits(normalized)) return Json(new { available = false, message = "Invalid phone number." });
            var exists = await PhoneExistsAsync(normalized, id);
            return Json(new { available = !exists, message = exists ? "Phone number is already registered." : "Phone number is available." });
        }

        [HttpGet]
        public async Task<IActionResult> CheckNationalId(string? nationalId, int? id)
        {
            nationalId = NormalizeOptional(nationalId);
            if (string.IsNullOrWhiteSpace(nationalId)) return Json(new { available = true, message = "National ID is optional." });
            var exists = await _dbContext.Customers.AnyAsync(x => x.NationalIdNumber == nationalId && (!id.HasValue || x.Id != id.Value));
            return Json(new { available = !exists, message = exists ? "National ID is already registered." : "National ID is available." });
        }

        private async Task PrepareAndValidateAsync(CustomerFormViewModel model, int? id = null)
        {
            NormalizeForm(model);

            if (!string.IsNullOrWhiteSpace(model.Email) && await _dbContext.Customers.AnyAsync(x => x.Email == model.Email && (!id.HasValue || x.Id != id.Value)))
            {
                ModelState.AddModelError(nameof(model.Email), "Email address is already registered.");
                ModelState.AddModelError(string.Empty, "Email address is already registered.");
            }

            if (await PhoneExistsAsync(model.PrimaryPhone, id))
            {
                ModelState.AddModelError(nameof(model.PrimaryPhone), "Phone number is already registered.");
                ModelState.AddModelError(string.Empty, "Phone number is already registered.");
            }

            if (!string.IsNullOrWhiteSpace(model.WhatsAppNumber) && await PhoneExistsAsync(model.WhatsAppNumber, id))
            {
                ModelState.AddModelError(nameof(model.WhatsAppNumber), "WhatsApp number is already registered.");
            }

            if (!string.IsNullOrWhiteSpace(model.NationalIdNumber) && await _dbContext.Customers.AnyAsync(x => x.NationalIdNumber == model.NationalIdNumber && (!id.HasValue || x.Id != id.Value)))
            {
                ModelState.AddModelError(nameof(model.NationalIdNumber), "National ID is already registered.");
                ModelState.AddModelError(string.Empty, "National ID is already registered.");
            }
        }

        private void NormalizeForm(CustomerFormViewModel model)
        {
            model.FirstName = NormalizeRequired(model.FirstName);
            model.LastName = NormalizeRequired(model.LastName);
            model.PreferredName = NormalizeOptional(model.PreferredName);
            model.NationalIdNumber = NormalizeOptional(model.NationalIdNumber)?.ToUpperInvariant();
            model.PrimaryPhone = NormalizePhone(model.PrimaryPhone);
            model.SecondaryPhone = NormalizeOptional(NormalizePhone(model.SecondaryPhone));
            model.WhatsAppNumber = NormalizeOptional(NormalizePhone(model.WhatsAppNumber));
            model.Email = NormalizeEmail(model.Email);
            model.AddressLine1 = NormalizeOptional(model.AddressLine1);
            model.AddressLine2 = NormalizeOptional(model.AddressLine2);
            model.City = NormalizeOptional(model.City);
            model.StateProvince = NormalizeOptional(model.StateProvince);
            model.PostalCode = NormalizeOptional(model.PostalCode);
            model.Country = string.IsNullOrWhiteSpace(model.Country) ? "Pakistan" : model.Country.Trim();
            model.EmergencyContactName = NormalizeOptional(model.EmergencyContactName);
            model.EmergencyContactPhone = NormalizeOptional(NormalizePhone(model.EmergencyContactPhone));
            model.EmergencyContactRelation = NormalizeOptional(model.EmergencyContactRelation);
            model.OrganizationName = NormalizeOptional(model.OrganizationName);
            model.Occupation = NormalizeOptional(model.Occupation);
            model.MembershipNumber = NormalizeOptional(model.MembershipNumber)?.ToUpperInvariant();
            model.BlacklistReason = NormalizeOptional(model.BlacklistReason);
            model.InternalNotes = NormalizeOptional(model.InternalNotes);
            model.CustomerTags = NormalizeCsv(model.CustomerTags);
            model.SelectedSportCodes = model.SelectedSportCodes.Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x.Trim().ToUpperInvariant()).Distinct().ToList();
            if (!model.AllowCredit) model.CreditLimit = 0;
            if (!model.IsMember)
            {
                model.MembershipNumber = null;
                model.MembershipStartDate = null;
                model.MembershipExpiryDate = null;
            }
            if (!model.IsBlacklisted) model.BlacklistReason = null;
        }

        private void MapToCustomer(CustomerFormViewModel model, Customer customer, bool isCreate)
        {
            customer.FirstName = model.FirstName;
            customer.LastName = model.LastName;
            customer.PreferredName = model.PreferredName;
            customer.NationalIdNumber = model.NationalIdNumber;
            customer.DateOfBirth = model.DateOfBirth;
            customer.Gender = model.Gender;
            customer.PrimaryPhone = model.PrimaryPhone;
            customer.SecondaryPhone = model.SecondaryPhone;
            customer.WhatsAppNumber = model.WhatsAppNumber;
            customer.Email = model.Email;
            customer.AddressLine1 = model.AddressLine1;
            customer.AddressLine2 = model.AddressLine2;
            customer.City = model.City;
            customer.StateProvince = model.StateProvince;
            customer.PostalCode = model.PostalCode;
            customer.Country = model.Country;
            customer.EmergencyContactName = model.EmergencyContactName;
            customer.EmergencyContactPhone = model.EmergencyContactPhone;
            customer.EmergencyContactRelation = model.EmergencyContactRelation;
            customer.CustomerType = model.CustomerType;
            customer.CustomerSource = model.CustomerSource;
            customer.OrganizationName = model.OrganizationName;
            customer.Occupation = model.Occupation;
            customer.PreferredSportCodes = string.Join(",", model.SelectedSportCodes);
            customer.PreferredContactMethod = model.PreferredContactMethod;
            customer.IsMember = model.IsMember;
            customer.MembershipNumber = model.MembershipNumber;
            customer.MembershipStartDate = model.MembershipStartDate;
            customer.MembershipExpiryDate = model.MembershipExpiryDate;
            customer.CreditLimit = model.CreditLimit;
            if (isCreate) customer.OutstandingBalance = 0;
            customer.AllowCredit = model.AllowCredit;
            customer.LoyaltyPoints = model.LoyaltyPoints;
            customer.ReceiveMarketingMessages = model.ReceiveMarketingMessages;
            customer.ReceiveBookingReminders = model.ReceiveBookingReminders;
            customer.IsBlacklisted = model.IsBlacklisted;
            customer.BlacklistReason = model.BlacklistReason;
            customer.InternalNotes = model.InternalNotes;
            customer.CustomerTags = model.CustomerTags;
            customer.IsActive = model.IsActive;
        }

        private CustomerFormViewModel MapForm(Customer customer)
        {
            return new CustomerFormViewModel
            {
                Id = customer.Id,
                CustomerCode = customer.CustomerCode,
                FirstName = customer.FirstName,
                LastName = customer.LastName,
                PreferredName = customer.PreferredName,
                ExistingProfileImageUrl = customer.ProfileImageUrl,
                NationalIdNumber = customer.NationalIdNumber,
                DateOfBirth = customer.DateOfBirth,
                Gender = customer.Gender,
                PrimaryPhone = customer.PrimaryPhone,
                SecondaryPhone = customer.SecondaryPhone,
                WhatsAppNumber = customer.WhatsAppNumber,
                Email = customer.Email,
                AddressLine1 = customer.AddressLine1,
                AddressLine2 = customer.AddressLine2,
                City = customer.City,
                StateProvince = customer.StateProvince,
                PostalCode = customer.PostalCode,
                Country = customer.Country,
                EmergencyContactName = customer.EmergencyContactName,
                EmergencyContactPhone = customer.EmergencyContactPhone,
                EmergencyContactRelation = customer.EmergencyContactRelation,
                CustomerType = customer.CustomerType,
                CustomerSource = customer.CustomerSource,
                OrganizationName = customer.OrganizationName,
                Occupation = customer.Occupation,
                SelectedSportCodes = SplitCsv(customer.PreferredSportCodes),
                PreferredContactMethod = customer.PreferredContactMethod,
                IsMember = customer.IsMember,
                MembershipNumber = customer.MembershipNumber,
                MembershipStartDate = customer.MembershipStartDate,
                MembershipExpiryDate = customer.MembershipExpiryDate,
                CreditLimit = customer.CreditLimit,
                OutstandingBalance = customer.OutstandingBalance,
                AllowCredit = customer.AllowCredit,
                LoyaltyPoints = customer.LoyaltyPoints,
                ReceiveMarketingMessages = customer.ReceiveMarketingMessages,
                ReceiveBookingReminders = customer.ReceiveBookingReminders,
                IsBlacklisted = customer.IsBlacklisted,
                BlacklistReason = customer.BlacklistReason,
                InternalNotes = customer.InternalNotes,
                CustomerTags = customer.CustomerTags,
                IsActive = customer.IsActive
            };
        }

        private CustomerDetailsViewModel MapDetails(Customer customer)
        {
            return new CustomerDetailsViewModel
            {
                Id = customer.Id,
                CustomerCode = customer.CustomerCode,
                FullName = customer.FullName,
                Initials = customer.Initials,
                PreferredName = customer.PreferredName,
                ProfileImageUrl = customer.ProfileImageUrl,
                NationalIdNumber = customer.NationalIdNumber,
                DateOfBirth = customer.DateOfBirth,
                CalculatedAge = CalculateAge(customer.DateOfBirth),
                Gender = customer.Gender,
                PrimaryPhone = customer.PrimaryPhone,
                SecondaryPhone = customer.SecondaryPhone,
                WhatsAppNumber = customer.WhatsAppNumber,
                Email = customer.Email,
                AddressLine1 = customer.AddressLine1,
                AddressLine2 = customer.AddressLine2,
                City = customer.City,
                StateProvince = customer.StateProvince,
                PostalCode = customer.PostalCode,
                Country = customer.Country,
                EmergencyContactName = customer.EmergencyContactName,
                EmergencyContactPhone = customer.EmergencyContactPhone,
                EmergencyContactRelation = customer.EmergencyContactRelation,
                CustomerType = customer.CustomerType,
                CustomerSource = customer.CustomerSource,
                OrganizationName = customer.OrganizationName,
                Occupation = customer.Occupation,
                PreferredSportCodes = customer.PreferredSportCodes,
                PreferredContactMethod = customer.PreferredContactMethod,
                IsMember = customer.IsMember,
                MembershipStatus = customer.MembershipStatus,
                MembershipNumber = customer.MembershipNumber,
                MembershipStartDate = customer.MembershipStartDate,
                MembershipExpiryDate = customer.MembershipExpiryDate,
                CreditLimit = customer.CreditLimit,
                OutstandingBalance = customer.OutstandingBalance,
                LoyaltyPoints = customer.LoyaltyPoints,
                AllowCredit = customer.AllowCredit,
                ReceiveMarketingMessages = customer.ReceiveMarketingMessages,
                ReceiveBookingReminders = customer.ReceiveBookingReminders,
                IsBlacklisted = customer.IsBlacklisted,
                BlacklistReason = customer.BlacklistReason,
                InternalNotes = customer.InternalNotes,
                CustomerTags = customer.CustomerTags,
                IsActive = customer.IsActive,
                CreatedAt = customer.CreatedAt,
                UpdatedAt = customer.UpdatedAt,
                LastVisitAt = customer.LastVisitAt,
                CanManage = CanManage()
            };
        }

        private async Task<CustomerFormViewModel> AddSportsAsync(CustomerFormViewModel model)
        {
            model.AvailableSports = await _dbContext.Sports.AsNoTracking()
                .Where(x => x.IsActive)
                .OrderBy(x => x.DisplayOrder)
                .ThenBy(x => x.Name)
                .Select(x => new SelectListItem { Value = x.Code, Text = x.Name, Selected = model.SelectedSportCodes.Contains(x.Code) })
                .ToListAsync();
            return model;
        }

        private IQueryable<Customer> ApplySorting(IQueryable<Customer> query, string sortBy, string direction)
        {
            var asc = direction == "asc";
            return sortBy switch
            {
                "name" => asc ? query.OrderBy(x => x.FirstName).ThenBy(x => x.LastName) : query.OrderByDescending(x => x.FirstName).ThenByDescending(x => x.LastName),
                "oldest" => query.OrderBy(x => x.CreatedAt),
                "code" => asc ? query.OrderBy(x => x.CustomerCode) : query.OrderByDescending(x => x.CustomerCode),
                "loyalty" => query.OrderByDescending(x => x.LoyaltyPoints),
                "balance" => query.OrderByDescending(x => x.OutstandingBalance),
                "last-visit" => query.OrderByDescending(x => x.LastVisitAt),
                "active" => query.OrderByDescending(x => x.IsActive).ThenBy(x => x.FirstName),
                "blacklisted" => query.OrderByDescending(x => x.IsBlacklisted).ThenBy(x => x.FirstName),
                _ => query.OrderByDescending(x => x.CreatedAt)
            };
        }

        private async Task<(bool Success, string? Url, string Message)> SaveProfileImageAsync(CustomerFormViewModel model)
        {
            if (model.ProfileImage == null || model.ProfileImage.Length == 0) return (true, null, string.Empty);
            var extension = Path.GetExtension(model.ProfileImage.FileName).ToLowerInvariant();
            if (!AllowedImageExtensions.Contains(extension) || !AllowedImageContentTypes.Contains(model.ProfileImage.ContentType))
            {
                return (false, null, "Only JPG, PNG, and WEBP images are allowed.");
            }
            if (model.ProfileImage.Length > MaxImageSize)
            {
                return (false, null, "Profile image cannot exceed 3 MB.");
            }

            var folder = Path.Combine(_environment.WebRootPath, "uploads", "customers");
            Directory.CreateDirectory(folder);
            var fileName = $"{Guid.NewGuid():N}{extension}";
            var fullPath = Path.Combine(folder, fileName);
            await using var stream = System.IO.File.Create(fullPath);
            await model.ProfileImage.CopyToAsync(stream);
            return (true, $"/uploads/customers/{fileName}", string.Empty);
        }

        private void DeleteProfileImage(string? url)
        {
            if (string.IsNullOrWhiteSpace(url) || !url.StartsWith("/uploads/customers/", StringComparison.OrdinalIgnoreCase)) return;
            var fileName = Path.GetFileName(url);
            var fullPath = Path.Combine(_environment.WebRootPath, "uploads", "customers", fileName);
            if (System.IO.File.Exists(fullPath)) System.IO.File.Delete(fullPath);
        }

        private Task<bool> PhoneExistsAsync(string? phone, int? id = null)
        {
            if (string.IsNullOrWhiteSpace(phone)) return Task.FromResult(false);
            return _dbContext.Customers.AnyAsync(x =>
                (x.PrimaryPhone == phone || x.SecondaryPhone == phone || x.WhatsAppNumber == phone) &&
                (!id.HasValue || x.Id != id.Value));
        }

        private IActionResult? EnsureCanManage() => CanManage() ? null : RedirectToAction("NotFound", "Error");

        private bool CanManage()
        {
            return RolePermissions.TryGetCurrentRole(User, out var role)
                && role is UserRole.SuperAdmin or UserRole.Admin or UserRole.BookingManager or UserRole.Receptionist;
        }

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

        private static string? NormalizeCsv(string? value)
        {
            if (string.IsNullOrWhiteSpace(value)) return null;
            return string.Join(", ", value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Distinct(StringComparer.OrdinalIgnoreCase));
        }

        private static List<string> SplitCsv(string? value) => string.IsNullOrWhiteSpace(value)
            ? new List<string>()
            : value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Select(x => x.ToUpperInvariant()).ToList();

        private static string NormalizeOption(string? value) => value?.Trim().ToLowerInvariant() ?? string.Empty;
        private static string NormalizeSort(string? value)
        {
            var allowed = new[] { "name", "newest", "oldest", "code", "loyalty", "balance", "last-visit", "active", "blacklisted" };
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
