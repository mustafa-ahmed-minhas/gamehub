using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GameHub.Controllers
{
    [AllowAnonymous]
    public class ErrorController : Controller
    {
        // Deliberately no [HttpGet]. app.UseStatusCodePagesWithReExecute("/Error/NotFound") in
        // Program.cs re-enters this path while PRESERVING the original request method, so a POST
        // that fails (for example a rejected antiforgery token, or a 404/400 on a form post) is
        // re-executed as POST /Error/NotFound. When this action was GET-only, the router rejected
        // that verb and the re-executed response reported 405 Method Not Allowed instead of the
        // real failure, which is why customer sign-out surfaced as a 405. Accepting every method
        // lets the genuine status code through.
        [Route("Error/NotFound")]
        public new IActionResult NotFound()
        {
            Response.StatusCode = StatusCodes.Status404NotFound;
            return View();
        }
    }
}
