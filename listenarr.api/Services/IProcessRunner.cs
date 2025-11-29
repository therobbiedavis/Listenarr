using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace Listenarr.Api.Services
{
    public record ProcessResult(int ExitCode, string Stdout, string Stderr, bool TimedOut);

    public interface IProcessRunner
    {
        Task<ProcessResult> RunAsync(ProcessStartInfo startInfo, int timeoutMs = 60000, CancellationToken cancellationToken = default);
        // Start a long-running process and return the Process instance so callers can interact with it (kill, read streams, etc.).
        // Implementations should not swallow exceptions - callers rely on the returned Process instance.
        Process StartProcess(ProcessStartInfo startInfo);
    }
}
