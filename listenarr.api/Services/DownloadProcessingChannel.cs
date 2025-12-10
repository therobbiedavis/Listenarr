using System.Threading.Channels;

namespace Listenarr.Api.Services
{
    /// <summary>
    /// Lightweight in-memory channel to publish newly queued download processing job IDs
    /// for in-memory consumers to react to immediately (best-effort; DB remains source of truth).
    /// </summary>
    public class DownloadProcessingChannel : IProcessingChannel
    {
        private readonly Channel<string> _channel;

        public DownloadProcessingChannel()
        {
            // Unbounded channel: callers expect enqueue to succeed without blocking. Use single-writer/multi-reader semantics.
            _channel = Channel.CreateUnbounded<string>(new UnboundedChannelOptions
            {
                SingleReader = false,
                SingleWriter = false,
                AllowSynchronousContinuations = false
            });
        }

        public async ValueTask EnqueueJobAsync(string jobId, CancellationToken ct = default)
        {
            await _channel.Writer.WriteAsync(jobId, ct).ConfigureAwait(false);
        }

        public IAsyncEnumerable<string> ReadAllAsync(CancellationToken ct = default)
        {
            return _channel.Reader.ReadAllAsync(ct);
        }

        public bool TryWrite(string jobId) => _channel.Writer.TryWrite(jobId);
    }
}
