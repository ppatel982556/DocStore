namespace Repositories.Models.ViewModels.Workspace
{
    public class MoveItemVM
    {
        public string ItemType { get; set; } = string.Empty;

        public long ItemId { get; set; }

        public long DestinationGroupId { get; set; }

        public long? DestinationFolderId { get; set; }

        public string PermissionOption { get; set; } = "keep";

        public long UserId { get; set; }

        public int ActiveRoleId { get; set; }
    }
}
