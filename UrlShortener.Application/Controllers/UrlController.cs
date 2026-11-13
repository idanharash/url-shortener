using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UrlShortener.Application.Dto;
using UrlShortener.Application.Settings;
using UrlShortener.Model.Service;

namespace UrlShortener.Application.Controllers;

[ApiController]
[Route("api")]
public class UrlController(IUrlService urlService, AppSettings settings) : ControllerBase
{
    // Creating links requires an API key; redirects and stats stay public.
    [Authorize]
    [HttpPost("shorten")]
    public async Task<IActionResult> ShortenUrl([FromBody] UrlRequestDto request)
    {
        return Ok(await urlService.CreateShortUrl(settings.PublicBaseUrl.TrimEnd('/'), request.Url));
    }

    // 302, not 301: browsers cache permanent redirects, so repeat visits would never reach
    // the service and would not be counted.
    [HttpGet("/{code}")]
    public async Task<IActionResult> RedirectToOriginal(string code)
    {
        var original = await urlService.GetOriginalUrl(code);
        return original is null ? NotFound() : Redirect(original);
    }
}
