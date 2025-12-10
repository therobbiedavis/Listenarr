using System.Threading;
using System.Threading.Tasks;
using Listenarr.Domain.Models;

namespace Listenarr.Api.Services
{
    public interface INzbUrlResolver
    {
        Task<(string Url, string? IndexerApiKey)> ResolveAsync(SearchResult result, CancellationToken ct = default);
    }
}
