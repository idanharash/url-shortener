using Microsoft.AspNetCore.Mvc;
using UrlShortener.Model.Service;

namespace UrlShortener.Application.Controllers
{
    [ApiController]
    [Route("api/stats")]
    public class StatsController : ControllerBase
    {
        private readonly IStatsService _urlService;

        public StatsController(IStatsService urlService)
        {
            _urlService = urlService;
        }
        [HttpGet("{code}")]
        public async Task<IActionResult> GetByCode(string code)
        {
            return Ok(await _urlService.GetByCode(code));
        }
    }
}
