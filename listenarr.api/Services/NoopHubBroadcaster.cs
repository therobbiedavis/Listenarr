using System.Collections.Generic;
using System.Threading.Tasks;
using Listenarr.Application.Services;
using Listenarr.Domain.Models;

namespace Listenarr.Api.Services
{
    // Minimal no-op broadcaster used as a safe fallback when the real
    // SignalR broadcaster hasn't been registered in a test service provider.
    public class NoopHubBroadcaster : IHubBroadcaster
    {
        public Task BroadcastQueueUpdateAsync(List<QueueItem> queue)
        {
            // Intentionally do nothing in tests or lightweight hosts
            return Task.CompletedTask;
        }
    }
}
