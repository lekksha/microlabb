using Files.API.Models;
using Files.FunctionalTests.Base;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Xunit;

namespace Files.FunctionalTests
{
    public class FilesScenarios : FilesScenariosBase
    {
        private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        // ── upload ────────────────────────────────────────────────────────────

        [Fact]
        public async Task Post_upload_returns_ok_with_fileId()
        {
            using var server = CreateServer();
            using var client = server.CreateClient();

            var response = await UploadAsync(client, "hello.txt", "Hello, world!");

            response.EnsureSuccessStatusCode();
            var result = await DeserializeAsync<UploadResult>(response);
            Assert.NotNull(result.FileId);
            Assert.EndsWith(".txt", result.FileId);
            Assert.Equal("hello.txt", result.OriginalName);
            Assert.Equal(13, result.Size);
        }

        [Fact]
        public async Task Post_upload_without_file_returns_bad_request()
        {
            using var server = CreateServer();
            using var client = server.CreateClient();

            using var emptyContent = new MultipartFormDataContent();
            var response = await client.PostAsync(Post.Upload, emptyContent);

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }

        [Fact]
        public async Task Post_upload_two_files_returns_distinct_ids()
        {
            using var server = CreateServer();
            using var client = server.CreateClient();

            var first  = await DeserializeAsync<UploadResult>(await UploadAsync(client, "a.txt", "A"));
            var second = await DeserializeAsync<UploadResult>(await UploadAsync(client, "b.txt", "B"));

            Assert.NotNull(first.FileId);
            Assert.NotNull(second.FileId);
            Assert.NotEqual(first.FileId, second.FileId);
        }

        // ── download ──────────────────────────────────────────────────────────

        [Fact]
        public async Task Get_download_returns_uploaded_content()
        {
            using var server = CreateServer();
            using var client = server.CreateClient();

            var uploaded = await DeserializeAsync<UploadResult>(
                await UploadAsync(client, "data.txt", "round-trip"));

            var download = await client.GetAsync(Get.Download(uploaded.FileId));

            download.EnsureSuccessStatusCode();
            var body = await download.Content.ReadAsStringAsync();
            Assert.Equal("round-trip", body);
            Assert.Equal("text/plain", download.Content.Headers.ContentType?.MediaType);
        }

        [Fact]
        public async Task Get_download_unknown_file_returns_404()
        {
            using var server = CreateServer();
            using var client = server.CreateClient();

            var response = await client.GetAsync(Get.Download("missing.bin"));

            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }

        [Theory]
        [InlineData("photo.jpg",  "image/jpeg")]
        [InlineData("photo.jpeg", "image/jpeg")]
        [InlineData("img.png",    "image/png")]
        [InlineData("doc.pdf",    "application/pdf")]
        [InlineData("data.json",  "application/json")]
        public async Task Get_download_returns_correct_content_type(string name, string expectedType)
        {
            using var server = CreateServer();
            using var client = server.CreateClient();

            var uploaded = await DeserializeAsync<UploadResult>(
                await UploadAsync(client, name, "x"));

            var download = await client.GetAsync(Get.Download(uploaded.FileId));

            download.EnsureSuccessStatusCode();
            Assert.Equal(expectedType, download.Content.Headers.ContentType?.MediaType);
        }

        // ── list ──────────────────────────────────────────────────────────────

        [Fact]
        public async Task Get_list_initially_empty()
        {
            using var server = CreateServer();
            using var client = server.CreateClient();

            var response = await client.GetAsync(Get.List);

            response.EnsureSuccessStatusCode();
            var list = await DeserializeAsync<List<FileMetadata>>(response);
            Assert.Empty(list);
        }

        [Fact]
        public async Task Get_list_returns_uploaded_files()
        {
            using var server = CreateServer();
            using var client = server.CreateClient();

            var uploaded = await DeserializeAsync<UploadResult>(
                await UploadAsync(client, "listed.txt", "content"));

            var response = await client.GetAsync(Get.List);
            response.EnsureSuccessStatusCode();
            var list = await DeserializeAsync<List<FileMetadata>>(response);

            Assert.Single(list);
            Assert.Equal(uploaded.FileId, list[0].FileId);
            Assert.Equal(7, list[0].Size);
        }

        // ── delete ────────────────────────────────────────────────────────────

        [Fact]
        public async Task Delete_existing_file_returns_no_content_and_removes_it()
        {
            using var server = CreateServer();
            using var client = server.CreateClient();

            var uploaded = await DeserializeAsync<UploadResult>(
                await UploadAsync(client, "to-delete.txt", "bye"));

            var delete = await client.DeleteAsync(Delete.Remove(uploaded.FileId));
            Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);

            var afterDownload = await client.GetAsync(Get.Download(uploaded.FileId));
            Assert.Equal(HttpStatusCode.NotFound, afterDownload.StatusCode);
        }

        [Fact]
        public async Task Delete_unknown_file_returns_404()
        {
            using var server = CreateServer();
            using var client = server.CreateClient();

            var response = await client.DeleteAsync(Delete.Remove("nothing.bin"));

            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }

        // ── path traversal protection ─────────────────────────────────────────

        [Theory]
        [InlineData("..%2F..%2Fetc%2Fpasswd")]
        [InlineData("subdir%2Ffile.txt")]
        public async Task Get_download_blocks_path_traversal(string maliciousId)
        {
            using var server = CreateServer();
            using var client = server.CreateClient();

            var response = await client.GetAsync($"api/files/{maliciousId}");

            Assert.True(
                response.StatusCode == HttpStatusCode.NotFound ||
                response.StatusCode == HttpStatusCode.BadRequest,
                $"Expected 404/400 for path-traversal id, got {(int)response.StatusCode}");
        }

        // ── full lifecycle ────────────────────────────────────────────────────

        [Fact]
        public async Task Lifecycle_upload_list_download_delete()
        {
            using var server = CreateServer();
            using var client = server.CreateClient();

            // 1. upload
            var uploaded = await DeserializeAsync<UploadResult>(
                await UploadAsync(client, "lifecycle.json", "{\"k\":1}"));

            // 2. list contains it
            var listed = await DeserializeAsync<List<FileMetadata>>(await client.GetAsync(Get.List));
            Assert.Contains(listed, f => f.FileId == uploaded.FileId);

            // 3. download yields original payload
            var body = await (await client.GetAsync(Get.Download(uploaded.FileId))).Content.ReadAsStringAsync();
            Assert.Equal("{\"k\":1}", body);

            // 4. delete removes it
            await client.DeleteAsync(Delete.Remove(uploaded.FileId));
            var listedAfter = await DeserializeAsync<List<FileMetadata>>(await client.GetAsync(Get.List));
            Assert.DoesNotContain(listedAfter, f => f.FileId == uploaded.FileId);
        }

        // ── helpers ───────────────────────────────────────────────────────────

        private static async Task<HttpResponseMessage> UploadAsync(HttpClient client, string fileName, string content)
        {
            using var multipart = new MultipartFormDataContent();
            var bytes = Encoding.UTF8.GetBytes(content);
            var fileContent = new ByteArrayContent(bytes);
            fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/octet-stream");
            multipart.Add(fileContent, "file", fileName);
            return await client.PostAsync(Post.Upload, multipart);
        }

        private static async Task<T> DeserializeAsync<T>(HttpResponseMessage response)
        {
            var content = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<T>(content, JsonOptions);
        }
    }
}
