using Files.API.Configuration;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;

namespace Files.FunctionalTests.Base
{
    public class FilesScenariosBase : IDisposable
    {
        private const string ApiUrlBase = "api/files";
        private readonly string _tempDir;

        protected FilesScenariosBase()
        {
            _tempDir = Path.Combine(Path.GetTempPath(), "files-functional-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_tempDir);
        }

        public TestServer CreateServer()
        {
            var path = Assembly.GetAssembly(typeof(FilesScenariosBase))?.Location;
            var hostBuilder = new WebHostBuilder()
                .UseContentRoot(Path.GetDirectoryName(path))
                .ConfigureAppConfiguration(cb =>
                {
                    cb.AddJsonFile("appsettings.json", optional: true)
                      .AddInMemoryCollection(new Dictionary<string, string>
                      {
                          ["FileStorage:Path"]        = _tempDir,
                          ["FileStorage:MaxFileSize"] = "1048576" // 1 MB
                      })
                      .AddEnvironmentVariables();
                })
                .ConfigureServices(services =>
                {
                    // Перекрываем привязку Path/MaxFileSize, чтобы каждый тест писал
                    // в свой временный каталог, не пересекаясь с другими.
                    services.PostConfigure<FileStorageOptions>(o =>
                    {
                        o.Path        = _tempDir;
                        o.MaxFileSize = 1024 * 1024;
                    });
                })
                .UseUrls("http://*:7005")
                .UseStartup<API.Startup>();
            return new TestServer(hostBuilder);
        }

        public void Dispose()
        {
            if (Directory.Exists(_tempDir))
                Directory.Delete(_tempDir, recursive: true);
        }

        public static class Get
        {
            public static string List                       = $"{ApiUrlBase}";
            public static string Download(string fileId)    => $"{ApiUrlBase}/{fileId}";
        }

        public static class Post
        {
            public static string Upload                     = $"{ApiUrlBase}/upload";
        }

        public static class Delete
        {
            public static string Remove(string fileId)      => $"{ApiUrlBase}/{fileId}";
        }
    }
}
