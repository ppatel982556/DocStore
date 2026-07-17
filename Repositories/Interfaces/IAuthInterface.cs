using Repositories.Models;
using Repositories.Models.ViewModels;
using Repositories.Models.ViewModels.Auth;

namespace Repositories.Interfaces
{
    public interface IAuthInterface
    {
        Task<User?> GetUserByEmailAsync(string email);
Task<List<RoleVM>> GetUserRolesAsync(int userId);

Task UpdateLastActiveRoleAsync(int userId,int roleId);
        Task<ServiceResult> Register(RegisterVM model, string? profilePictureId);
    }
}