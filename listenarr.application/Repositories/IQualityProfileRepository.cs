using System.Collections.Generic;
using System.Threading.Tasks;
using Listenarr.Domain.Models;

namespace Listenarr.Application.Repositories
{
    public interface IQualityProfileRepository
    {
        Task<List<QualityProfile>> GetAllAsync();
        Task<QualityProfile?> FindByIdAsync(int id);
        Task<QualityProfile?> GetDefaultAsync();
        Task<QualityProfile> AddAsync(QualityProfile profile);
        Task<QualityProfile> UpdateAsync(QualityProfile profile);
        Task<bool> DeleteAsync(int id);
        Task<int> CountAudiobooksUsingProfileAsync(int profileId);
    }
}
