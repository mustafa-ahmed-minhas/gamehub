using System.Security.Claims;
using GameHub.Data;
using GameHub.Helpers;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;

namespace GameHub.Filters
{
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
    public class CustomerAuthorizeAttribute : Attribute, IAsyncAuthorizationFilter
    {
        public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
        {
            context.HttpContext.Response.Headers.CacheControl = "no-store, no-cache, must-revalidate, max-age=0";
            context.HttpContext.Response.Headers.Pragma = "no-cache";
            context.HttpContext.Response.Headers.Expires = "0";

            var result = await context.HttpContext.AuthenticateAsync(AuthConstants.CustomerScheme);
            var returnUrl = context.HttpContext.Request.Path + context.HttpContext.Request.QueryString;

            if (!result.Succeeded || result.Principal?.Identity?.IsAuthenticated != true)
            {
                context.Result = new RedirectToActionResult("Login", "Account", new { returnUrl });
                return;
            }

            context.HttpContext.User = result.Principal;

            var idValue = result.Principal.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!int.TryParse(idValue, out var customerId))
            {
                await context.HttpContext.SignOutAsync(AuthConstants.CustomerScheme);
                context.Result = new RedirectToActionResult("Login", "Account", new { returnUrl });
                return;
            }

            var dbContext = context.HttpContext.RequestServices.GetRequiredService<ApplicationDbContext>();
            var customer = await dbContext.Customers
                .AsNoTracking()
                .Where(item => item.Id == customerId)
                .Select(item => new { item.IsActive, item.HasOnlineAccount, item.IsBlacklisted })
                .FirstOrDefaultAsync();

            if (customer == null || !customer.IsActive || !customer.HasOnlineAccount)
            {
                await context.HttpContext.SignOutAsync(AuthConstants.CustomerScheme);
                context.Result = new RedirectToActionResult("Login", "Account", new { returnUrl });
                return;
            }

            if (customer.IsBlacklisted)
            {
                await context.HttpContext.SignOutAsync(AuthConstants.CustomerScheme);
                context.HttpContext.Items["CustomerAccessMessage"] = "Your account requires support assistance.";
                context.Result = new RedirectToActionResult("Login", "Account", null);
            }
        }
    }
}
