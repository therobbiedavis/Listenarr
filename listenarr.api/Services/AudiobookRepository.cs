using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using Listenarr.Api.Models;

namespace Listenarr.Api.Services
{
    public class AudiobookRepository : IAudiobookRepository
    {
        private readonly ListenArrDbContext _db;
        public AudiobookRepository(ListenArrDbContext db)
        {
            _db = db;
        }

        public async Task<List<Audiobook>> GetAllAsync()
        {
            return await _db.Audiobooks.OrderBy(a => a.Title).ToListAsync();
        }

        public async Task<Audiobook?> GetByAsinAsync(string asin)
        {
            return await _db.Audiobooks.FirstOrDefaultAsync(a => a.Asin == asin);
        }

        public async Task<Audiobook?> GetByIsbnAsync(string isbn)
        {
            return await _db.Audiobooks.FirstOrDefaultAsync(a => a.Isbn == isbn);
        }

        public async Task<Audiobook?> GetByIdAsync(int id)
        {
            return await _db.Audiobooks.FirstOrDefaultAsync(a => a.Id == id);
        }

        public async Task AddAsync(Audiobook audiobook)
        {
            _db.Audiobooks.Add(audiobook);
            await _db.SaveChangesAsync();
        }

        public async Task<bool> DeleteAsync(Audiobook audiobook)
        {
            _db.Audiobooks.Remove(audiobook);
            await _db.SaveChangesAsync();
            return true;
        }

        public async Task<bool> DeleteByIdAsync(int id)
        {
            var audiobook = await GetByIdAsync(id);
            if (audiobook == null)
                return false;

            return await DeleteAsync(audiobook);
        }

        public async Task<int> DeleteBulkAsync(List<int> ids)
        {
            var audiobooks = await _db.Audiobooks
                .Where(a => ids.Contains(a.Id))
                .ToListAsync();

            if (!audiobooks.Any())
                return 0;

            _db.Audiobooks.RemoveRange(audiobooks);
            await _db.SaveChangesAsync();
            return audiobooks.Count;
        }
    }
}
