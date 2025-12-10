using System.Collections.Generic;
using System.Threading.Tasks;

// Backwards-compatibility shim: forward API-level interface to the application-level abstraction.
// This allows existing code that referenced Listenarr.Api.Services.IHubBroadcaster to continue
// compiling while consumers move to `Listenarr.Application.Services.IHubBroadcaster`.
namespace Listenarr.Api.Services
{
    public interface IHubBroadcaster : Listenarr.Application.Services.IHubBroadcaster
    {
    }
}
