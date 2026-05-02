using Files.API.Configuration;
using Files.API.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Files.API.Services
{
    public class LocalFileStorageService : IFileStorageService
    {
        private readonly FileStorageOptions          _options;
        private readonly ILogger<LocalFileStorageService> _logger;

        public LocalFileStorageService(
            IOptions<FileStorageOptions>         options,
            ILogger<LocalFileStorageService>     logger)
        {
            _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
            _logger  = logger;
            Directory.CreateDirectory(_options.Path);
        }

        public async Task<UploadResult> SaveAsync(IFormFile file, CancellationToken ct = default)
        {
            if (file == null || file.Length == 0)
                throw new ArgumentException("File is empty or missing", nameof(file));

            if (file.Length > _options.MaxFileSize)
                throw new InvalidOperationException(
                    $"File size {file.Length} exceeds limit of {_options.MaxFileSize} bytes");

            var ext      = Path.GetExtension(file.FileName);
            var fileId   = Guid.NewGuid().ToString("N") + ext;
            var fullPath = ResolvePath(fileId);

            await using var stream = File.Create(fullPath);
            await file.CopyToAsync(stream, ct);

            _logger?.LogInformation("Stored file {FileId} ({Size} bytes)", fileId, file.Length);

            return new UploadResult
            {
                FileId       = fileId,
                OriginalName = file.FileName,
                Size         = file.Length
            };
        }

        public bool TryOpenRead(string fileId, out Stream stream, out string contentType)
        {
            stream      = null;
            contentType = null;

            if (!IsSafeFileId(fileId)) return false;

            var fullPath = ResolvePath(fileId);
            if (!File.Exists(fullPath)) return false;

            stream      = File.OpenRead(fullPath);
            contentType = GetContentType(fileId);
            return true;
        }

        public IReadOnlyList<FileMetadata> List()
        {
            if (!Directory.Exists(_options.Path))
                return Array.Empty<FileMetadata>();

            return Directory.EnumerateFiles(_options.Path)
                .Select(path =>
                {
                    var info = new FileInfo(path);
                    return new FileMetadata
                    {
                        FileId    = info.Name,
                        Size      = info.Length,
                        CreatedAt = info.CreationTimeUtc
                    };
                })
                .OrderByDescending(f => f.CreatedAt)
                .ToList();
        }

        public bool Delete(string fileId)
        {
            if (!IsSafeFileId(fileId)) return false;

            var fullPath = ResolvePath(fileId);
            if (!File.Exists(fullPath)) return false;

            File.Delete(fullPath);
            _logger?.LogInformation("Deleted file {FileId}", fileId);
            return true;
        }

        private string ResolvePath(string fileId) => Path.Combine(_options.Path, fileId);

        // Защита от path traversal: разрешаем только имя файла без разделителей.
        private static bool IsSafeFileId(string fileId)
        {
            if (string.IsNullOrWhiteSpace(fileId))         return false;
            if (fileId.Contains('/') || fileId.Contains('\\')) return false;
            if (fileId.Contains("..", StringComparison.Ordinal)) return false;
            return fileId == Path.GetFileName(fileId);
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
