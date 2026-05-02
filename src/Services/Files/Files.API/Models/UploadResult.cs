namespace Files.API.Models
{
    public class UploadResult
    {
        public string FileId       { get; set; }
        public string OriginalName { get; set; }
        public long   Size         { get; set; }
    }
}
