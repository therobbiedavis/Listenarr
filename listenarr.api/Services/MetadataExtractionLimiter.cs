using System.Threading;

namespace Listenarr.Api.Services
{
    public class MetadataExtractionLimiter
    {
        // Default concurrent ffprobe extractions
        public SemaphoreSlim Sem { get; } = new SemaphoreSlim(4);
    }
}
