using System.Security.Claims;
using GameHub.Data;
using GameHub.Filters;
using GameHub.Helpers;
using GameHub.Models.Entities;
using GameHub.Models.Enums;
using GameHub.Models.ViewModels;
using GameHub.Services.Interfaces;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GameHub.Controllers
{
    [AllowAnonymous]
    public class AccountController : Controller
    {
        private const int LockoutMinutes = 15;
        private readonly ApplicationDbContext _dbContext;
        private readonly ICustomerCodeService _customerCodeService;
        private readonly IWebHostEnvironment _environment;
        private readonly ILogger<AccountController> _logger;

        public AccountController(ApplicationDbContext dbContext, ICustomerCodeService customerCodeService, IWebHostEnvironment environment, ILogger<AccountController> logger)
        {
            _dbContext = dbContext;
            _customerCodeService = customerCodeService;
            _environment = environment;
            _logger = logger;
        }

        [HttpGet]
        [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
        public async Task<IActionResult> Signup()
        {
            if (await IsCustomerAuthenticatedAsync())
            {
                return RedirectToAction("Index", "MyAccount");
            }

            var settings = await _dbContext.SystemSettings.AsNoTracking().FirstOrDefaultAsync();
            ApplySignupPasswordPolicy(settings);
            return View(new CustomerSignupViewModel());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
        public async Task<IActionResult> Signup(CustomerSignupViewModel model)
        {
            NormalizeSignup(model);
            await ValidateSignupAsync(model);

            if (!ModelState.IsValid)
            {
                return SignupFailed(model);
            }

            var normalizedPhone = NormalizePhone(model.PrimaryPhone);
            var customer = await _dbContext.Customers
                .FirstOrDefaultAsync(item => (item.Email != null && item.Email.ToLower() == model.Email) || item.PrimaryPhone == normalizedPhone);

            if (customer != null)
            {
                if (customer.HasOnlineAccount)
                {
                    ModelState.AddModelError(string.Empty, "An online account already exists for these details.");
                    return SignupFailed(model);
                }

                if (!customer.IsActive || customer.IsBlacklisted)
                {
                    ModelState.AddModelError(string.Empty, "Your account cannot be created online. Please contact GameHub support.");
                    return SignupFailed(model);
                }

                if (!CanSafelyLinkExistingCustomer(customer, model.Email, normalizedPhone))
                {
                    // The signup matched an existing profile on only one identifier. Linking
                    // would overwrite that profile's other identifier, so report the field
                    // that does not match instead of failing later inside the database.
                    if (!string.IsNullOrWhiteSpace(customer.Email) &&
                        !customer.Email.Equals(model.Email, StringComparison.OrdinalIgnoreCase))
                    {
                        ModelState.AddModelError(nameof(model.Email), "This email address belongs to a different GameHub profile. Please contact GameHub support.");
                    }

                    if (customer.PrimaryPhone != normalizedPhone)
                    {
                        ModelState.AddModelError(nameof(model.PrimaryPhone), "This phone number belongs to a different GameHub profile. Please contact GameHub support.");
                    }

                    return SignupFailed(model);
                }

                if (!string.IsNullOrWhiteSpace(customer.NationalIdNumber)
                    && !customer.NationalIdNumber.Equals(model.NationalIdNumber, StringComparison.Ordinal))
                {
                    ModelState.AddModelError(nameof(model.NationalIdNumber), "This CNIC / National ID does not match the existing GameHub profile. Please contact GameHub support.");
                    return SignupFailed(model);
                }

                customer.FirstName = model.FirstName;
                customer.LastName = model.LastName;
                customer.Email ??= model.Email;
                customer.PrimaryPhone = customer.PrimaryPhone == normalizedPhone ? customer.PrimaryPhone : normalizedPhone;
                customer.WhatsAppNumber = NormalizePhone(model.WhatsAppNumber);
                customer.NationalIdNumber ??= model.NationalIdNumber;
                customer.DateOfBirth ??= model.DateOfBirth;
                customer.Gender = model.Gender!.Value;
            }
            else
            {
                customer = new Customer
                {
                    CustomerCode = await _customerCodeService.GenerateNextCodeAsync(),
                    CustomerSource = CustomerSource.Website,
                    CustomerType = CustomerType.Individual,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow,
                    Country = "Pakistan",
                    PreferredContactMethod = PreferredContactMethod.WhatsApp
                };

                customer.FirstName = model.FirstName;
                customer.LastName = model.LastName;
                customer.Email = model.Email;
                customer.PrimaryPhone = normalizedPhone;
                customer.WhatsAppNumber = NormalizePhone(model.WhatsAppNumber);
                customer.NationalIdNumber = model.NationalIdNumber;
                customer.DateOfBirth = model.DateOfBirth;
                customer.Gender = model.Gender!.Value;
                _dbContext.Customers.Add(customer);
            }

            var token = TokenHelper.CreateSecureToken();
            customer.PasswordHash = CustomerPasswordHelper.HashPassword(customer, model.Password);
            customer.HasOnlineAccount = true;
            customer.EmailVerified = false;
            customer.EmailVerificationToken = TokenHelper.HashToken(token);
            customer.EmailVerificationTokenExpiresAt = DateTime.UtcNow.AddHours(24);
            customer.OnlineAccountCreatedAt = DateTime.UtcNow;
            customer.PasswordChangedAt = DateTime.UtcNow;
            customer.ReceiveBookingReminders = model.ReceiveBookingReminders;
            customer.ReceiveMarketingMessages = model.ReceiveMarketingMessages;
            customer.FailedLoginAttempts = 0;
            customer.LockoutEndAt = null;
            customer.UpdatedAt = DateTime.UtcNow;

            try
            {
                await _dbContext.SaveChangesAsync();
            }
            catch (DbUpdateException ex) when (IsDuplicateKey(ex))
            {
                // A concurrent signup (or a profile that changed after validation) can
                // still trip a unique index. Translate it into a field-level message
                // instead of surfacing a 500 to the visitor.
                _logger.LogWarning(ex, "Customer signup was rejected by a unique constraint.");

                if (HasUniqueIndex(ex, "IX_Customers_Email"))
                {
                    ModelState.AddModelError(nameof(model.Email), "An account already exists with this email address.");
                }
                else if (HasUniqueIndex(ex, "IX_Customers_NationalIdNumber"))
                {
                    ModelState.AddModelError(nameof(model.NationalIdNumber), "This CNIC / National ID is already registered to another GameHub account.");
                }
                else
                {
                    ModelState.AddModelError(string.Empty, "We could not create your account because some of these details are already registered. Please try again in a moment.");
                }

                return SignupFailed(model);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Customer signup failed while saving the account.");
                ModelState.AddModelError(string.Empty, "We could not create your account right now. Please try again in a moment.");
                return SignupFailed(model);
            }

            var verificationLink = Url.Action(nameof(VerifyEmail), "Account", new { token }, Request.Scheme);
            if (_environment.IsDevelopment())
            {
                TempData["DevelopmentVerificationLink"] = verificationLink;
            }

            TempData.SetToast("success", "Account Created", "Please verify your email to unlock the full booking experience.");
            return RedirectToAction(nameof(VerificationSent), new { email = customer.Email });
        }

        [HttpGet]
        [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
        public async Task<IActionResult> Login(string? returnUrl = null)
        {
            if (await IsCustomerAuthenticatedAsync())
            {
                return RedirectToAction("Index", "MyAccount");
            }

            return View(new CustomerLoginViewModel { ReturnUrl = SafeReturnUrl(returnUrl) });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
        public async Task<IActionResult> Login(CustomerLoginViewModel model)
        {
            model.EmailOrPhone = model.EmailOrPhone?.Trim() ?? string.Empty;
            model.ReturnUrl = SafeReturnUrl(model.ReturnUrl);

            if (!ModelState.IsValid)
            {
                TempData.SetToast("error", "Login Failed", "Invalid login details.");
                return View(model);
            }

            var login = model.EmailOrPhone.ToLowerInvariant();
            var phone = NormalizePhone(model.EmailOrPhone);
            var customer = await _dbContext.Customers.FirstOrDefaultAsync(item =>
                item.HasOnlineAccount &&
                ((item.Email != null && item.Email.ToLower() == login) || item.PrimaryPhone == phone));

            if (customer == null || string.IsNullOrWhiteSpace(customer.PasswordHash))
            {
                return InvalidLogin(model);
            }

            if (!customer.IsActive || customer.IsBlacklisted)
            {
                return InvalidLogin(model);
            }

            if (customer.LockoutEndAt.HasValue && customer.LockoutEndAt > DateTime.UtcNow)
            {
                ModelState.AddModelError(string.Empty, "Invalid login details.");
                TempData.SetToast("warning", "Account Locked", "Please try again after a short while.");
                return View(model);
            }

            if (!CustomerPasswordHelper.VerifyPassword(customer, customer.PasswordHash, model.Password))
            {
                customer.FailedLoginAttempts += 1;
                if (customer.FailedLoginAttempts >= 5)
                {
                    customer.LockoutEndAt = DateTime.UtcNow.AddMinutes(LockoutMinutes);
                }
                await _dbContext.SaveChangesAsync();
                return InvalidLogin(model);
            }

            customer.FailedLoginAttempts = 0;
            customer.LockoutEndAt = null;
            customer.LastOnlineLoginAt = DateTime.UtcNow;
            await _dbContext.SaveChangesAsync();

            await SignInCustomerAsync(customer, model.RememberMe);
            TempData.SetToast("success", "Welcome Back", $"Good to see you, {customer.FirstName}.");

            return Redirect(model.ReturnUrl ?? Url.Action("Index", "MyAccount")!);
        }

        [HttpPost]
        [CustomerPrincipal]
        [ValidateAntiForgeryToken]
        [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
        public async Task<IActionResult> Logout()
        {
            // CustomerPrincipal resolves HttpContext.User to the signed-in customer before the
            // standard antiforgery filter validates the token. Sign out is reached through
            // AccountController, which is [AllowAnonymous] and therefore not covered by
            // CustomerAuthorize, while the default authentication scheme is the admin one. Without
            // that alignment the customer-bound token in the layout form was compared against the
            // admin/anonymous principal and threw AntiforgeryValidationException.
            var publicLanding = Url.Action("Index", "Home") ?? "/";

            // Pass the destination explicitly. The cookie handler compares the request path with
            // options.LogoutPath ("/Account/Logout"), which is this action's own path, so an
            // un-redirected sign-out queues a second redirect back to the same URL and the final
            // Location header depends on write order. Naming the public page removes the ambiguity.
            await HttpContext.SignOutAsync(AuthConstants.CustomerScheme, new AuthenticationProperties
            {
                RedirectUri = publicLanding
            });

            // Clear the whole customer session rather than a single key, so no booking draft,
            // checkout state or cached customer value can survive a sign out.
            HttpContext.Session.Clear();

            var customerCookieOptions = new CookieOptions
            {
                HttpOnly = true,
                Secure = Request.IsHttps,
                SameSite = SameSiteMode.Lax,
                Path = "/"
            };

            // SignOutAsync already expires the auth cookie; these explicit deletes also cover the
            // session cookie and any stale cookie left by a previous scheme build.
            Response.Cookies.Delete("GameHub.Customer.Auth", customerCookieOptions);
            Response.Cookies.Delete(".GameHub.Customer.Session", customerCookieOptions);
            Response.Headers.CacheControl = "no-store, no-cache, must-revalidate, max-age=0";
            Response.Headers.Pragma = "no-cache";
            Response.Headers.Expires = "0";

            // TempData rides on the session provider, so the toast has to be written after the
            // session is cleared for the confirmation to reach the landing page.
            TempData.SetToast("success", "Signed Out", "You have signed out successfully.");
            return RedirectToAction("Index", "Home");
        }

        [HttpGet]
        public async Task<IActionResult> VerificationSent(string? email)
        {
            ViewBag.Email = email;
            return View();
        }

        [HttpGet]
        public async Task<IActionResult> VerifyEmail(string? token)
        {
            if (string.IsNullOrWhiteSpace(token))
            {
                return View("VerifyEmailResult", false);
            }

            var tokenHash = TokenHelper.HashToken(token);
            var customer = await _dbContext.Customers.FirstOrDefaultAsync(item =>
                item.EmailVerificationToken == tokenHash &&
                item.EmailVerificationTokenExpiresAt != null);

            if (customer == null || customer.EmailVerificationTokenExpiresAt < DateTime.UtcNow)
            {
                TempData.SetToast("error", "Verification Failed", "The verification link is invalid or expired.");
                return View("VerifyEmailResult", false);
            }

            customer.EmailVerified = true;
            customer.EmailVerificationToken = null;
            customer.EmailVerificationTokenExpiresAt = null;
            customer.UpdatedAt = DateTime.UtcNow;
            await _dbContext.SaveChangesAsync();

            TempData.SetToast("success", "Email Verified", "Your email has been verified successfully.");
            return View("VerifyEmailResult", true);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ResendVerification(string email)
        {
            var normalizedEmail = email?.Trim().ToLowerInvariant();
            var customer = await _dbContext.Customers.FirstOrDefaultAsync(item =>
                item.HasOnlineAccount &&
                !item.EmailVerified &&
                item.Email != null &&
                item.Email.ToLower() == normalizedEmail);

            if (customer != null)
            {
                if (customer.EmailVerificationTokenExpiresAt.HasValue &&
                    customer.EmailVerificationTokenExpiresAt.Value > DateTime.UtcNow.AddHours(23))
                {
                    TempData.SetToast("info", "Verification Pending", "Please wait before requesting another verification link.");
                    return RedirectToAction(nameof(VerificationSent), new { email = customer.Email });
                }

                var token = TokenHelper.CreateSecureToken();
                customer.EmailVerificationToken = TokenHelper.HashToken(token);
                customer.EmailVerificationTokenExpiresAt = DateTime.UtcNow.AddHours(24);
                await _dbContext.SaveChangesAsync();

                if (_environment.IsDevelopment())
                {
                    TempData["DevelopmentVerificationLink"] = Url.Action(nameof(VerifyEmail), "Account", new { token }, Request.Scheme);
                }
            }

            TempData.SetToast("success", "Verification Sent", "If an account exists, a new verification link has been prepared.");
            return RedirectToAction(nameof(VerificationSent), new { email = normalizedEmail });
        }

        [HttpGet]
        public async Task<IActionResult> CheckEmail(string email)
        {
            var normalized = email?.Trim().ToLowerInvariant();
            if (string.IsNullOrWhiteSpace(normalized) || !normalized.Contains('@'))
            {
                return Json(new { available = false, accountExists = false, canLinkExistingCustomer = false, message = "Enter a valid email address." });
            }

            var customer = await _dbContext.Customers.AsNoTracking().FirstOrDefaultAsync(item => item.Email != null && item.Email.ToLower() == normalized);
            return Json(BuildAvailabilityResponse(customer));
        }

        [HttpGet]
        public async Task<IActionResult> CheckPhone(string phone)
        {
            var normalized = NormalizePhone(phone);
            if (!PhonePattern.IsMatch(normalized))
            {
                return Json(new { available = false, accountExists = false, canLinkExistingCustomer = false, message = "Phone number must start with 0 and contain exactly 11 digits (e.g., 03001234567)." });
            }

            var customer = await _dbContext.Customers.AsNoTracking().FirstOrDefaultAsync(item => item.PrimaryPhone == normalized);
            return Json(BuildAvailabilityResponse(customer));
        }

        private object BuildAvailabilityResponse(Customer? customer)
        {
            if (customer == null)
            {
                return new { available = true, accountExists = false, canLinkExistingCustomer = false, message = "Available for signup." };
            }

            if (customer.HasOnlineAccount)
            {
                return new { available = false, accountExists = true, canLinkExistingCustomer = false, message = "An online account already exists for these details." };
            }

            if (!customer.IsActive || customer.IsBlacklisted)
            {
                return new { available = false, accountExists = false, canLinkExistingCustomer = false, message = "Please contact GameHub support for account assistance." };
            }

            return new { available = true, accountExists = false, canLinkExistingCustomer = true, message = "We can link this signup to an existing GameHub profile after validation." };
        }

        private async Task SignInCustomerAsync(Customer customer, bool rememberMe)
        {
            var claims = new List<Claim>
            {
                new(ClaimTypes.NameIdentifier, customer.Id.ToString()),
                new(ClaimTypes.Name, customer.FullName),
                new(ClaimTypes.Email, customer.Email ?? string.Empty),
                new("CustomerCode", customer.CustomerCode),
                new("AccessType", "Customer")
            };

            var identity = new ClaimsIdentity(claims, AuthConstants.CustomerScheme);
            var principal = new ClaimsPrincipal(identity);
            var properties = new AuthenticationProperties
            {
                IsPersistent = rememberMe,
                AllowRefresh = true,
                ExpiresUtc = rememberMe ? DateTimeOffset.UtcNow.AddDays(30) : DateTimeOffset.UtcNow.AddHours(4)
            };

            await HttpContext.SignInAsync(AuthConstants.CustomerScheme, principal, properties);
        }

        private IActionResult InvalidLogin(CustomerLoginViewModel model)
        {
            ModelState.AddModelError(string.Empty, "Invalid login details.");
            model.Password = string.Empty;
            TempData.SetToast("error", "Login Failed", "Invalid login details.");
            return View(model);
        }

        private async Task<bool> IsCustomerAuthenticatedAsync()
        {
            var result = await HttpContext.AuthenticateAsync(AuthConstants.CustomerScheme);
            return result.Succeeded && result.Principal?.Identity?.IsAuthenticated == true;
        }

        private async Task ValidateSignupAsync(CustomerSignupViewModel model)
        {
            var settings = await _dbContext.SystemSettings.AsNoTracking().FirstOrDefaultAsync();
            ApplySignupPasswordPolicy(settings);

            // Model binding stores an empty form field as null. Required attributes
            // report that; the policy checks must not dereference the missing value.
            if (!string.IsNullOrEmpty(model.Password))
            {
                var minimumLength = Math.Max(settings?.PasswordMinLength ?? 8, 8);
                if (model.Password.Length < minimumLength)
                {
                    ModelState.AddModelError(nameof(model.Password), $"Password must be at least {minimumLength} characters.");
                }

                if (settings?.RequireStrongPassword != false &&
                    (!model.Password.Any(char.IsUpper) ||
                     !model.Password.Any(char.IsLower) ||
                     !model.Password.Any(char.IsDigit) ||
                     !model.Password.Any(ch => !char.IsLetterOrDigit(ch))))
                {
                    ModelState.AddModelError(nameof(model.Password), "Password must include uppercase, lowercase, number and special character.");
                }
            }

            var normalizedPhone = NormalizePhone(model.PrimaryPhone);

            int? emailOwnerId = null;
            if (!string.IsNullOrWhiteSpace(model.Email))
            {
                var emailOwners = await _dbContext.Customers.AsNoTracking()
                    .Where(item => item.Email != null && item.Email.ToLower() == model.Email)
                    .Select(item => new { item.Id, item.HasOnlineAccount })
                    .ToListAsync();

                if (emailOwners.Any(owner => owner.HasOnlineAccount))
                {
                    ModelState.AddModelError(nameof(model.Email), "An online account already exists for this email address.");
                }

                emailOwnerId = emailOwners.Select(owner => (int?)owner.Id).FirstOrDefault();
            }

            int? phoneOwnerId = null;
            if (PhonePattern.IsMatch(normalizedPhone))
            {
                var phoneOwners = await _dbContext.Customers.AsNoTracking()
                    .Where(item => item.PrimaryPhone == normalizedPhone)
                    .Select(item => new { item.Id, item.HasOnlineAccount })
                    .ToListAsync();

                if (phoneOwners.Any(owner => owner.HasOnlineAccount))
                {
                    ModelState.AddModelError(nameof(model.PrimaryPhone), "An online account already exists for this phone number.");
                }

                phoneOwnerId = phoneOwners.Select(owner => (int?)owner.Id).FirstOrDefault();
            }

            // The email and phone resolve to different existing profiles. Linking
            // either row would collide with the other row's unique email on save.
            if (emailOwnerId.HasValue && phoneOwnerId.HasValue && emailOwnerId.Value != phoneOwnerId.Value)
            {
                ModelState.AddModelError(nameof(model.Email), "This email address and phone number are linked to different GameHub profiles. Please contact GameHub support.");
            }

            if (!string.IsNullOrWhiteSpace(model.NationalIdNumber) && NationalIdPattern.IsMatch(model.NationalIdNumber))
            {
                // A different customer row holding the same CNIC would violate the
                // unique index on save; the row matched by email / phone is fine
                // because linking only fills the CNIC when it is still empty.
                var duplicateNationalId = await _dbContext.Customers.AnyAsync(item =>
                    item.NationalIdNumber == model.NationalIdNumber &&
                    !((item.Email != null && item.Email.ToLower() == model.Email) || item.PrimaryPhone == normalizedPhone));
                if (duplicateNationalId)
                {
                    ModelState.AddModelError(nameof(model.NationalIdNumber), "This CNIC / National ID is already registered to another GameHub account.");
                }
            }
        }

        private IActionResult SignupFailed(CustomerSignupViewModel model)
        {
            model.Password = string.Empty;
            model.ConfirmPassword = string.Empty;
            return View(model);
        }

        private void ApplySignupPasswordPolicy(SystemSettings? settings)
        {
            ViewData["PasswordMinLength"] = Math.Max(settings?.PasswordMinLength ?? 8, 8);
            ViewData["RequireStrongPassword"] = settings?.RequireStrongPassword != false;
        }

        private static bool IsDuplicateKey(DbUpdateException exception)
        {
            var message = exception.GetBaseException().Message;
            return message.Contains("UNIQUE", StringComparison.OrdinalIgnoreCase)
                || message.Contains("duplicate key", StringComparison.OrdinalIgnoreCase)
                || message.Contains("Cannot insert duplicate", StringComparison.OrdinalIgnoreCase);
        }

        private static bool HasUniqueIndex(DbUpdateException exception, string indexName)
            => exception.GetBaseException().Message.Contains(indexName, StringComparison.OrdinalIgnoreCase);

        private static bool CanSafelyLinkExistingCustomer(Customer customer, string email, string primaryPhone)
        {
            var emailMatches = string.IsNullOrWhiteSpace(customer.Email) || customer.Email.Equals(email, StringComparison.OrdinalIgnoreCase);
            var phoneMatches = customer.PrimaryPhone == primaryPhone;
            return emailMatches && phoneMatches;
        }

        private static void NormalizeSignup(CustomerSignupViewModel model)
        {
            model.FirstName = model.FirstName?.Trim() ?? string.Empty;
            model.LastName = model.LastName?.Trim() ?? string.Empty;
            model.Email = model.Email?.Trim().ToLowerInvariant() ?? string.Empty;
            model.PrimaryPhone = NormalizePhone(model.PrimaryPhone);
            model.WhatsAppNumber = string.IsNullOrWhiteSpace(model.WhatsAppNumber) ? string.Empty : NormalizePhone(model.WhatsAppNumber);
            model.NationalIdNumber = model.NationalIdNumber?.Trim() ?? string.Empty;
        }

        private static string NormalizePhone(string? value)
        {
            if (string.IsNullOrWhiteSpace(value)) return string.Empty;
            var trimmed = value.Trim();
            var prefix = trimmed.StartsWith("+") ? "+" : string.Empty;
            return prefix + new string(trimmed.Where(char.IsDigit).ToArray());
        }

        private static readonly System.Text.RegularExpressions.Regex PhonePattern = new(@"^0\d{10}$", System.Text.RegularExpressions.RegexOptions.Compiled);

        private static readonly System.Text.RegularExpressions.Regex NationalIdPattern = new(@"^\d{13}$", System.Text.RegularExpressions.RegexOptions.Compiled);

        private string? SafeReturnUrl(string? returnUrl)
        {
            return Url.IsLocalUrl(returnUrl) ? returnUrl : null;
        }
    }
}
