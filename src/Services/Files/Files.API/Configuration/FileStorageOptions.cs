namespace Files.API.Configuration
{
    public class FileStorageOptions
    {
        public const string SectionName = "FileStorage";

        public string Path { get; set; } = "/app/uploads";

        public long MaxFileSize { get; set; } = 50 * 1024 * 1024; // 50 MB
    }
}
