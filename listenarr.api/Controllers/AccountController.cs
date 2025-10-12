using Listenarr.Api.Models;
using Listenarr.Api.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace Listenarr.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AccountController : ControllerBase
    {
        private readonly IStartupConfigService _startupConfigService;
        private readonly ILogger<AccountController> _logger;
        private readonly IUserService _userService;
    private readonly ILoginRateLimiter _rateLimiter;

        public AccountController(IStartupConfigService startupConfigService, ILogger<AccountController> logger, IUserService userService, ILoginRateLimiter rateLimiter)
        {
            _startupConfigService = startupConfigService;
            _logger = logger;
            _userService = userService;
            _rateLimiter = rateLimiter;
        }

        [HttpPost("login")]
        [AllowAnonymous]
        public async Task<IActionResult> Login([FromBody] LoginRequest req)
        {
            // NOTE: This is a minimal demo implementation. Replace with a proper user store.
            if (string.IsNullOrWhiteSpace(req.Username) || string.IsNullOrWhiteSpace(req.Password))
            {
                return BadRequest(new { message = "Username and password required" });
            }

            // Rate limiter key: IP + username
            var ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
            var key = $"{ip}:{req.Username}";
            if (_rateLimiter.IsBlocked(key))
            {
                var seconds = _rateLimiter.GetSecondsUntilUnblock(key);
                // Add Retry-After header (in seconds) and return 429 with remaining seconds
                Response.Headers["Retry-After"] = seconds.ToString();
                return StatusCode(429, new { message = "Too many failed login attempts, try again later", retryAfterSeconds = seconds });
            }

            // Validate against user store
            var valid = await _userService.ValidateCredentialsAsync(req.Username, req.Password);
            if (!valid)
            {
                _rateLimiter.RecordFailure(key);
                var seconds = _rateLimiter.GetSecondsUntilUnblock(key);
                if (seconds > 0)
                {
                    Response.Headers["Retry-After"] = seconds.ToString();
                    return StatusCode(429, new { message = "Too many failed login attempts, try again later", retryAfterSeconds = seconds });
                }

                return Unauthorized(new { message = "Invalid credentials" });
            }

            var user = await _userService.GetByUsernameAsync(req.Username);
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.Name, req.Username)
            };
            if (user?.IsAdmin == true) claims.Add(new Claim(ClaimTypes.Role, "Administrator"));

            var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            var authProperties = new AuthenticationProperties
            {
                IsPersistent = req.RememberMe
            };

            await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(claimsIdentity), authProperties);
            // Clear failures on successful login
            _rateLimiter.RecordSuccess(key);
            return Ok(new { message = "Logged in" });
        }

        [HttpPost("register")]
        [AllowAnonymous]
        public async Task<IActionResult> Register([FromBody] RegisterRequest req)
        {
            if (string.IsNullOrWhiteSpace(req.Username) || string.IsNullOrWhiteSpace(req.Password))
            {
                return BadRequest(new { message = "Username and password required" });
            }

            try
            {
                var user = await _userService.CreateUserAsync(req.Username, req.Password, req.Email, req.IsAdmin);
                return Ok(new { id = user.Id, username = user.Username });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to register user");
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpPost("logout")]
        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return Ok(new { message = "Logged out" });
        }

    [HttpGet("me")]
    [AllowAnonymous]
    public ActionResult<object> Me()
        {
            if (!User?.Identity?.IsAuthenticated ?? false)
                return Ok(new { authenticated = false });

            return Ok(new { authenticated = true, name = User?.Identity?.Name ?? string.Empty });
        }

        [HttpGet("admins")]
        public async Task<IActionResult> GetAdminUsers()
        {
            var admins = await _userService.GetAdminUsersAsync();
            var result = admins.Select(u => new
            {
                u.Id,
                u.Username,
                u.Email,
                u.IsAdmin,
                u.CreatedAt
            }).ToList();
            
            return Ok(result);
        }
    }

    public class LoginRequest
    {
        public string Username { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public bool RememberMe { get; set; }
    }

    public class RegisterRequest
    {
        public string Username { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public string? Email { get; set; }
        public bool IsAdmin { get; set; }
    }
}
