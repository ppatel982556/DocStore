namespace Repositories.Models.ViewModels.Workspace
{
    public class FolderVM
    {
        public long FolderId { get; set; }

        public string FolderName { get; set; } = string.Empty;

        public string? Description { get; set; }

        public bool IsFolder { get; set; }

        public DateTime CreatedAt { get; set; }
    }
}