namespace Repositories.Constants.Workspace.Move
{
    public static class MoveConstants
    {
        public const string ItemTypeFolder = "folder";

        public const string ItemTypeFile = "file";

        public const string PermissionKeep = "keep";

        public const string PermissionInherit = "inherit";

        public const string PermissionRemoveUnavailable = "removeUnavailable";

        public const string PermissionMerge = "merge";

        public const string ActionMoved = "Moved";

        public const string DuplicateFolderMessage = "A folder with the same name already exists in the destination.";

        public const string DuplicateFileMessage = "A file with the same name already exists in the destination.";

        public const string FileRootMoveMessage = "Files must be moved into a folder.";
    }
}
