using Files.API.Models;
using Microsoft.AspNetCore.Http;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Files.API.Services
{
    public interface IFileStorageService
    {
        Task<UploadResult> SaveAsync(IFormFile file, CancellationToken ct = default);

        bool TryOpenRead(string fileId, out Stream stream, out string contentType);

        IReadOnlyList<FileMetadata> List();

        bool Delete(string fileId);
    }
}
