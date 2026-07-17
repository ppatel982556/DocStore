namespace Repositories.Models.ViewModels.Workspace
{
    public class FolderContentVM
    {
        public long Id { get; set; }

        public string Name { get; set; } = string.Empty;

        public bool IsFolder { get; set; }

        public string Type { get; set; } = string.Empty;

        public string Path { get; set; } = string.Empty;

        public long? Size { get; set; }

        public DateTime CreatedAt { get; set; }
    }
}