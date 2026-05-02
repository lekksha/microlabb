using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Files.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class FilesController : ControllerBase
    {
        private readonly string _storageDir;

        public FilesController(IConfiguration config)
        {
            _storageDir = config["StoragePath"] ?? "/app/uploads";
            Directory.CreateDirectory(_storageDir);
        }

        [HttpPost("upload")]
        public async Task<IActionResult> Upload(IFormFile file)
        {
            if (file == null || file.Length == 0)
                return BadRequest(new { error = "No file provided" });

            var ext      = Path.GetExtension(file.FileName);
            var fileId   = Guid.NewGuid().ToString("N") + ext;
            var filePath = Path.Combine(_storageDir, fileId);

            using var stream = System.IO.File.Create(filePath);
            await file.CopyToAsync(stream);

            return Ok(new { fileId, originalName = file.FileName, size = file.Length });
        }

        [HttpGet("{fileId}")]
        public IActionResult Download(string fileId)
        {
            var filePath = Path.Combine(_storageDir, fileId);
            if (!System.IO.File.Exists(filePath))
                return NotFound(new { error = "File not found" });

            var stream      = System.IO.File.OpenRead(filePath);
            var contentType = GetContentType(fileId);
            return File(stream, contentType, fileId);
        }

        [HttpGet]
        public IActionResult List()
        {
            if (!Directory.Exists(_storageDir))
                return Ok(Array.Empty<object>());

            var files = Directory.GetFiles(_storageDir)
                .Select(f => new
                {
                    fileId    = Path.GetFileName(f),
                    size      = new FileInfo(f).Length,
                    createdAt = new FileInfo(f).CreationTimeUtc
                })
                .OrderByDescending(f => f.createdAt);

            return Ok(files);
        }

        [HttpDelete("{fileId}")]
        public IActionResult Delete(string fileId)
        {
            var filePath = Path.Combine(_storageDir, fileId);
            if (!System.IO.File.Exists(filePath))
                return NotFound(new { error = "File not found" });

            System.IO.File.Delete(filePath);
            return NoContent();
        }

        private static string GetContentType(string fileName)
        {
            var ext = Path.GetExtension(fileName).ToLowerInvariant();
            return ext switch
            {
                ".jpg"  => "image/jpeg",
                ".jpeg" => "image/jpeg",
                ".png"  => "image/png",
                ".gif"  => "image/gif",
                ".pdf"  => "application/pdf",
                ".txt"  => "text/plain",
                ".json" => "application/json",
                _       => "application/octet-stream"
            };
        }
    }
}
