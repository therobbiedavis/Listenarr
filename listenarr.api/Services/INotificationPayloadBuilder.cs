using System;
using System.Net.Http;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;

namespace Listenarr.Api.Services
{
    /// <summary>
    /// Abstraction for building notification payloads and preparing attachments.
    /// Implementations should contain only logic for payload construction and attachment retrieval.
    /// </summary>
    public interface INotificationPayloadBuilder
    {
        JsonNode CreateDiscordPayload(string trigger, object data, string? startupBaseUrl);

        Task<(JsonObject payload, NotificationAttachmentInfo? attachment)> CreateDiscordPayloadWithAttachmentAsync(
            string trigger,
            object data,
            string? startupBaseUrl,
            HttpClient httpClient,
            IHttpContextAccessor? httpContextAccessor = null,
            Action<string>? logInfo = null,
            Action<Exception, string>? logDebug = null);
    }
}
