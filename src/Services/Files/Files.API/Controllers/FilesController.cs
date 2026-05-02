using Files.API.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Files.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class FilesController : ControllerBase
    {
        private readonly IFileStorageService     _storage;
        private readonly ILogger<FilesController> _logger;

        public FilesController(IFileStorageService storage, ILogger<FilesController> logger)
        {
            _storage = storage;
            _logger  = logger;
        }

        [HttpPost("upload")]
        [RequestSizeLimit(50 * 1024 * 1024)]
        public async Task<IActionResult> Upload(IFormFile file, CancellationToken ct)
        {
            if (file == null || file.Length == 0)
                return BadRequest(new { error = "No file provided" });

            try
            {
                var result = await _storage.SaveAsync(file, ct);
                return Ok(result);
            }
            catch (InvalidOperationException ex)
            {
                _logger?.LogWarning(ex, "File upload rejected");
                return BadRequest(new { error = ex.Message });
            }
        }

        [HttpGet("{fileId}")]
        public IActionResult Download(string fileId)
        {
            if (!_storage.TryOpenRead(fileId, out var stream, out var contentType))
                return NotFound(new { error = "File not found" });

            return File(stream, contentType, fileId);
        }

        [HttpGet]
        public IActionResult List() => Ok(_storage.List());

        [HttpDelete("{fileId}")]
        public IActionResult Delete(string fileId)
        {
            return _storage.Delete(fileId)
                ? (IActionResult)NoContent()
                : NotFound(new { error = "File not found" });
        }
    }
}
