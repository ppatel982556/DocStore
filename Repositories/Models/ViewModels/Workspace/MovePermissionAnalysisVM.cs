using Repositories.Models.ViewModels;

namespace Repositories.Models.ViewModels.Workspace
{
    public class MovePermissionAnalysisVM
    {
        public bool Success { get; set; }

        public string Message { get; set; } = string.Empty;

        public bool IsCrossGroup { get; set; }

        public string ItemName { get; set; } = string.Empty;

        public string SourcePath { get; set; } = string.Empty;

        public string DestinationPath { get; set; } = string.Empty;

        public int FoldersAffected { get; set; }

        public int FilesAffected { get; set; }

        public List<RoleVM> CurrentRoles { get; set; } = new();

        public List<RoleVM> DestinationRoles { get; set; } = new();

        public List<RoleVM> MissingRoles { get; set; } = new();

        public List<RoleVM> AfterMoveRoles { get; set; } = new();
    }
}
