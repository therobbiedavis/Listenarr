using System.Collections.Generic;
using System.Threading.Tasks;

namespace Listenarr.Api.Services
{
    public interface IDownloadQueueService
    {
        Task<List<QueueItem>> GetQueueAsync();
    }
}
