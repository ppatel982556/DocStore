namespace Repositories.Models.ViewModels.RecycleBin
{
    public class RecycleBinItemVM
    {
        public long Id { get; set; }

        public string Name { get; set; } = string.Empty;

        public string Type { get; set; } = string.Empty;

        public string GroupName { get; set; } = string.Empty;

        public string Path { get; set; } = string.Empty;

        public string DeletedBy { get; set; } = string.Empty;

        public DateTime? DeletedAt { get; set; }

        public long? Size { get; set; }

        public string SizeDisplay { get; set; } = string.Empty;

        public string Icon { get; set; } = string.Empty;

        public string Description { get; set; } = string.Empty;

        public long? GroupId { get; set; }

        public long? ParentFolderId { get; set; }

        public bool IsFolder { get; set; }
    }
}
