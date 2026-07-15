using Repositories.Models;
using Repositories.Models.ViewModels.Auth;

namespace Repositories.Interfaces
{
    public interface IAuthInterface
    {
        Task<User?> GetUserByEmailAsync(string email);
        Task<List<string>> GetUserRolesAsync(int userId);
        Task<ServiceResult> Register(RegisterVM model, string? profilePictureId);
    }
}