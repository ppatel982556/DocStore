namespace Repositories.Models.ViewModels.Workspace
{
    public class CreateGroupVM
    {
        public string GroupName { get; set; } = string.Empty;

        public string? Description { get; set; }

        public List<long> SelectedRoleIds { get; set; } = new();

        public long CreatedBy { get; set; }
    }
}