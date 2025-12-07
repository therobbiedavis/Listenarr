using System.Net.Http;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Linq;
using Microsoft.AspNetCore.Http;

namespace Listenarr.Api.Services
{
    /// <summary>
    /// Service for sending webhook notifications.
    /// Refactored to delegate payload construction and attachment handling to NotificationPayloadBuilder.
    /// Provides static compatibility shims used by tests.
    /// </summary>
    public class NotificationService
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<NotificationService> _logger;
        private readonly IConfigurationService _configurationService;
        private readonly IHttpContextAccessor? _httpContextAccessor;
        private readonly INotificationPayloadBuilder _payloadBuilder;

        public NotificationService(HttpClient httpClient, ILogger<NotificationService> logger, IConfigurationService configurationService, INotificationPayloadBuilder payloadBuilder, IHttpContextAccessor? httpContextAccessor = null)
        {
            _httpClient = httpClient;
            _logger = logger;
            _configurationService = configurationService;
            _payloadBuilder = payloadBuilder ?? throw new ArgumentNullException(nameof(payloadBuilder));
            _httpContextAccessor = httpContextAccessor;
        }

        // Compatibility shims removed — callers/tests should use NotificationPayloadBuilder directly.

        /// <summary>
        /// Sends a notification to the webhook URL if configured.
        /// </summary>
        public async Task SendNotificationAsync(string trigger, object data, string webhookUrl, List<string> enabledTriggers)
        {
            if (string.IsNullOrWhiteSpace(webhookUrl) || enabledTriggers == null || !enabledTriggers.Contains(trigger))
            {
                return;
            }

            // Discord-specific handling
            if (webhookUrl.Contains("discord.com/api/webhooks", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    var startup = await _configurationService.GetStartupConfigAsync();
                    var baseUrl = startup?.UrlBase;

                    if (string.IsNullOrWhiteSpace(baseUrl) && _httpContextAccessor?.HttpContext != null)
                    {
                        var derived = NotificationPayloadBuilder.GetBaseUrlFromHttpContext(_httpContextAccessor.HttpContext);
                        if (!string.IsNullOrWhiteSpace(derived)) baseUrl = derived;
                    }

                    if (!string.IsNullOrWhiteSpace(baseUrl) &&
                        !(baseUrl.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                          baseUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase)))
                    {
                        _logger.LogWarning("Invalid base URL configured: {BaseUrl} - notifications will not include images", baseUrl);
                        baseUrl = null;
                    }

                    // Build payload (with optional attachment) using the helper
                    var (payloadObj, attachment) = await _payloadBuilder.CreateDiscordPayloadWithAttachmentAsync(
                        trigger,
                        data,
                        baseUrl,
                        _httpClient,
                        _httpContextAccessor,
                        logInfo: msg => _logger.LogInformation(msg),
                        logDebug: (ex, msg) => _logger.LogDebug(ex, msg)
                    );

                    // Send multipart if we have an attachment
                    if (attachment != null)
                    {
                        using var multipartContent = new MultipartFormDataContent();

                        var jsonContent = new System.Net.Http.StringContent(payloadObj.ToJsonString(), Encoding.UTF8, "application/json");
                        jsonContent.Headers.ContentDisposition = new System.Net.Http.Headers.ContentDispositionHeaderValue("form-data") { Name = "payload_json" };
                        jsonContent.Headers.TryAddWithoutValidation("Content-Disposition", "form-data; name=\"payload_json\"");
                        jsonContent.Headers.TryAddWithoutValidation("X-Debug-Payload-Name", "name=\"payload_json\"");
                        multipartContent.Add(jsonContent, "payload_json");

                        var imageContent = new ByteArrayContent(attachment.ImageData);
                        imageContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(attachment.ContentType);
                        imageContent.Headers.TryAddWithoutValidation("X-Debug-Files", $"name=\"files[0]\"; filename=\"{attachment.Filename}\"");
                        multipartContent.Add(imageContent, "files[0]", attachment.Filename);

                        var response = await _httpClient.PostAsync(webhookUrl, multipartContent);
                        if (!response.IsSuccessStatusCode)
                        {
                            string body = string.Empty;
                            try { body = await response.Content.ReadAsStringAsync(); } catch { }

                            var redactedUrl = LogRedaction.RedactText(webhookUrl, LogRedaction.GetSensitiveValuesFromEnvironment());
                            var redactedBody = LogRedaction.RedactText(body, LogRedaction.GetSensitiveValuesFromEnvironment());
                            _logger.LogWarning("Failed to send notification to {WebhookUrl}: {StatusCode} - {Body}", redactedUrl, response.StatusCode, redactedBody);
                        }
                    }
                    else
                    {
                        var discordJson = payloadObj.ToJsonString();
                        var discordContent = new System.Net.Http.StringContent(discordJson, Encoding.UTF8, "application/json");
                        var response = await _httpClient.PostAsync(webhookUrl, discordContent);
                        if (!response.IsSuccessStatusCode)
                        {
                            string body = string.Empty;
                            try { body = await response.Content.ReadAsStringAsync(); } catch { }

                            var redactedUrl = LogRedaction.RedactText(webhookUrl, LogRedaction.GetSensitiveValuesFromEnvironment());
                            var redactedBody = LogRedaction.RedactText(body, LogRedaction.GetSensitiveValuesFromEnvironment());
                            _logger.LogWarning("Failed to send notification to {WebhookUrl}: {StatusCode} - {Body}", redactedUrl, response.StatusCode, redactedBody);
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error sending notification to {WebhookUrl}", LogRedaction.RedactText(webhookUrl, LogRedaction.GetSensitiveValuesFromEnvironment()));
                }

                return;
            }

            // Generic webhook fallback
            var defaultPayload = new
            {
                @event = trigger,
                data = data,
                timestamp = DateTime.UtcNow
            };

            var defaultJson = JsonSerializer.Serialize(defaultPayload, new JsonSerializerOptions { DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull });
            using var defaultContent = new StringContent(defaultJson, Encoding.UTF8, "application/json");

            try
            {
                var response = await _httpClient.PostAsync(webhookUrl, defaultContent);
                if (!response.IsSuccessStatusCode)
                {
                    string body = string.Empty;
                    try { body = await response.Content.ReadAsStringAsync(); } catch { }

                    var redactedUrl = LogRedaction.RedactText(webhookUrl, LogRedaction.GetSensitiveValuesFromEnvironment());
                    var redactedBody = LogRedaction.RedactText(body, LogRedaction.GetSensitiveValuesFromEnvironment());
                    _logger.LogWarning("Failed to send notification to {WebhookUrl}: {StatusCode} - {Body}", redactedUrl, response.StatusCode, redactedBody);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending notification to {WebhookUrl}", LogRedaction.RedactText(webhookUrl, LogRedaction.GetSensitiveValuesFromEnvironment()));
            }
        }
    }
}
