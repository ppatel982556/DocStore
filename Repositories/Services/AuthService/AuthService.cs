using BCrypt.Net;
using Microsoft.Extensions.Logging;
using Repositories.Constants.Enums;
using Repositories.Interfaces;
using Repositories.Models;
using Repositories.Models.ViewModels;
using Repositories.Models.ViewModels.Auth;
using Repositories.Services.CloudinaryService;

namespace Services.AuthService
{
    public class AuthService : IAuthService
    {
        private readonly IAuthInterface _authRepository;
        private readonly ILogger<AuthService> _logger;


        private readonly ICloudinaryService _cloudinaryService;

        public AuthService(
            IAuthInterface authRepository,
            ICloudinaryService cloudinaryService,
            ILogger<AuthService> logger)
        {
            _authRepository = authRepository;
            _cloudinaryService = cloudinaryService;
            _logger = logger;
        }

       public async Task<LoginResult> LoginAsync(vmLogin model)
{
    _logger.LogInformation("Login attempt for {Email}", model.Email);

    var user = await _authRepository.GetUserByEmailAsync(model.Email);

    if (user == null)
    {
        return new LoginResult
        {
            Status = LoginStatus.UserNotFound
        };
    }

    if (!user.IsActive)
    {
        return new LoginResult
        {
            Status = LoginStatus.InactiveUser
        };
    }

    if (!BCrypt.Net.BCrypt.Verify(model.Password, user.PasswordHash))
    {
        return new LoginResult
        {
            Status = LoginStatus.InvalidPassword
        };
    }

    var roles = await _authRepository.GetUserRolesAsync(user.UserId);

    if (roles.Count == 0)
    {
        return new LoginResult
        {
            Status = LoginStatus.UserNotFound
        };
    }

    user.Roles = roles;

    RoleVM activeRole;

    if (user.LastActiveRoleId.HasValue)
    {
        activeRole = roles.FirstOrDefault(r => r.RoleId == user.LastActiveRoleId.Value)
                     ?? roles.First();
    }
    else
    {
        activeRole = roles.First();

        await _authRepository.UpdateLastActiveRoleAsync(
            user.UserId,
            activeRole.RoleId);

        user.LastActiveRoleId = activeRole.RoleId;
    }

    return new LoginResult
    {
        Status = LoginStatus.Success,
        User = user,
        ActiveRole = activeRole
    };
}
        public async Task<ServiceResult> Register(RegisterVM model)
        {
            var existingUser = await _authRepository.GetUserByEmailAsync(model.Email);

            if (existingUser != null)
            {
                return new ServiceResult
                {
                    Success = false,
                    Message = "Email already exists."
                };
            }

            string? profilePictureId = null;

            if (model.ProfilePicture != null)
            {
                var uploadResult =
                    await _cloudinaryService.UploadProfilePictureAsync(model.ProfilePicture);

                if (uploadResult == null)
                {
                    return new ServiceResult
                    {
                        Success = false,
                        Message = "Unable to upload profile picture."
                    };
                }

                profilePictureId = uploadResult.PublicId;
            }

            return await _authRepository.Register(model, profilePictureId);
        }
    }
}
