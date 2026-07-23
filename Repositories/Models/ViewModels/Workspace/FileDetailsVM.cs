using Repositories.Models.ViewModels;

namespace Repositories.Models.ViewModels.Workspace
{
    public class FileDetailsVM
    {
        public long FileId { get; set; }

        public long GroupId { get; set; }

        public long FolderId { get; set; }

        public string FileName { get; set; } = string.Empty;

        public string OriginalFileName { get; set; } = string.Empty;

        public string Description { get; set; } = string.Empty;

        public string Extension { get; set; } = string.Empty;

        public string ContentType { get; set; } = string.Empty;

        public string FileCategory { get; set; } = string.Empty;

        public long FileSize { get; set; }

        public string FileSizeDisplay { get; set; } = string.Empty;

        public string GroupName { get; set; } = string.Empty;

        public string FolderName { get; set; } = string.Empty;

        public string Path { get; set; } = string.Empty;

        public string StorageProvider { get; set; } = "Supabase";

        public string CreatedBy { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; }

        public string? UpdatedBy { get; set; }

        public DateTime? UpdatedAt { get; set; }

        public List<long> SelectedRoleIds { get; set; } = new();

        public List<RoleVM> Roles { get; set; } = new();
    }
}