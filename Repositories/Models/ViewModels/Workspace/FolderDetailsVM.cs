using Repositories.Models.ViewModels;

public class FolderDetailsVM
{
    public long FolderId { get; set; }

    public string FolderName { get; set; } = string.Empty;

    public string? Description { get; set; }

    public string Path { get; set; } = string.Empty;

    public int Level { get; set; }

    public string GroupName { get; set; } = string.Empty;

    public string ParentFolder { get; set; } = string.Empty;

    public string CreatedBy { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }

    public string UpdatedBy { get; set; } = string.Empty;

    public DateTime? UpdatedAt { get; set; }

    public int ChildFolderCount { get; set; }

    public int FileCount { get; set; }

    public List<RoleVM> Roles { get; set; } = new();

    public List<long> SelectedRoleIds { get; set; } = new();
    public long TotalSize { get; set; }

    public string TotalSizeDisplay { get; set; } = "";
}