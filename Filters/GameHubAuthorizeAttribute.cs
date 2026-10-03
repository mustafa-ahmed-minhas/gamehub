using GameHub.Helpers;
using GameHub.Models.Enums;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace GameHub.Filters
{
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
    public class GameHubAuthorizeAttribute : Attribute, IAuthorizationFilter
    {
        private readonly UserRole[] _allowedRoles;

        public GameHubAuthorizeAttribute(params UserRole[] allowedRoles)
        {
            _allowedRoles = allowedRoles;
        }

        public void OnAuthorization(AuthorizationFilterContext context)
        {
            var user = context.HttpContext.User;
            if (user.Identity?.IsAuthenticated != true)
            {
                var returnUrl = context.HttpContext.Request.Path + context.HttpContext.Request.QueryString;
                context.Result = new RedirectToActionResult("Login", "Auth", new { returnUrl = returnUrl.ToString() });
                return;
            }

            if (!RolePermissions.TryGetCurrentRole(user, out var currentRole) ||
                (_allowedRoles.Length > 0 && !_allowedRoles.Contains(currentRole)))
            {
                context.Result = new RedirectToActionResult("NotFound", "Error", null);
            }
        }
    }
}
