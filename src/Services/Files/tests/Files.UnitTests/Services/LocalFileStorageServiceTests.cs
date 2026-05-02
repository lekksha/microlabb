using Files.API.Configuration;
using Files.API.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace Files.UnitTests.Services
{
    public class LocalFileStorageServiceTests : IDisposable
    {
        private readonly string                 _tempDir;
        private readonly LocalFileStorageService _service;

        public LocalFileStorageServiceTests()
        {
            _tempDir = Path.Combine(Path.GetTempPath(), "files-tests-" + Guid.NewGuid().ToString("N"));
            var options = Options.Create(new FileStorageOptions
            {
                Path        = _tempDir,
                MaxFileSize = 1024
            });
            _service = new LocalFileStorageService(options, logger: null);
        }

        public void Dispose()
        {
            if (Directory.Exists(_tempDir))
                Directory.Delete(_tempDir, recursive: true);
        }

        [Fact]
        public async Task SaveAsync_WritesFile_AndReturnsMetadata()
        {
            var file = BuildFile("hello.txt", "hello world");

            var result = await _service.SaveAsync(file);

            Assert.NotNull(result);
            Assert.NotNull(result.FileId);
            Assert.EndsWith(".txt", result.FileId);
            Assert.Equal("hello.txt", result.OriginalName);
            Assert.Equal(file.Length, result.Size);
            Assert.True(File.Exists(Path.Combine(_tempDir, result.FileId)));
        }

        [Fact]
        public async Task SaveAsync_GeneratesUniqueIds_ForSameFileName()
        {
            var first  = await _service.SaveAsync(BuildFile("dup.txt", "a"));
            var second = await _service.SaveAsync(BuildFile("dup.txt", "b"));

            Assert.NotEqual(first.FileId, second.FileId);
        }

        [Fact]
        public async Task SaveAsync_RejectsEmptyFile()
        {
            var file = BuildFile("empty.txt", "");

            await Assert.ThrowsAsync<ArgumentException>(() => _service.SaveAsync(file));
        }

        [Fact]
        public async Task SaveAsync_RejectsFileExceedingMaxSize()
        {
            // MaxFileSize в тесте = 1024 байт.
            var oversized = BuildFile("big.bin", new string('x', 2048));

            await Assert.ThrowsAsync<InvalidOperationException>(() => _service.SaveAsync(oversized));
        }

        [Fact]
        public async Task TryOpenRead_ReturnsTrueAndStream_ForExistingFile()
        {
            var saved = await _service.SaveAsync(BuildFile("a.txt", "payload"));

            var ok = _service.TryOpenRead(saved.FileId, out var stream, out var contentType);

            Assert.True(ok);
            Assert.NotNull(stream);
            Assert.Equal("text/plain", contentType);
            using (stream)
            {
                using var reader = new StreamReader(stream);
                Assert.Equal("payload", reader.ReadToEnd());
            }
        }

        [Fact]
        public void TryOpenRead_ReturnsFalse_ForMissingFile()
        {
            var ok = _service.TryOpenRead("nonexistent.bin", out var stream, out var contentType);

            Assert.False(ok);
            Assert.Null(stream);
            Assert.Null(contentType);
        }

        [Theory]
        [InlineData("../etc/passwd")]
        [InlineData("..\\windows\\system32\\file")]
        [InlineData("subdir/file.txt")]
        [InlineData("")]
        [InlineData(null)]
        public void TryOpenRead_BlocksPathTraversal(string fileId)
        {
            var ok = _service.TryOpenRead(fileId, out var stream, out _);

            Assert.False(ok);
            Assert.Null(stream);
        }

        [Theory]
        [InlineData("file.jpg",  "image/jpeg")]
        [InlineData("file.jpeg", "image/jpeg")]
        [InlineData("file.png",  "image/png")]
        [InlineData("file.gif",  "image/gif")]
        [InlineData("file.pdf",  "application/pdf")]
        [InlineData("file.txt",  "text/plain")]
        [InlineData("file.json", "application/json")]
        [InlineData("file.bin",  "application/octet-stream")]
        public async Task TryOpenRead_ReturnsCorrectContentType_ByExtension(string name, string expectedType)
        {
            var saved = await _service.SaveAsync(BuildFile(name, "x"));

            _service.TryOpenRead(saved.FileId, out var stream, out var contentType);
            stream?.Dispose();

            Assert.Equal(expectedType, contentType);
        }

        [Fact]
        public async Task List_ReturnsAllSavedFiles_NewestFirst()
        {
            var first  = await _service.SaveAsync(BuildFile("first.txt",  "1"));
            await Task.Delay(15); // гарантируем разное время создания
            var second = await _service.SaveAsync(BuildFile("second.txt", "2"));

            var list = _service.List();

            Assert.Equal(2, list.Count);
            Assert.Equal(second.FileId, list.First().FileId);
            Assert.Equal(first.FileId,  list.Last().FileId);
        }

        [Fact]
        public void List_ReturnsEmpty_WhenStorageIsEmpty()
        {
            Assert.Empty(_service.List());
        }

        [Fact]
        public async Task Delete_RemovesExistingFile_AndReturnsTrue()
        {
            var saved = await _service.SaveAsync(BuildFile("del.txt", "x"));

            var deleted = _service.Delete(saved.FileId);

            Assert.True(deleted);
            Assert.False(File.Exists(Path.Combine(_tempDir, saved.FileId)));
            Assert.Empty(_service.List());
        }

        [Fact]
        public void Delete_ReturnsFalse_ForMissingFile()
        {
            Assert.False(_service.Delete("does-not-exist.txt"));
        }

        [Theory]
        [InlineData("../escape.txt")]
        [InlineData("a/b.txt")]
        [InlineData("")]
        public void Delete_BlocksPathTraversal(string fileId)
        {
            Assert.False(_service.Delete(fileId));
        }

        private static IFormFile BuildFile(string fileName, string content)
        {
            var bytes  = Encoding.UTF8.GetBytes(content);
            var stream = new MemoryStream(bytes);
            return new FormFile(stream, 0, bytes.Length, "file", fileName)
            {
                Headers     = new Microsoft.AspNetCore.Http.HeaderDictionary(),
                ContentType = "application/octet-stream"
            };
        }
    }
}
