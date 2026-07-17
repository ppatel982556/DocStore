using Repositories.Interfaces;
using Repositories.Models;

namespace Repositories.Services.UserService
{
    public class UserService : IUserService
    {
        private readonly IUserInterface _userRepository;

        public UserService(IUserInterface userRepository)
        {
            _userRepository = userRepository;
        }

        public Task<User?> GetUserByIdAsync(int userId)
        {
            return _userRepository.GetUserByIdAsync(userId);
        }

        public Task<User?> GetUserByEmailAsync(string email)
        {
            return _userRepository.GetUserByEmailAsync(email);
        }

        public Task<ServiceResult> UpdateProfileAsync(User user)
        {
            return _userRepository.UpdateProfileAsync(user);
        }

        public Task<ServiceResult> ChangeProfilePictureAsync(int userId, string? profilePictureId)
        {
            return _userRepository.ChangeProfilePictureAsync(userId, profilePictureId);
        }
    }
}