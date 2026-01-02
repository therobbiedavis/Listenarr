using System.Collections.Generic;
using System.Threading.Tasks;
using Listenarr.Domain.Models;

namespace Listenarr.Api.Services
{
    public interface IRootFolderService
    {
        Task<List<RootFolder>> GetAllAsync();
        Task<RootFolder?> GetByIdAsync(int id);
        Task<RootFolder> CreateAsync(RootFolder root);
        // moveFiles: when true, enqueue move jobs for affected audiobooks; when false, perform DB-only reassign
        Task<RootFolder> UpdateAsync(RootFolder root, bool moveFiles = false, bool deleteEmptySource = true);
        Task DeleteAsync(int id, int? reassignRootId = null);
    }
}