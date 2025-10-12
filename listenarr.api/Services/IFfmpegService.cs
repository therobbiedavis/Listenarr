using System.Threading.Tasks;

namespace Listenarr.Api.Services
{
    public interface IFfmpegService
    {
        /// <summary>
        /// Return the full path to ffprobe if present in the application's configured directory.
        /// This method will NOT attempt to download or install ffprobe when called. It only
        /// checks for an existing bundled binary and returns the path or null.
        /// </summary>
        Task<string?> GetFfprobePathAsync(bool ensureInstalled = false);

        /// <summary>
        /// Ensure that ffprobe is installed into the application's bundled directory. This
        /// performs the download/extract/install flow when a binary is not already present.
        /// Intended to be called once at program startup; other callers should prefer
        /// GetFfprobePathAsync(false) to avoid triggering an installer run.
        /// Returns the installed path or null if not available.
        /// </summary>
        Task<string?> EnsureFfprobeInstalledAsync();
    }
}
