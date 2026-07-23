using Microsoft.AspNetCore.Http;
using System.ComponentModel.DataAnnotations;

namespace Repositories.Models.ViewModels.Workspace
{
    public class UploadFileVM
    {
        [Range(1, long.MaxValue, ErrorMessage = "Please select a group.")]
        public long GroupId { get; set; }

        [Range(1, long.MaxValue, ErrorMessage = "Please select a folder.")]
        public long FolderId { get; set; }

        [Required(ErrorMessage = "Please select a file.")]
        public IFormFile File { get; set; } = default!;

        public string? Description { get; set; }

        public List<int> SelectedRoleIds { get; set; } = new();

        public long CreatedBy { get; set; }
    }
}
