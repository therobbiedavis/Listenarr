using Microsoft.AspNetCore.Mvc;
using Listenarr.Api.Services;

namespace Listenarr.Api.Controllers
{
    [ApiController]
    [Route("api/diagnostics")]
    public class DiagnosticsController : ControllerBase
    {
        private readonly PlaywrightInstallStatus _status;

        public DiagnosticsController(PlaywrightInstallStatus status)
        {
            _status = status;
        }

            [HttpPost("playwright/install")]
            public async Task<IActionResult> TriggerPlaywrightInstall([FromServices] IPlaywrightInstaller installer, CancellationToken ct)
            {
                try
                {
                    var (success, outText, errText, exitCode) = await installer.InstallOnceAsync(ct).ConfigureAwait(false);
                    return Ok(new
                    {
                        success,
                        exitCode,
                        stdout = string.IsNullOrEmpty(outText) ? null : outText,
                        stderr = string.IsNullOrEmpty(errText) ? null : errText
                    });
                }
                catch (Exception ex)
                {
                    return StatusCode(500, new { error = ex.Message });
                }
            }

        [HttpGet("playwright")]
        public IActionResult GetPlaywrightStatus()
        {
            return Ok(new
            {
                installed = _status.IsInstalled,
                lastAttempt = _status.LastAttempt,
                lastError = _status.LastError,
                lastOutputSnippet = string.IsNullOrEmpty(_status.LastOutput) ? null : (_status.LastOutput.Length > 200 ? _status.LastOutput.Substring(0, 200) + "..." : _status.LastOutput)
            });
        }
    }
}
