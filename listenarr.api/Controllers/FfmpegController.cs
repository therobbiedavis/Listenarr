using System.IO;
using System.Threading.Tasks;
using Listenarr.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Text.Json;
using System.Runtime.InteropServices;

public class FfprobeScanRequest { public string? FilePath { get; set; } }

namespace Listenarr.Api.Controllers
{
    [ApiController]
    [Route("api/ffmpeg")]
    public class FfmpegController : ControllerBase
    {
        private readonly IFfmpegService _ffmpegService;
        private readonly ILogger<FfmpegController> _logger;

        public FfmpegController(IFfmpegService ffmpegService, ILogger<FfmpegController> logger)
        {
            _ffmpegService = ffmpegService;
            _logger = logger;
        }

        [HttpGet("info")]
        public async Task<IActionResult> GetInfo()
        {
            var path = await _ffmpegService.GetFfprobePathAsync(false);
            var baseDir = Path.Combine(Directory.GetCurrentDirectory(), "config", "ffmpeg");
            var licensePath = Path.Combine(baseDir, "LICENSE_NOTICE.txt");
            string license = string.Empty;
            if (System.IO.File.Exists(licensePath))
            {
                license = await System.IO.File.ReadAllTextAsync(licensePath);
            }

            return Ok(new { ffprobePath = path, licenseNotice = license });
        }

        [HttpPost("scan")]
        public async Task<IActionResult> RunFfprobe([FromBody] FfprobeScanRequest req)
        {
            if (req == null || string.IsNullOrEmpty(req.FilePath)) return BadRequest(new { message = "FilePath is required" });

            var filePath = req.FilePath!;
            var ffprobeName = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "ffprobe.exe" : "ffprobe";
            var ffprobePath = Path.Combine(Directory.GetCurrentDirectory(), "config", "ffmpeg", ffprobeName);

            try
            {
                _logger.LogInformation("Running bundled ffprobe at {Path} against file {File}", ffprobePath, filePath);

                var startInfo = new ProcessStartInfo
                {
                    FileName = ffprobePath,
                    Arguments = $"-v quiet -print_format json -show_format -show_streams \"{filePath}\"",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using var proc = Process.Start(startInfo);
                if (proc == null)
                {
                    _logger.LogWarning("Failed to start ffprobe process for {File}", filePath);
                    return StatusCode(500, new { message = "Failed to start ffprobe process" });
                }

                var stdout = await proc.StandardOutput.ReadToEndAsync();
                var stderr = await proc.StandardError.ReadToEndAsync();
                proc.WaitForExit(10000);

                _logger.LogInformation("ffprobe exit code {Code} for file {File}; stderr length={Len}", proc.ExitCode, filePath, stderr?.Length ?? 0);

                object? parsed = null;
                if (!string.IsNullOrEmpty(stdout))
                {
                    try { parsed = JsonSerializer.Deserialize<JsonElement>(stdout); }
                    catch (Exception jex) { _logger.LogDebug(jex, "Failed to parse ffprobe JSON output for {File}", filePath); }
                }

                return Ok(new { ffprobePath, exitCode = proc.ExitCode, stdout, stderr, parsed });
            }
            catch (System.ComponentModel.Win32Exception wex)
            {
                _logger.LogWarning(wex, "ffprobe execution failed for {File}", filePath);
                return StatusCode(500, new { message = "ffprobe execution failed", error = wex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error running ffprobe for {File}", filePath);
                return StatusCode(500, new { message = "Error running ffprobe", error = ex.Message });
            }
        }
    }
}
