using System.Security.Claims;
using GameHub.Data;
using GameHub.Helpers;
using GameHub.Models.ViewModels;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GameHub.Controllers
{
    [AllowAnonymous]
    public class AuthController : Controller
    {
        private readonly ApplicationDbContext _dbContext;

        public AuthController(ApplicationDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        [HttpGet]
        [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
        public IActionResult Login(string? returnUrl = null)
        {
            if (User.Identity?.IsAuthenticated == true)
            {
                return RedirectToAction("Index", "Dashboard");
            }

            return View(new LoginViewModel
            {
                ReturnUrl = Url.IsLocalUrl(returnUrl) ? returnUrl : null
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
        public async Task<IActionResult> Login(LoginViewModel model)
        {
            model.Email = model.Email?.Trim().ToLowerInvariant() ?? string.Empty;
            model.ReturnUrl = Url.IsLocalUrl(model.ReturnUrl) ? model.ReturnUrl : null;

            if (!ModelState.IsValid)
            {
                model.Password = string.Empty;
                TempData.SetToast("error", "Something Went Wrong", "Invalid email or password.");
                return View(model);
            }

            var user = await _dbContext.Users.FirstOrDefaultAsync(item => item.Email.ToLower() == model.Email);
            if (user == null || !PasswordHelper.VerifyPassword(user, user.PasswordHash, model.Password))
            {
                ModelState.AddModelError(string.Empty, "Invalid email or password.");
                model.Password = string.Empty;
                TempData.SetToast("error", "Something Went Wrong", "Invalid email or password.");
                return View(model);
            }

            if (!user.IsActive)
            {
                ModelState.AddModelError(string.Empty, "Your account is inactive. Please contact the administrator.");
                model.Password = string.Empty;
                TempData.SetToast("warning", "Attention Required", "Your account is inactive. Please contact the administrator.");
                return View(model);
            }

            var claims = new List<Claim>
            {
                new(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new(ClaimTypes.Name, user.FullName),
                new(ClaimTypes.Email, user.Email),
                new(ClaimTypes.Role, user.Role.ToString()),
                new(ClaimTypes.GivenName, user.FirstName),
                new(ClaimTypes.Surname, user.LastName)
            };

            var identity = new ClaimsIdentity(claims, AuthConstants.Scheme);
            var principal = new ClaimsPrincipal(identity);
            var properties = new AuthenticationProperties
            {
                IsPersistent = model.RememberMe,
                ExpiresUtc = DateTimeOffset.UtcNow.AddHours(8),
                AllowRefresh = true
            };

            user.LastLoginAt = DateTime.UtcNow;
            await _dbContext.SaveChangesAsync();
            await HttpContext.SignInAsync(AuthConstants.Scheme, principal, properties);

            TempData.SetToast("success", "Success", $"Welcome back, {user.FirstName}.");

            return Redirect(Url.IsLocalUrl(model.ReturnUrl) ? model.ReturnUrl! : Url.Action("Index", "Dashboard")!);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync(AuthConstants.Scheme);
            TempData.SetToast("success", "Success", "You have been signed out successfully.");
            return RedirectToAction(nameof(Login));
        }
    }
}
