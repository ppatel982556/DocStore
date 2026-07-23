namespace Repositories.Models.ViewModels.Workspace
{
    public class WorkspaceVM
    {
        public List<GroupVM> Groups { get; set; } = new();

        public long? SelectedGroupId { get; set; }

        public string? SelectedGroupName { get; set; }

        public List<FolderTreeVM> FolderTree { get; set; } = new();

        public long? SelectedFolderId { get; set; }

        public string? CurrentFolderName { get; set; }
        public List<RoleVM> Roles { get; set; } = new();
    }
}