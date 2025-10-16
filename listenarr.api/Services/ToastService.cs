using Microsoft.AspNetCore.SignalR;
using Listenarr.Api.Hubs;

namespace Listenarr.Api.Services
{
    public class ToastService : IToastService
    {
        private readonly IHubContext<DownloadHub> _hub;
        private readonly ILogger<ToastService> _logger;

        public ToastService(IHubContext<DownloadHub> hub, ILogger<ToastService> logger)
        {
            _hub = hub;
            _logger = logger;
        }

        public async Task PublishToastAsync(string level, string title, string message, int? timeoutMs = null)
        {
            try
            {
                var payload = new {
                    level = level ?? "info",
                    title = title ?? string.Empty,
                    message = message ?? string.Empty,
                    timeoutMs = timeoutMs
                };

                await _hub.Clients.All.SendAsync("ToastMessage", payload);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to publish toast message: {Title}", title);
            }
        }
    }
}
