using Repositories.Models;

namespace Repositories.Interfaces
{
    public interface IRolePermissionInterface
    {
        Task<List<RolePermission>> GetPermissionsByRoleAsync(int roleId);

        Task<RolePermission?> GetPermissionAsync(int roleId, int pageId);
    }
}