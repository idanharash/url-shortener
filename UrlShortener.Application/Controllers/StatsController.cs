using Microsoft.AspNetCore.Mvc;
using UrlShortener.Model.Service;

namespace UrlShortener.Application.Controllers;

[ApiController]
[Route("api/stats")]
public class StatsController(IStatsService statsService) : ControllerBase
{
    [HttpGet("{code}")]
    public async Task<IActionResult> GetByCode(string code)
    {
        var stats = await statsService.GetByCode(code);
        return stats is null ? NotFound() : Ok(stats);
    }
}
