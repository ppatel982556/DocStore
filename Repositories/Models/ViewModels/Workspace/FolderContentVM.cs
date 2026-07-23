public class FolderContentVM
{
    public long Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public bool IsFolder { get; set; }

    public string Type { get; set; } = string.Empty;

    public string Path { get; set; } = string.Empty;

    public int Level { get; set; }

    public long? Size { get; set; }

    public string SizeDisplay { get; set; } = "-";

    public string CreatedBy { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }

    public string? Extension { get; set; }

    public string Icon { get; set; } = string.Empty;
}   