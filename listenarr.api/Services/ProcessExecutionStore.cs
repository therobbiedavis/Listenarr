using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Listenarr.Domain.Models;
using Listenarr.Infrastructure.Models;

namespace Listenarr.Api.Services
{
    public interface IProcessExecutionStore
    {
        Task SaveAsync(ProcessResult result, string? source = null, ProcessStartInfo? startInfo = null, CancellationToken cancellationToken = default);
    }

    public class ProcessExecutionStore : IProcessExecutionStore
    {
        private readonly ListenArrDbContext _db;

        public ProcessExecutionStore(ListenArrDbContext db)
        {
            _db = db;
        }

        public async Task SaveAsync(ProcessResult result, string? source = null, ProcessStartInfo? startInfo = null, CancellationToken cancellationToken = default)
        {
            try
            {
                var entity = new ProcessExecutionLog
                {
                    Timestamp = DateTimeOffset.UtcNow,
                    Source = source,
                    FileName = startInfo?.FileName,
                    Arguments = startInfo?.Arguments,
                    ExitCode = result.ExitCode,
                    TimedOut = result.TimedOut,
                    Stdout = result.Stdout,
                    Stderr = result.Stderr,
                    DurationMs = null
                };

                _db.ProcessExecutionLogs.Add(entity);
                await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            }
            catch
            {
                // Swallow errors here - persistence is best-effort to avoid disrupting process flows.
            }
        }
    }
}

