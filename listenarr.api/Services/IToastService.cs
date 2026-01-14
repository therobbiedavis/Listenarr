using System.Threading.Tasks;

namespace Listenarr.Api.Services
{
    public interface IToastService
    {
        Task PublishToastAsync(string level, string title, string message, int? timeoutMs = null);

        /// <summary>
        /// Publish a notification to the activity dropdown without triggering a popup toast message.
        /// This is useful for server-driven events where clients already display context-specific toasts
        /// (eg. when broadcasting an IndexersUpdated event the client will show a toast, so the server
        /// should only create a notification to populate the activity bell).
        /// </summary>
        Task PublishNotificationAsync(string title, string message, string? icon = null, int? timeoutMs = null);
    }
}
