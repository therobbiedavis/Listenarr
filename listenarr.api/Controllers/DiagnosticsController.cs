using Listenarr.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace Listenarr.Api.Controllers;

[ApiController]
[Route("api/diagnostics")]
public class DiagnosticsController : ControllerBase
{

    [HttpGet("session")]
    public IActionResult GetSessionStatus()
    {
        var isAuthenticated = User?.Identity?.IsAuthenticated ?? false;
        var username = User?.Identity?.Name ?? "Anonymous";
        var authType = User?.Identity?.AuthenticationType ?? "None";
        
        return Ok(new 
        { 
            authenticated = isAuthenticated, 
            username, 
            authType,
            hasApiKey = Request.Headers.ContainsKey("X-Api-Key"),
            hasSessionToken = Request.Headers.ContainsKey("Authorization") || Request.Headers.ContainsKey("X-Session-Token")
        });
    }
}
