using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Repositories.Models.ViewModels.Workspace
{
    public class UpdateFileVM
{
    public long FileId { get; set; }

    // Name without extension
    public string FileName { get; set; } = string.Empty;

    // Read-only in UI
    public string Extension { get; set; } = string.Empty;

    public string? Description { get; set; }

    public List<long> SelectedRoleIds { get; set; } = new();

    public long UpdatedBy { get; set; }
}
}