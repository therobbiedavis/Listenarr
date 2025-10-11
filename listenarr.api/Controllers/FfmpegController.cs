using System.IO;
using System.Threading.Tasks;
using Listenarr.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace Listenarr.Api.Controllers
{
    [ApiController]
    [Route("api/ffmpeg")]
    public class FfmpegController : ControllerBase
    {
        private readonly IFfmpegService _ffmpegService;

        public FfmpegController(IFfmpegService ffmpegService)
        {
            _ffmpegService = ffmpegService;
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
    }
}
