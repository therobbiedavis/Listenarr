// csharp
using System;
using System.Collections.Generic;
using System.Linq;

namespace Listenarr.Api.Services.Adapters
{
    public interface IDownloadClientAdapterFactory
    {
        IDownloadClientAdapter GetByIdOrType(string id);
    }

    public class DownloadClientAdapterFactory : IDownloadClientAdapterFactory
    {
        private readonly Dictionary<string, IDownloadClientAdapter> _byId;
        private readonly Dictionary<string, IDownloadClientAdapter> _byType;
        private readonly IDownloadClientAdapter? _default;

        public DownloadClientAdapterFactory(IEnumerable<IDownloadClientAdapter> adapters)
        {
            var list = adapters?.ToList() ?? new List<IDownloadClientAdapter>();
            _byId = list
                .Where(a => !string.IsNullOrWhiteSpace(a.ClientId))
                .GroupBy(a => a.ClientId!, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

            _byType = list
                .Where(a => !string.IsNullOrWhiteSpace(a.ClientType))
                .GroupBy(a => a.ClientType!, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

            _default = list.FirstOrDefault();
        }

        public IDownloadClientAdapter GetByIdOrType(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                if (_default != null) return _default;
                throw new InvalidOperationException("No IDownloadClientAdapter implementations are registered.");
            }

            if (_byId.TryGetValue(id, out var byId)) return byId;
            if (_byType.TryGetValue(id, out var byType)) return byType;
            if (_default != null) return _default;

            throw new InvalidOperationException("No IDownloadClientAdapter implementations are registered.");
        }
    }
}
