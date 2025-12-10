using System.Threading.Tasks;
using Listenarr.Api.Repositories;
using Listenarr.Domain.Models;

namespace Listenarr.Api.Tests
{
    internal class TestCompletedDownloadProcessor : Listenarr.Api.Services.ICompletedDownloadProcessor
    {
        private readonly IDownloadRepository? _downloadRepo;

        public TestCompletedDownloadProcessor(IDownloadRepository? downloadRepo)
        {
            _downloadRepo = downloadRepo;
        }

        public async Task ProcessCompletedDownloadAsync(string downloadId, string finalPath)
        {
            if (_downloadRepo != null)
            {
                var d = await _downloadRepo.FindAsync(downloadId);
                if (d != null)
                {
                    d.Status = DownloadStatus.Completed;
                    d.FinalPath = finalPath;
                    await _downloadRepo.UpdateAsync(d);
                }
            }
        }
    }
}
