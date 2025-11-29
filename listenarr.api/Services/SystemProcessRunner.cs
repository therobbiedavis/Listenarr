using System;
using System.Diagnostics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Listenarr.Api.Services
{
    public class SystemProcessRunner : IProcessRunner
    {
        private readonly ILogger<SystemProcessRunner> _logger;

        public SystemProcessRunner(ILogger<SystemProcessRunner> logger)
        {
            _logger = logger;
        }

        public async Task<ProcessResult> RunAsync(ProcessStartInfo startInfo, int timeoutMs = 60000, CancellationToken cancellationToken = default)
        {
            var stdout = new StringBuilder();
            var stderr = new StringBuilder();

            using var process = new Process();
            process.StartInfo = startInfo;
            process.StartInfo.RedirectStandardOutput = true;
            process.StartInfo.RedirectStandardError = true;
            process.StartInfo.UseShellExecute = false;

            process.OutputDataReceived += (s, e) => { if (e.Data != null) stdout.AppendLine(e.Data); };
            process.ErrorDataReceived += (s, e) => { if (e.Data != null) stderr.AppendLine(e.Data); };

            try
            {
                process.Start();
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();

                var exited = await Task.Run(() => process.WaitForExit(timeoutMs), cancellationToken);
                if (!exited)
                {
                    try { process.Kill(true); } catch { }
                    _logger.LogWarning("Process timed out after {Timeout}ms: {FileName} {Args}", timeoutMs, startInfo.FileName, startInfo.Arguments);
                    return new ProcessResult(-1, stdout.ToString(), stderr.ToString(), true);
                }

                var exit = process.ExitCode;
                return new ProcessResult(exit, stdout.ToString(), stderr.ToString(), false);
            }
            catch (OperationCanceledException)
            {
                _logger.LogWarning("Process run cancelled for {File} {Args}", startInfo.FileName, startInfo.Arguments);
                return new ProcessResult(-1, stdout.ToString(), stderr.ToString() + "\nOperationCanceled", false);
            }
            catch (Exception ex)
            {
                // Include exception details in the returned stderr so callers can surface meaningful diagnostics
                var errText = stderr.ToString();
                try
                {
                    errText = string.IsNullOrEmpty(errText) ? ex.ToString() : errText + "\n" + ex.ToString();
                }
                catch { errText = ex.Message; }

                _logger.LogWarning(ex, "Process runner threw an exception for {File} {Args}", startInfo.FileName, startInfo.Arguments);
                return new ProcessResult(-1, stdout.ToString(), errText, false);
            }
        }

        public Process StartProcess(ProcessStartInfo startInfo)
        {
            try
            {
                var process = new Process();
                process.StartInfo = startInfo;
                // Do not override startInfo properties like RedirectStandardOutput/Error or UseShellExecute - caller configures them.
                process.Start();
                return process;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to start process {File} {Args}", startInfo.FileName, startInfo.Arguments);
                throw;
            }
        }
    }
}
