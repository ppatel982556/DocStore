namespace Repositories.Models.DBModels.Workspace
{
    public class GroupFolder
{
    public long FolderId { get; set; }

    public long GroupId { get; set; }

    public long? ParentFolderId { get; set; }

    public string FolderName { get; set; } = string.Empty;

    public string? Description { get; set; }

    public string FullPath { get; set; } = "/";

    public int Level { get; set; }

    public bool HasChildren { get; set; }

    public int DisplayOrder { get; set; }

    public long CreatedBy { get; set; }

    public long? UpdatedBy { get; set; }

    public long? DeletedBy { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public DateTime? DeletedAt { get; set; }

    public bool IsDeleted { get; set; }
}
}