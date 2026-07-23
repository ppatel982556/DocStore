using System.ComponentModel.DataAnnotations;

namespace Repositories.Models.ViewModels.Workspace
{
    public class UpdateGroupVM
    {
        public long GroupId { get; set; }

        [Required]
        public string GroupName { get; set; } = string.Empty;

        public string? Description { get; set; }

        public long UpdatedBy { get; set; }

        public List<long> SelectedRoleIds { get; set; } = new();
    }
}