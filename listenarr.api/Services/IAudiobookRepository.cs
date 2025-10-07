using System.Threading.Tasks;
using System.Collections.Generic;
using Listenarr.Api.Models;

namespace Listenarr.Api.Services
{
    public interface IAudiobookRepository
    {
        Task<List<Audiobook>> GetAllAsync();
        Task<Audiobook?> GetByAsinAsync(string asin);
        Task<Audiobook?> GetByIsbnAsync(string isbn);
        Task<Audiobook?> GetByIdAsync(int id);
        Task AddAsync(Audiobook audiobook);
        Task<bool> UpdateAsync(Audiobook audiobook);
        Task<bool> DeleteAsync(Audiobook audiobook);
        Task<bool> DeleteByIdAsync(int id);
        Task<int> DeleteBulkAsync(List<int> ids);
    }
}
