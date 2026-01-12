using Listenarr.Domain.Models;
using Listenarr.Infrastructure.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Listenarr.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Route("api/v3/[controller]")]
    public class TagController : ControllerBase
    {
        private readonly ListenArrDbContext _context;

        public TagController(ListenArrDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<Tag>>> GetTags()
        {
            return Ok(await _context.Tags.ToListAsync());
        }

        [HttpPost]
        public async Task<ActionResult<Tag>> CreateTag([FromBody] Tag tag)
        {
            _context.Tags.Add(tag);
            await _context.SaveChangesAsync();
            return CreatedAtAction(nameof(GetTags), new { id = tag.Id }, tag);
        }
    }
}
