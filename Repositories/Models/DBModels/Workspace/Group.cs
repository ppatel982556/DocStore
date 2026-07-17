namespace Repositories.Models.DBModels.Workspace
{
    public class Group
    {
        public long GroupId { get; set; }

        public string GroupName { get; set; } = string.Empty;

        public string? Description { get; set; }

        public long CreatedBy { get; set; }

        public long? UpdatedBy { get; set; }

        public long? DeletedBy { get; set; }

        public DateTime CreatedAt { get; set; }

        public DateTime? UpdatedAt { get; set; }

        public DateTime? DeletedAt { get; set; }

        public bool IsDeleted { get; set; }
    }
}