using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Repositories.Models;

namespace Repositories.Interfaces
{
    public interface IUserInterface
    {
        Task<User?> GetUserByIdAsync(int userId);

        Task<User?> GetUserByEmailAsync(string email);

        Task<ServiceResult> UpdateProfileAsync(User user);

        Task<ServiceResult> ChangeProfilePictureAsync(int userId, string? profilePictureId);
    }
}