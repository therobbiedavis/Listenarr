using System.Collections.Generic;
using System.Threading.Tasks;

namespace Listenarr.Api.Repositories
{
    public interface IDownloadProcessingJobRepository
    {
        Task<List<string>> GetPendingDownloadIdsAsync(IEnumerable<string> completedDownloadIds);
        Task<List<string>> GetAllJobDownloadIdsAsync(IEnumerable<string> completedDownloadIds);
    }
}
