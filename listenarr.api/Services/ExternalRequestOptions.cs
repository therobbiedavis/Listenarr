namespace Listenarr.Api.Services
{
    // Options to control external request behavior (US proxy / domain preference)
    public class ExternalRequestOptions
    {
        // When true, attempts to force .com domains when localized content is detected
        public bool PreferUsDomain { get; set; } = true;

        // When true, use the configured US proxy for requests that must originate in the US
        public bool UseUsProxy { get; set; } = false;

        // Proxy host (e.g., proxy.example.com)
        public string? UsProxyHost { get; set; }

        // Proxy port
        public int UsProxyPort { get; set; } = 0;

        // Optional credentials for the proxy
        public string? UsProxyUsername { get; set; }
        public string? UsProxyPassword { get; set; }
    }
}
