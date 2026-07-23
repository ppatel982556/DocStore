namespace Repositories.Models.ViewModels.Workspace
{
    public class FileVM
    {
        public long FileId { get; set; }

        public string FileName { get; set; } = string.Empty;

        public string Extension { get; set; } = string.Empty;

        public string FileCategory { get; set; } = string.Empty;

        public long FileSize { get; set; }

        public DateTime CreatedAt { get; set; }
    }
}