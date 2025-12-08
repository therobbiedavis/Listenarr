using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Listenarr.Api.Repositories;
using Microsoft.EntityFrameworkCore;
using Listenarr.Infrastructure.Models;

namespace Listenarr.Api.Tests
{
    /// <summary>
    /// Lightweight test repository used by unit tests when a real IDbContextFactory is not provided.
    /// If a `ListenArrDbContext` is provided to the factory, operations are performed against it;
    /// otherwise this falls back to an in-memory dictionary to allow simple tests to run.
    /// </summary>
    public class TestDownloadRepository : IDownloadRepository
    {
        private readonly ListenArrDbContext? _db;
        private readonly ConcurrentDictionary<string, Download> _mem = new();

        public TestDownloadRepository(ListenArrDbContext? db = null)
        {
            _db = db;
        }

        public Task AddAsync(Download download)
        {
            if (_db != null)
            {
                _db.Downloads.Add(download);
                return _db.SaveChangesAsync();
            }

            _mem[download.Id] = download;
            return Task.CompletedTask;
        }

        public Task<Download?> FindAsync(string id)
        {
            if (_db != null)
                return _db.Downloads.FindAsync(id).AsTask();

            _mem.TryGetValue(id, out var d);
            return Task.FromResult(d);
        }

        public Task UpdateAsync(Download download)
        {
            if (_db != null)
            {
                _db.Downloads.Update(download);
                return _db.SaveChangesAsync();
            }

            _mem[download.Id] = download;
            return Task.CompletedTask;
        }

        public Task UpdateMetadataAsync(string id, string key, object? value)
        {
            if (_db != null)
            {
                var d = _db.Downloads.Find(id);
                if (d == null) return Task.CompletedTask;
                if (d.Metadata == null) d.Metadata = new Dictionary<string, object>();
                d.Metadata[key] = value ?? string.Empty;
                _db.Downloads.Update(d);
                return _db.SaveChangesAsync();
            }

            if (_mem.TryGetValue(id, out var mem))
            {
                if (mem.Metadata == null) mem.Metadata = new Dictionary<string, object>();
                mem.Metadata[key] = value ?? string.Empty;
            }

            return Task.CompletedTask;
        }

        public Task RemoveAsync(string id)
        {
            if (_db != null)
            {
                var d = _db.Downloads.Find(id);
                if (d != null)
                {
                    _db.Downloads.Remove(d);
                    return _db.SaveChangesAsync();
                }
                return Task.CompletedTask;
            }

            _mem.TryRemove(id, out _);
            return Task.CompletedTask;
        }

        public Task<List<Download>> GetAllAsync()
        {
            if (_db != null)
                return _db.Downloads.ToListAsync();

            return Task.FromResult(_mem.Values.ToList());
        }

        public Task<List<Download>> GetByClientAsync(string clientId)
        {
            if (_db != null)
                return _db.Downloads.Where(d => d.DownloadClientId == clientId).ToListAsync();

            var list = _mem.Values.Where(d => d.DownloadClientId == clientId).ToList();
            return Task.FromResult(list);
        }

        public Task<List<Download>> GetByIdsAsync(IEnumerable<string> ids)
        {
            var idSet = ids?.ToList() ?? new List<string>();
            if (_db != null)
                return _db.Downloads.Where(d => idSet.Contains(d.Id)).ToListAsync();

            var list = _mem.Values.Where(d => idSet.Contains(d.Id)).ToList();
            return Task.FromResult(list);
        }
    }
}
