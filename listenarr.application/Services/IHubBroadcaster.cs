using System.Collections.Generic;
using System.Threading.Tasks;
using Listenarr.Domain.Models;

namespace Listenarr.Application.Services
{
    public interface IHubBroadcaster
    {
        Task BroadcastQueueUpdateAsync(List<QueueItem> queue);
    }
}
