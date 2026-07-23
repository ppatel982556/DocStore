namespace Repositories.Models.ViewModels.Workspace
{
    public class MoveDestinationNodeVM
    {
        public string Id { get; set; } = string.Empty;

        public string Text { get; set; } = string.Empty;

        public string Type { get; set; } = string.Empty;

        public long GroupId { get; set; }

        public long? FolderId { get; set; }

        public bool Expanded { get; set; } = true;

        public bool Enabled { get; set; } = true;

        public List<MoveDestinationNodeVM> Items { get; set; } = new();
    }
}
