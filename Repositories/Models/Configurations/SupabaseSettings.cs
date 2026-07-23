namespace Repositories.Models.Configurations
{
    public class SupabaseSettings
    {
        public string ProjectUrl { get; set; } = string.Empty;

        public string ServiceRoleKey { get; set; } = string.Empty;

        public string BucketName { get; set; } = string.Empty;
    }
}