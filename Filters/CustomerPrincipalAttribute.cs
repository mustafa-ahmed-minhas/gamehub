using GameHub.Helpers;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace GameHub.Filters
{
    /// <summary>
    /// Adopts the customer cookie principal as <c>HttpContext.User</c> for the current request.
    /// <para>
    /// The default authentication scheme in this application is the admin scheme
    /// (<see cref="AuthConstants.Scheme"/>). On any request that is not protected by
    /// <see cref="CustomerAuthorizeAttribute"/> - including <c>AccountController</c>, which is
    /// <c>[AllowAnonymous]</c> - <c>HttpContext.User</c> is therefore the admin principal rather
    /// than the signed-in customer.
    /// <para>
    /// Antiforgery tokens are bound to the claim identity of <c>HttpContext.User</c>. A token
    /// minted on a customer page (where <c>CustomerAuthorize</c> already assigned the customer
    /// principal) consequently failed validation on sign out with
    /// <c>AntiforgeryValidationException: "The provided antiforgery token was meant for a
    /// different claims-based user than the current user."</c> This filter makes the validating
    /// request resolve the same principal that minted the token, so the standard
    /// <c>[ValidateAntiForgeryToken]</c> check compares like with like.
    /// <para>
    /// <c>Order = -1000</c> places this ahead of the antiforgery authorization filter, which runs
    /// at order 1000. The principal is only replaced when a customer is genuinely signed in, so
    /// the admin scheme keeps full control of admin requests and admin sign out.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
    public sealed class CustomerPrincipalAttribute : Attribute, IAsyncAuthorizationFilter, IOrderedFilter
    {
        /// <summary>
        /// Runs ahead of the antiforgery authorization filter, which sits at order 1000.
        /// </summary>
        public int Order => -1000;

        public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
        {
            var result = await context.HttpContext.AuthenticateAsync(AuthConstants.CustomerScheme);
            if (result.Succeeded && result.Principal?.Identity?.IsAuthenticated == true)
            {
                context.HttpContext.User = result.Principal;
            }
        }
    }
}
