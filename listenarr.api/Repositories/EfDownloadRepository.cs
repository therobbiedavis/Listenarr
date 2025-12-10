using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Listenarr.Infrastructure.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Listenarr.Api.Repositories
{
    public class EfDownloadRepository : IDownloadRepository
    {
        private readonly IDbContextFactory<ListenArrDbContext> _dbFactory;
        private readonly ILogger<EfDownloadRepository> _logger;

        public EfDownloadRepository(IDbContextFactory<ListenArrDbContext> dbFactory, ILogger<EfDownloadRepository> logger)
        {
            _dbFactory = dbFactory ?? throw new ArgumentNullException(nameof(dbFactory));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task AddAsync(Download download)
        {
            var ctx = await _dbFactory.CreateDbContextAsync();
            ctx.Downloads.Add(download);
            await ctx.SaveChangesAsync();
        }

        public async Task<Download?> FindAsync(string id)
        {
            var ctx = await _dbFactory.CreateDbContextAsync();
            return await ctx.Downloads.FindAsync(id);
        }

        public async Task UpdateAsync(Download download)
        {
            var ctx = await _dbFactory.CreateDbContextAsync();
            ctx.Downloads.Update(download);
            await ctx.SaveChangesAsync();
        }

        public async Task UpdateMetadataAsync(string id, string key, object? value)
        {
            var ctx = await _dbFactory.CreateDbContextAsync();
            var d = await ctx.Downloads.FindAsync(id);
            if (d == null) return;
            if (d.Metadata == null) d.Metadata = new System.Collections.Generic.Dictionary<string, object>();
            d.Metadata[key] = value ?? string.Empty;
            ctx.Downloads.Update(d);
            await ctx.SaveChangesAsync();
        }

        public async Task RemoveAsync(string id)
        {
            var ctx = await _dbFactory.CreateDbContextAsync();
            var d = await ctx.Downloads.FindAsync(id);
            if (d == null) return;
            ctx.Downloads.Remove(d);
            await ctx.SaveChangesAsync();
        }

        public async Task<List<Download>> GetAllAsync()
        {
            var ctx = await _dbFactory.CreateDbContextAsync();
            return await ctx.Downloads.ToListAsync();
        }

        public async Task<List<Download>> GetByClientAsync(string clientId)
        {
            var ctx = await _dbFactory.CreateDbContextAsync();
            return await ctx.Downloads
                .Where(d => d.DownloadClientId == clientId)
                .ToListAsync();
        }

        public async Task<List<Download>> GetByIdsAsync(IEnumerable<string> ids)
        {
            var idSet = ids?.ToList() ?? new List<string>();
            if (!idSet.Any()) return new List<Download>();
            var ctx = await _dbFactory.CreateDbContextAsync();
            return await ctx.Downloads
                .Where(d => idSet.Contains(d.Id))
                .ToListAsync();
        }
    }
}
