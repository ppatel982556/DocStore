using Repositories.Models;

namespace Repositories.Interfaces
{
    public interface IAuthInterface
    {
        Task<User?> GetUserByEmailAsync(string email);
        Task<List<string>> GetUserRolesAsync(int userId);
    }
}