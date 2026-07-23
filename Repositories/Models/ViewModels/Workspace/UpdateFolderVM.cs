using Repositories.Models.ViewModels.Workspace;

public class UpdateFolderVM
{
    public long FolderId { get; set; }

    public long GroupId { get; set; }

    public long? ParentFolderId { get; set; }

    public string FolderName { get; set; } = string.Empty;

    public string? Description { get; set; }

    public List<long> SelectedRoleIds { get; set; } = new();

    public long UpdatedBy { get; set; }

    public PermissionPropagationVM PermissionOptions { get; set; }
        = new();
}