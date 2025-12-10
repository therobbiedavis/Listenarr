using System.Collections.Generic;

namespace Listenarr.Api.Services.Adapters
{
    // Options for a single download client instance
    public class DownloadClientOptions
    {
        public string? Id { get; set; }          // logical id, e.g. "home-qbit"
        public string? Type { get; set; }        // client type, e.g. "qbittorrent", "transmission"
        public string? Host { get; set; }
        public int Port { get; set; }
        public bool UseSSL { get; set; }
        public string? Username { get; set; }
        public string? Password { get; set; }
        public string? ApiKey { get; set; }
        public string? DownloadPath { get; set; }
    }

    // Top-level binding for multiple download clients
    public class DownloadClientsOptions
    {
        // key = logical id or name from configuration, value = options
        public Dictionary<string, DownloadClientOptions> Clients { get; set; } = new();
    }
}
