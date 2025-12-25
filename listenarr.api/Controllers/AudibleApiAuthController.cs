using Microsoft.AspNetCore.Mvc;

namespace Listenarr.Api.Controllers;

// Audible auth endpoints removed — placeholder controller that returns 404
[ApiController]
[Route("api/audible-auth")]
public class AudibleApiAuthController : ControllerBase
{
    [HttpPost("external-login-start")]
    public IActionResult StartExternalLogin() => NotFound(new { message = "Audible integration removed" });

    [HttpPost("external-login-complete")]
    public IActionResult CompleteExternalLogin() => NotFound(new { message = "Audible integration removed" });

    [HttpGet("status")]
    public IActionResult Status() => NotFound(new { message = "Audible integration removed" });

    [HttpPost("logout")]
    public IActionResult Logout() => NotFound(new { message = "Audible integration removed" });
}
