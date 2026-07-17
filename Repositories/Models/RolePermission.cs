namespace Repositories.Models
{
    public class RolePermission
    {
        public int RolePermissionId { get; set; }

        public int RoleId { get; set; }

        public int PageId { get; set; }

        public bool CanView { get; set; }

        public bool CanOpen { get; set; }

        public bool CanCreate { get; set; }

        public bool CanEdit { get; set; }

        public bool CanDelete { get; set; }

        public bool CanRestore { get; set; }

        public bool CanUpload { get; set; }

        public bool CanDownload { get; set; }

        public bool CanMove { get; set; }

        public bool CanCopy { get; set; }

        public bool CanRename { get; set; }

        public bool CanExport { get; set; }
    }
}