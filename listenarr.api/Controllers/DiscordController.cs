using Microsoft.AspNetCore.Mvc;
using Listenarr.Api.Services;
using System.Threading.Tasks;

namespace Listenarr.Api.Controllers
{
    /// <summary>
    /// Controller for Discord bot integration endpoints
    /// </summary>
    [ApiController]
    [Route("api/discord")]
    public class DiscordController : ControllerBase
    {
        private readonly IDiscordBotService _discordBotService;

        public DiscordController(IDiscordBotService discordBotService)
        {
            _discordBotService = discordBotService;
        }

        /// <summary>
        /// Get Discord bot status and information
        /// </summary>
        [HttpGet("status")]
        public async Task<ActionResult<object>> GetStatus()
        {
            try
            {
                var status = await _discordBotService.GetBotStatusAsync();
                return Ok(status);
            }
            catch (System.Exception ex)
            {
                return StatusCode(500, new { error = "Failed to get Discord bot status", message = ex.Message });
            }
        }

        /// <summary>
        /// Restart the Discord bot
        /// </summary>
        [HttpPost("restart")]
        public async Task<ActionResult<object>> RestartBot()
        {
            try
            {
                var result = await _discordBotService.RestartBotAsync();
                return Ok(new { success = result, message = result ? "Bot restarted successfully" : "Failed to restart bot" });
            }
            catch (System.Exception ex)
            {
                return StatusCode(500, new { error = "Failed to restart Discord bot", message = ex.Message });
            }
        }

        /// <summary>
        /// Get Discord bot logs
        /// </summary>
        [HttpGet("logs")]
        public async Task<ActionResult<object>> GetLogs([FromQuery] int lines = 100)
        {
            try
            {
                var logs = await _discordBotService.GetBotLogsAsync(lines);
                return Ok(new { logs });
            }
            catch (System.Exception ex)
            {
                return StatusCode(500, new { error = "Failed to get Discord bot logs", message = ex.Message });
            }
        }
    }
}