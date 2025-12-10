using System;
using System.Net.Http;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;

namespace Listenarr.Api.Services
{
    /// <summary>
    /// Lightweight adapter that exposes the existing static NotificationPayloadBuilder
    /// via the INotificationPayloadBuilder interface so it can be injected and mocked.
    /// </summary>
    internal class NotificationPayloadBuilderAdapter : INotificationPayloadBuilder
    {
        public JsonNode CreateDiscordPayload(string trigger, object data, string? startupBaseUrl)
        {
            return NotificationPayloadBuilder.CreateDiscordPayload(trigger, data, startupBaseUrl);
        }

        public async Task<(JsonObject payload, NotificationAttachmentInfo? attachment)> CreateDiscordPayloadWithAttachmentAsync(
            string trigger,
            object data,
            string? startupBaseUrl,
            HttpClient httpClient,
            IHttpContextAccessor? httpContextAccessor = null,
            Action<string>? logInfo = null,
            Action<Exception, string>? logDebug = null)
        {
            var (payload, attachment) = await NotificationPayloadBuilder.CreateDiscordPayloadWithAttachmentAsync(
                trigger,
                data,
                startupBaseUrl,
                httpClient,
                httpContextAccessor,
                logInfo,
                logDebug);

            NotificationAttachmentInfo? mapped = null;
            if (attachment != null)
            {
                mapped = new NotificationAttachmentInfo
                {
                    ImageData = attachment.ImageData,
                    Filename = attachment.Filename,
                    ContentType = attachment.ContentType
                };
            }

            return (payload, mapped);
        }
    }
}
