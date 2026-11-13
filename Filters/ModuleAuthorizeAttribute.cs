using GameHub.Helpers;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace GameHub.Filters
{
    /// <summary>
    /// Guards a controller action with a module from <see cref="RolePermissions"/>.
    /// Use this for screens shared by several roles (for example Bookings vs Approvals)
    /// so the permission matrix stays in one place.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
    public class ModuleAuthorizeAttribute : Attribute, IAuthorizationFilter
    {
        private readonly string _module;

        public ModuleAuthorizeAttribute(string module)
        {
            _module = module;
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

            if (!RolePermissions.CanAccessModule(user, _module))
            {
                context.Result = new ForbidResult(AuthConstants.Scheme);
            }
        }
    }
}
