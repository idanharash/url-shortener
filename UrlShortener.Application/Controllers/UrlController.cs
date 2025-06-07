using Microsoft.AspNetCore.Mvc;
using UrlShortener.Application.Dto;
using UrlShortener.Model.Service;

namespace UrlShortener.Application.Controllers
{
    [ApiController]
    [Route("api")]
    public class UrlController(IUrlService urlService) : ControllerBase
    {
        private readonly IUrlService _service = urlService;

        [HttpPost("shorten")]
        public async Task<IActionResult> ShortenUrl([FromBody] UrlRequestDto request)
        {
            return Ok(await _service.CreateShortUrl($"{Request.Scheme}://{Request.Host}", request.Url));
        }

        [HttpGet("/{code}")]
        public async Task<IActionResult> RedirectToOriginal(string code)
        {
            var orginal =  await _service.GetOriginalUrl(code);
            if (orginal == null)
                return NotFound();

            return RedirectPermanent(orginal);
        }
    }

}
