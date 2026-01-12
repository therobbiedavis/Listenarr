using Listenarr.Domain.Models;
using Listenarr.Infrastructure.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Listenarr.Api.Models;

namespace Listenarr.Api.Controllers
{
    [ApiController]
    [ApiExplorerSettings(IgnoreApi = true)]
    [Route("api/internal/indexer")]
    public class IndexerController : ControllerBase // Legacy/internal helper - hidden from API docs/routing used for manual testing only
    {
        private readonly ListenArrDbContext _context;
        private readonly ILogger<IndexerController> _logger;

        public IndexerController(ListenArrDbContext context, ILogger<IndexerController> logger)
        {
            _context = context;
            _logger = logger;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<IndexerDto>>> GetIndexers()
        {
            var indexers = await _context.Indexers.ToListAsync();
            return Ok(indexers.Select(MapToDto));
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<IndexerDto>> GetIndexer(int id)
        {
            var indexer = await _context.Indexers.FindAsync(id);
            if (indexer == null) return NotFound();
            return Ok(MapToDto(indexer));
        }

        [HttpPost]
        public async Task<ActionResult<IndexerDto>> CreateIndexer([FromBody] IndexerDto dto)
        {
            try
            {
                var indexer = MapFromDto(dto);
                indexer.CreatedAt = DateTime.UtcNow;
                indexer.UpdatedAt = DateTime.UtcNow;

                // Mark added by prowlarr if the name contains the marker or dto flag set
                if (dto.AddedByProwlarr || (!string.IsNullOrEmpty(dto.Name) && dto.Name.Contains("(Prowlarr)", StringComparison.OrdinalIgnoreCase)))
                {
                    indexer.AddedByProwlarr = true;
                }

                _context.Indexers.Add(indexer);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Created indexer {Id} - {Name}", indexer.Id, indexer.Name);
                return CreatedAtAction(nameof(GetIndexer), new { id = indexer.Id }, MapToDto(indexer));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating indexer");
                return BadRequest(new { error = ex.Message });
            }
        }

        [HttpPut("{id}")]
        public async Task<ActionResult<IndexerDto>> UpdateIndexer(int id, [FromBody] IndexerDto dto)
        {
            if (id != dto.Id) return BadRequest("ID mismatch");
            var existing = await _context.Indexers.FindAsync(id);
            if (existing == null) return NotFound();

            try
            {
                existing.Name = dto.Name ?? existing.Name;
                existing.Implementation = dto.Implementation ?? existing.Implementation;
                existing.EnableRss = dto.EnableRss;
                existing.EnableAutomaticSearch = dto.EnableAutomaticSearch;
                existing.EnableInteractiveSearch = dto.EnableInteractiveSearch;
                existing.Priority = dto.Priority;
                existing.Tags = dto.Tags != null && dto.Tags.Any() ? string.Join(',', dto.Tags) : existing.Tags;
                existing.UpdatedAt = DateTime.UtcNow;

                UpdateFieldsFromDto(existing, dto.Fields);

                await _context.SaveChangesAsync();
                _logger.LogInformation("Updated indexer {Id} - {Name}", existing.Id, existing.Name);
                return Ok(MapToDto(existing));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating indexer {Id}", id);
                return BadRequest(new { error = ex.Message });
            }
        }

        [HttpDelete("{id}")]
        public async Task<ActionResult> DeleteIndexer(int id)
        {
            var indexer = await _context.Indexers.FindAsync(id);
            if (indexer == null) return NotFound();

            _context.Indexers.Remove(indexer);
            await _context.SaveChangesAsync();
            _logger.LogInformation("Deleted indexer {Id} - {Name}", indexer.Id, indexer.Name);
            return NoContent();
        }

        [HttpPost("test")]
        public async Task<ActionResult> TestIndexer([FromBody] IndexerDto dto)
        {
            // Basic validation for now
            if (string.IsNullOrEmpty(dto.Name)) return BadRequest(new { error = "Name is required" });
            var baseUrl = dto.Fields?.FirstOrDefault(f => string.Equals(f.Name, "baseUrl", StringComparison.OrdinalIgnoreCase))?.Value?.ToString();
            if (string.IsNullOrEmpty(baseUrl)) return BadRequest(new { error = "Base URL is required" });
            // TODO: perform an actual request to baseUrl + api path to validate indexer
            return Ok(new { success = true, message = "Indexer configuration is valid" });
        }

        [HttpPost("{id}/test")]
        public async Task<ActionResult> TestIndexerById(int id)
        {
            var indexer = await _context.Indexers.FindAsync(id);
            if (indexer == null) return NotFound(new { error = "Indexer not found" });

            // Basic attempt to validate URL
            if (string.IsNullOrEmpty(indexer.Url)) return BadRequest(new { error = "Indexer base URL not configured" });

            // TODO: make an HTTP call to the indexer to validate
            indexer.LastTestedAt = DateTime.UtcNow;
            indexer.LastTestSuccessful = true;
            indexer.LastTestError = null;
            await _context.SaveChangesAsync();

            return Ok(new { success = true, message = "Indexer tested successfully", indexer = MapToDto(indexer) });
        }

        [HttpPut("{id}/toggle")]
        public async Task<ActionResult<IndexerDto>> ToggleIndexer(int id)
        {
            var indexer = await _context.Indexers.FindAsync(id);
            if (indexer == null) return NotFound();

            indexer.IsEnabled = !indexer.IsEnabled;
            indexer.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            return Ok(MapToDto(indexer));
        }

        private IndexerDto MapToDto(Indexer i)
        {
            var fields = new List<IndexerField>();
            if (!string.IsNullOrEmpty(i.Url)) fields.Add(new IndexerField { Name = "baseUrl", Value = i.Url });
            if (!string.IsNullOrEmpty(i.ApiPath)) fields.Add(new IndexerField { Name = "apiPath", Value = i.ApiPath });
            if (!string.IsNullOrEmpty(i.ApiKey)) fields.Add(new IndexerField { Name = "apiKey", Value = i.ApiKey });
            if (!string.IsNullOrEmpty(i.Categories)) fields.Add(new IndexerField { Name = "categories", Value = i.Categories.Split(',').Select(s => s.Trim()).Where(s => !string.IsNullOrEmpty(s)).ToList() });

            return new IndexerDto
            {
                Id = i.Id,
                Name = i.Name,
                Implementation = i.Implementation,
                ConfigContract = i.ConfigContract ?? "NewznabSettings",
                Protocol = i.Type ?? "torrent",
                EnableRss = i.EnableRss,
                EnableAutomaticSearch = i.EnableAutomaticSearch,
                EnableInteractiveSearch = i.EnableInteractiveSearch,
                Priority = i.Priority,
                Fields = fields,
                Tags = !string.IsNullOrEmpty(i.Tags) ? i.Tags.Split(',').Select(s => s.Trim()).Where(s => !string.IsNullOrEmpty(s)).ToList() : new List<string>(),
                AddedByProwlarr = i.AddedByProwlarr,
                ProwlarrIndexerId = i.ProwlarrIndexerId
            };
        }

        private Indexer MapFromDto(IndexerDto dto)
        {
            var indexer = new Indexer
            {
                Name = dto.Name ?? string.Empty,
                Implementation = dto.Implementation ?? "Newznab",
                ConfigContract = dto.ConfigContract,
                Type = dto.Protocol ?? "torrent",
                EnableRss = dto.EnableRss,
                EnableAutomaticSearch = dto.EnableAutomaticSearch,
                EnableInteractiveSearch = dto.EnableInteractiveSearch,
                Priority = dto.Priority,
                Tags = dto.Tags != null && dto.Tags.Any() ? string.Join(',', dto.Tags) : null,
                AddedByProwlarr = dto.AddedByProwlarr,
                ProwlarrIndexerId = dto.ProwlarrIndexerId
            };

            UpdateFieldsFromDto(indexer, dto.Fields);
            return indexer;
        }

        private void UpdateFieldsFromDto(Indexer indexer, List<IndexerField> fields)
        {
            if (fields == null) return;

            foreach (var field in fields)
            {
                switch (field.Name.ToLower())
                {
                    case "baseurl":
                        indexer.Url = field.Value?.ToString();
                        break;
                    case "apipath":
                        indexer.ApiPath = field.Value?.ToString();
                        break;
                    case "apikey":
                        indexer.ApiKey = field.Value?.ToString();
                        break;
                    case "categories":
                        if (field.Value is IEnumerable<object> list)
                        {
                            indexer.Categories = string.Join(',', list.Select(o => o.ToString()));
                        }
                        else
                        {
                            indexer.Categories = field.Value?.ToString();
                        }
                        break;
                }
            }
        }
    }
}
