using System.Collections.Generic;
using System.Threading.Tasks;

namespace Listenarr.Api.Services
{
    /// <summary>
    /// Interface for AudnexusService to allow easier testing and DI.
    /// </summary>
    public interface IAudnexusService
    {
        Task<AudnexusBookResponse?> GetBookMetadataAsync(string asin, string region = "us", bool seedAuthors = true, bool update = false);
        Task<List<AudnexusAuthorSearchResult>?> SearchAuthorsAsync(string name, string region = "us");
        Task<AudnexusAuthorResponse?> GetAuthorAsync(string asin, string region = "us", bool update = false);
        Task<AudnexusChapterResponse?> GetChaptersAsync(string asin, string region = "us", bool update = false);
    }
}
