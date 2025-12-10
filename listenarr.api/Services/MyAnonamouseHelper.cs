using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;

namespace Listenarr.Api.Services
{
    internal static class MyAnonamouseHelper
    {
        private const string DefaultBaseUrl = "https://www.myanonamouse.net";
        private static readonly string[] MamKeys = { "mam_id", "mamid", "mamId", "mamID", "mam" };

        public static string? TryGetMamId(string? additionalSettings)
        {
            if (string.IsNullOrWhiteSpace(additionalSettings))
                return null;

            try
            {
                using var doc = JsonDocument.Parse(additionalSettings);
                return FindMamId(doc.RootElement);
            }
            catch (JsonException)
            {
                return null;
            }
        }

        public static HttpClient CreateAuthenticatedHttpClient(string mamId, string? baseUrl, TimeSpan? timeout = null)
        {
            var handler = new HttpClientHandler
            {
                CookieContainer = BuildCookieContainer(mamId, baseUrl),
                UseCookies = true,
                AutomaticDecompression = DecompressionMethods.All
            };

            var client = new HttpClient(handler);
            client.Timeout = timeout ?? TimeSpan.FromSeconds(60);
            client.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/122.0.0.0 Safari/537.36");
            client.DefaultRequestHeaders.AcceptLanguage.ParseAdd("en-US,en;q=0.9");
            client.DefaultRequestHeaders.Referrer = new Uri(DefaultBaseUrl);
            client.DefaultRequestHeaders.Accept.Clear();
            client.DefaultRequestHeaders.Accept.ParseAdd("application/json, text/javascript, */*; q=0.01");
            return client;
        }

        public static CookieContainer BuildCookieContainer(string mamId, string? baseUrl)
        {
            var container = new CookieContainer();
            var baseUri = NormalizeBaseUri(baseUrl);
            container.Add(baseUri, new Cookie("mam_id", mamId));

            try
            {
                var host = baseUri.Host;
                if (!host.StartsWith("www.", StringComparison.OrdinalIgnoreCase))
                {
                    var wwwUri = new Uri($"{baseUri.Scheme}://www.{host}");
                    container.Add(wwwUri, new Cookie("mam_id", mamId));
                }
            }
            catch
            {
                // Ignore malformed host
            }

            return container;
        }

        public static string ResolveTorrentFileName(HttpResponseMessage response, string torrentUrl)
        {
            var contentDisposition = response.Content.Headers.ContentDisposition;
            if (contentDisposition != null)
            {
                if (!string.IsNullOrWhiteSpace(contentDisposition.FileNameStar))
                    return TrimFileName(contentDisposition.FileNameStar);
                if (!string.IsNullOrWhiteSpace(contentDisposition.FileName))
                    return TrimFileName(contentDisposition.FileName);
            }

            if (Uri.TryCreate(torrentUrl, UriKind.Absolute, out var uri))
            {
                var name = Path.GetFileName(uri.LocalPath);
                if (!string.IsNullOrWhiteSpace(name))
                    return name;
            }

            return "myanonamouse.torrent";
        }

        private static string TrimFileName(string fileName)
        {
            if (string.IsNullOrEmpty(fileName))
                return fileName;

            return fileName.Trim().Trim('"');
        }

        private static Uri NormalizeBaseUri(string? baseUrl)
        {
            var trimmed = baseUrl?.Trim() ?? string.Empty;
            if (string.IsNullOrEmpty(trimmed))
                trimmed = DefaultBaseUrl;

            if (!trimmed.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
                !trimmed.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                trimmed = "https://" + trimmed;
            }

            return new Uri(trimmed);
        }

        private static string? FindMamId(JsonElement element)
        {
            if (element.ValueKind == JsonValueKind.Object)
            {
                foreach (var prop in element.EnumerateObject())
                {
                    if (MamKeys.Any(k => string.Equals(prop.Name, k, StringComparison.OrdinalIgnoreCase)))
                    {
                        if (prop.Value.ValueKind == JsonValueKind.String)
                            return prop.Value.GetString();
                    }

                    if (prop.Value.ValueKind == JsonValueKind.Object || prop.Value.ValueKind == JsonValueKind.Array)
                    {
                        var nested = FindMamId(prop.Value);
                        if (!string.IsNullOrEmpty(nested))
                            return nested;
                    }
                }
            }
            else if (element.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in element.EnumerateArray())
                {
                    var nested = FindMamId(item);
                    if (!string.IsNullOrEmpty(nested))
                        return nested;
                }
            }

            return null;
        }
    }
}
