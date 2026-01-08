using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Listenarr.Domain.Models;
using Listenarr.Infrastructure.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Listenarr.Api.Repositories
{
    public class EfRootFolderRepository : IRootFolderRepository
    {
        private readonly IDbContextFactory<ListenArrDbContext> _dbFactory;
        private readonly ILogger<EfRootFolderRepository> _logger;

        public EfRootFolderRepository(IDbContextFactory<ListenArrDbContext> dbFactory, ILogger<EfRootFolderRepository> logger)
        {
            _dbFactory = dbFactory;
            _logger = logger;
        }

        public async Task AddAsync(RootFolder root)
        {
            var ctx = await _dbFactory.CreateDbContextAsync();
            ctx.RootFolders.Add(root);
            await ctx.SaveChangesAsync();
        }

        public async Task<List<RootFolder>> GetAllAsync()
        {
            var ctx = await _dbFactory.CreateDbContextAsync();
            return await ctx.RootFolders.OrderBy(r => r.Name).ToListAsync();
        }

        public async Task<RootFolder?> GetByIdAsync(int id)
        {
            var ctx = await _dbFactory.CreateDbContextAsync();
            return await ctx.RootFolders.FindAsync(id);
        }

        public async Task<RootFolder?> GetByPathAsync(string path)
        {
            var ctx = await _dbFactory.CreateDbContextAsync();
            return await ctx.RootFolders.FirstOrDefaultAsync(r => r.Path == path);
        }

        public async Task RemoveAsync(int id)
        {
            var ctx = await _dbFactory.CreateDbContextAsync();
            var r = await ctx.RootFolders.FindAsync(id);
            if (r == null) return;
            ctx.RootFolders.Remove(r);
            await ctx.SaveChangesAsync();
        }

        public async Task UpdateAsync(RootFolder root)
        {
            var ctx = await _dbFactory.CreateDbContextAsync();
            ctx.RootFolders.Update(root);
            await ctx.SaveChangesAsync();
        }

        public async Task<RootFolder?> GetDefaultAsync()
        {
            var ctx = await _dbFactory.CreateDbContextAsync();
            return await ctx.RootFolders.FirstOrDefaultAsync(r => r.IsDefault);
        }
    }
}