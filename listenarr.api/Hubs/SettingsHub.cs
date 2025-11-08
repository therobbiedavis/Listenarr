using Microsoft.AspNetCore.SignalR;

namespace Listenarr.Api.Hubs
{
    /// <summary>
    /// SignalR hub for broadcasting settings updates to connected clients
    /// Used by the Discord bot to receive real-time configuration changes
    /// </summary>
    public class SettingsHub : Hub
    {
        // This hub is primarily used for server-to-client communication
        // Clients can connect to receive settings updates when they change
    }
}