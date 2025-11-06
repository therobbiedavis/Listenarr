using Listenarr.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace Listenarr.Api.Controllers;

[ApiController]
[Route("api/diagnostics")]
public class DiagnosticsController : ControllerBase
{
    private readonly NotificationService _notificationService;
    private readonly IConfigurationService _configurationService;
    private readonly ILogger<DiagnosticsController> _logger;

    public DiagnosticsController(
        NotificationService notificationService,
        IConfigurationService configurationService,
        ILogger<DiagnosticsController> logger)
    {
        _notificationService = notificationService;
        _configurationService = configurationService;
        _logger = logger;
    }

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

    [HttpPost("test-notification")]
    public async Task<IActionResult> TestNotification([FromBody] TestNotificationRequest request)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(request.Trigger))
            {
                return BadRequest("Trigger type is required");
            }

            // Get settings to check webhook configuration
            var settings = await _configurationService.GetApplicationSettingsAsync();
            if (settings == null || string.IsNullOrWhiteSpace(settings.WebhookUrl))
            {
                return BadRequest("Webhook URL is not configured");
            }

            // For test notifications, allow testing any trigger even if not enabled
            // This lets users verify webhook configuration before enabling specific triggers
            var triggersToUse = settings.EnabledNotificationTriggers ?? new List<string>();
            if (!triggersToUse.Contains(request.Trigger))
            {
                _logger.LogInformation("Test notification for trigger '{Trigger}' - trigger not enabled but allowing for testing purposes", request.Trigger);
                // Temporarily add the trigger to the list for this test
                triggersToUse = new List<string>(triggersToUse) { request.Trigger };
            }

            // Send the notification
            await _notificationService.SendNotificationAsync(
                request.Trigger,
                request.Data ?? new object(),
                settings.WebhookUrl,
                triggersToUse
            );

            return Ok(new { message = "Test notification sent successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send test notification");
            return StatusCode(500, $"Failed to send notification: {ex.Message}");
        }
    }

    public class TestNotificationRequest
    {
        public required string Trigger { get; set; }
        public object? Data { get; set; }
    }
}
