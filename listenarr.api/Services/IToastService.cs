using System.Threading.Tasks;

namespace Listenarr.Api.Services
{
    public interface IToastService
    {
        Task PublishToastAsync(string level, string title, string message, int? timeoutMs = null);
    }
}
