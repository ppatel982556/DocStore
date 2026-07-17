namespace Repositories.Models.ViewModels.Workspace
{
    public class FolderTreeVM
    {
        public long Id { get; set; }

        public long? ParentId { get; set; }

        public string Text { get; set; } = string.Empty;

        public bool Expanded { get; set; }

        public bool HasChildren { get; set; }
    }
}