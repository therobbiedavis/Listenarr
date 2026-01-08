using System.Threading.Tasks;

namespace Listenarr.Api.Services
{
    public interface ILegacyOutputPathMigrator
    {
        Task MigrateAsync();
    }
}