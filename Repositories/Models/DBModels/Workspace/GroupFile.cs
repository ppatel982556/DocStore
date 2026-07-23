namespace Repositories.Models.DBModels.Workspace
{
    public class GroupFile
    {
        public long FileId { get; set; }

        public long GroupId { get; set; }

        public long FolderId { get; set; }

        public string FileName { get; set; } = string.Empty;

        public string OriginalFileName { get; set; } = string.Empty;

        public string Extension { get; set; } = string.Empty;

        public string ContentType { get; set; } = string.Empty;

        public string FileCategory { get; set; } = string.Empty;

        public long FileSize { get; set; }

        public string StorageProvider { get; set; } = string.Empty;

        public string ObjectKey { get; set; } = string.Empty;

        public string? Description { get; set; }

        public long CreatedBy { get; set; }

        public long? UpdatedBy { get; set; }

        public long? DeletedBy { get; set; }

        public DateTime CreatedAt { get; set; }

        public DateTime? UpdatedAt { get; set; }

        public DateTime? DeletedAt { get; set; }

        public bool IsDeleted { get; set; }
    }
}