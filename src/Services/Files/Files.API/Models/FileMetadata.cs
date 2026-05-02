using System;

namespace Files.API.Models
{
    public class FileMetadata
    {
        public string FileId    { get; set; }
        public long   Size      { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
