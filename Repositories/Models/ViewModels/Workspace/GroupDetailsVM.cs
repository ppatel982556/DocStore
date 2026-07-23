using Repositories.Models.ViewModels;

public class GroupDetailsVM
{
    public long GroupId { get; set; }

    public string GroupName { get; set; } = "";

    public string? Description { get; set; }

    public string CreatedBy { get; set; } = "";

    public DateTime CreatedAt { get; set; }

    public string UpdatedBy { get; set; } = "";

    public DateTime? UpdatedAt { get; set; }

    public int FolderCount { get; set; }

    public int FileCount { get; set; }

    public long TotalSize { get; set; }

    public string TotalSizeDisplay { get; set; } = "";

    public List<RoleVM> Roles { get; set; } = new();

    public List<long> SelectedRoleIds { get; set; } = new();

}
