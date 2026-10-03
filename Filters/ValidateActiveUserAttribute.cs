using System.Security.Claims;
using GameHub.Data;
using GameHub.Helpers;
using GameHub.Models.Enums;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;

namespace GameHub.Filters
{
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
    public class ValidateActiveUserAttribute : Attribute, IAsyncActionFilter
    {
        public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
        {
            var principal = context.HttpContext.User;
            if (principal.Identity?.IsAuthenticated != true)
            {
                await next();
                return;
            }

            var idValue = principal.FindFirstValue(ClaimTypes.NameIdentifier);
            var roleValue = principal.FindFirstValue(ClaimTypes.Role);

            if (!int.TryParse(idValue, out var userId) || !Enum.TryParse<UserRole>(roleValue, out var cookieRole))
            {
                await InvalidateSessionAsync(context);
                return;
            }

            var dbContext = context.HttpContext.RequestServices.GetRequiredService<ApplicationDbContext>();
            var user = await dbContext.Users.AsNoTracking()
                .Where(item => item.Id == userId)
                .Select(item => new { item.IsActive, item.Role })
                .FirstOrDefaultAsync();

            if (user == null || !user.IsActive || user.Role != cookieRole)
            {
                await InvalidateSessionAsync(context);
                return;
            }

            await next();
        }

        private static async Task InvalidateSessionAsync(ActionExecutingContext context)
        {
            await context.HttpContext.SignOutAsync(AuthConstants.Scheme);
            if (context.Controller is Controller controller)
            {
                controller.TempData.SetToast("warning", "Attention Required", "Your session is no longer valid. Please sign in again.");
            }
            context.Result = new RedirectToActionResult("Login", "Auth", null);
        }
    }
}
