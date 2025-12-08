using System.Threading.Tasks;

namespace Listenarr.Api.Services
{
    public interface ICompletedDownloadProcessor
    {
        Task ProcessCompletedDownloadAsync(string downloadId, string finalPath);
    }
}
