namespace Repositories.Models.ViewModels.Workspace
{
    public class GroupVM
    {
        public long GroupId { get; set; }

        public string GroupName { get; set; } = string.Empty;

        public string? Description { get; set; }
    }
}