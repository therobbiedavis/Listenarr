using Listenarr.Api.Models;
using Listenarr.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Listenarr.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class StartupConfigController : ControllerBase
    {
        private readonly IStartupConfigService _startupConfigService;
        private readonly ILogger<StartupConfigController> _logger;

        public StartupConfigController(IStartupConfigService startupConfigService, ILogger<StartupConfigController> logger)
        {
            _startupConfigService = startupConfigService;
            _logger = logger;
        }

    [HttpGet]
    [AllowAnonymous]
    public ActionResult<StartupConfig> Get()
        {
            try
            {
                var cfg = _startupConfigService.GetConfig() ?? new StartupConfig();
                return Ok(cfg);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to return startup config");
                return StatusCode(500, "Internal server error");
            }
        }

        [HttpPost]
        public async Task<ActionResult<StartupConfig>> Save([FromBody] StartupConfig config)
        {
            try
            {
                await _startupConfigService.SaveAsync(config);
                return Ok(config);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to save startup config");
                return StatusCode(500, "Internal server error");
            }
        }
    }
}
