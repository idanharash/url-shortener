using Microsoft.AspNetCore.Mvc;
using UrlShortener.Application.Dto;
using UrlShortener.Model.Observability;
using UrlShortener.Model.Service;

namespace UrlShortener.Application.Controllers
{
    [ApiController]
    [Route("api")]
    public class UrlController(IUrlService urlService, IAppTracer tracer) : ControllerBase
    {
        private readonly IUrlService _service = urlService;
        private readonly IAppTracer _tracer = tracer;

        [HttpPost("shorten")]
        public async Task<IActionResult> ShortenUrl([FromBody] UrlRequestDto request)
        {
            return Ok(await _service.CreateShortUrl($"{Request.Scheme}://{Request.Host}", request.Url));
        }

        [HttpGet("/{code}")]
        public async Task<IActionResult> RedirectToOriginal(string code)
        {
            return await _tracer.TraceAsync<IActionResult>("RedirectToOriginal", "App", async activity =>
            {
                activity?.SetTag("url.code", code);

                var original = await _service.GetOriginalUrl(code);
                return original == null
                    ? new NotFoundResult()
                    : new RedirectResult(original, permanent: true);
            });
        }

    }

}
