using System.ComponentModel.DataAnnotations;

public class CreateFolderVM
{
    public long GroupId { get; set; }

    public long? ParentFolderId { get; set; }

    [Required]
    [StringLength(100)]
    public string FolderName { get; set; } = string.Empty;

    [StringLength(500)]
    public string? Description { get; set; }

    public List<long> SelectedRoleIds { get; set; } = new();

    public long CreatedBy { get; set; }
    public string? ParentFolderName { get; set; }
}