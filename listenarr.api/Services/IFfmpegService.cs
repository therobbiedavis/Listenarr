using System.Threading.Tasks;

namespace Listenarr.Api.Services
{
    public interface IFfmpegService
    {
        /// <summary>
        /// Return the full path to ffprobe, installing it into config/ffmpeg when necessary.
        /// Returns null if installation failed or was disabled.
        /// </summary>
        Task<string?> GetFfprobePathAsync(bool ensureInstalled = true);
    }
}
