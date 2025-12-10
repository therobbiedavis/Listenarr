// csharp
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using Listenarr.Domain.Models;
using Listenarr.Infrastructure.Models;

namespace Listenarr.Api.Services
{
    /// <summary>
    /// Moved AudiobookRepository implementation into the Infrastructure project.
    /// Keeps the original namespace Listenarr.Api.Services so existing code and DI registrations
    /// don't need to change.
    /// </summary>
    public class AudiobookRepository : IAudiobookRepository
    {
        private readonly ListenArrDbContext _db;
        public AudiobookRepository(ListenArrDbContext db)
        {
            _db = db;
        }

        public async Task<List<Audiobook>> GetAllAsync()
        {
            // Include Files so callers that fetch the full library will receive file records
            return await _db.Audiobooks
                .Include(a => a.Files)
                .OrderBy(a => a.Title)
                .ToListAsync();
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
            // Include QualityProfile and Files for callers that need full audiobook details
            return await _db.Audiobooks
                .Include(a => a.QualityProfile)
                .Include(a => a.Files)
                .FirstOrDefaultAsync(a => a.Id == id);
        }

        public async Task AddAsync(Audiobook audiobook)
        {
            _db.Audiobooks.Add(audiobook);
            await _db.SaveChangesAsync();
        }

        public async Task<bool> UpdateAsync(Audiobook audiobook)
        {
            // Defensive: preserve existing BasePath if the incoming audiobook doesn't provide one
            try
            {
                var existing = await _db.Audiobooks.AsNoTracking().FirstOrDefaultAsync(a => a.Id == audiobook.Id);
                if (existing != null && string.IsNullOrEmpty(audiobook.BasePath) && !string.IsNullOrEmpty(existing.BasePath))
                {
                    audiobook.BasePath = existing.BasePath;
                }
            }
            catch
            {
                // If anything goes wrong reading existing record, fall back to update behavior
            }

            _db.Audiobooks.Update(audiobook);
            await _db.SaveChangesAsync();
            return true;
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
