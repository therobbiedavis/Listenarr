using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Listenarr.Domain.Models;
using Listenarr.Application.Repositories;
using Listenarr.Infrastructure.Models;

namespace Listenarr.Infrastructure.Repositories
{
    public class QualityProfileRepository : IQualityProfileRepository
    {
        private readonly ListenArrDbContext _db;

        public QualityProfileRepository(ListenArrDbContext db)
        {
            _db = db;
        }

        public async Task<List<QualityProfile>> GetAllAsync()
        {
            return await _db.QualityProfiles.ToListAsync();
        }

        public async Task<QualityProfile?> FindByIdAsync(int id)
        {
            return await _db.QualityProfiles.FindAsync(id);
        }

        public async Task<QualityProfile?> GetDefaultAsync()
        {
            return await _db.QualityProfiles.FirstOrDefaultAsync(p => p.IsDefault);
        }

        public async Task<QualityProfile> AddAsync(QualityProfile profile)
        {
            _db.QualityProfiles.Add(profile);
            await _db.SaveChangesAsync();
            return profile;
        }

        public async Task<QualityProfile> UpdateAsync(QualityProfile profile)
        {
            _db.QualityProfiles.Update(profile);
            await _db.SaveChangesAsync();
            return profile;
        }

        public async Task<bool> DeleteAsync(int id)
        {
            var existing = await _db.QualityProfiles.FindAsync(id);
            if (existing == null) return false;
            _db.QualityProfiles.Remove(existing);
            await _db.SaveChangesAsync();
            return true;
        }

        public async Task<int> CountAudiobooksUsingProfileAsync(int profileId)
        {
            return await _db.Audiobooks.CountAsync(a => a.QualityProfileId == profileId);
        }
    }
}
