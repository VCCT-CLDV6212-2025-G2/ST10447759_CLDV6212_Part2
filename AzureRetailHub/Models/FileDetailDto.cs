namespace AzureRetailHub.Models
{
    public class FileDetailDto
    {
        public string Name { get; set; }
        public long Size { get; set; }
        public DateTimeOffset? UploadedOn { get; set; }
    }
}