using System.Collections.Generic;
using System.Threading.Tasks;
using Listenarr.Domain.Models;

namespace Listenarr.Api.Repositories
{
    public interface IRootFolderRepository
    {
        Task<List<RootFolder>> GetAllAsync();
        Task<RootFolder?> GetByIdAsync(int id);
        Task<RootFolder?> GetByPathAsync(string path);
        Task AddAsync(RootFolder root);
        Task UpdateAsync(RootFolder root);
        Task RemoveAsync(int id);
        Task<RootFolder?> GetDefaultAsync();
    }
}