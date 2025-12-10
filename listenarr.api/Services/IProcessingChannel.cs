using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Listenarr.Api.Services
{
    /// <summary>
    /// Abstraction over an in-memory processing channel so consumers can be decoupled from the concrete channel implementation.
    /// </summary>
    public interface IProcessingChannel
    {
        ValueTask EnqueueJobAsync(string jobId, CancellationToken ct = default);
        IAsyncEnumerable<string> ReadAllAsync(CancellationToken ct = default);
        bool TryWrite(string jobId);
    }
}
