using BCrypt.Net;
using Microsoft.Extensions.Logging;
using Repositories.Constants.Enums;
using Repositories.Interfaces;
using Repositories.Models;
using Repositories.Models.ViewModels.Auth;

namespace Services.AuthService
{
    public class AuthService : IAuthService
    {
        private readonly IAuthInterface _authRepository;
        private readonly ILogger<AuthService> _logger;

        public AuthService(
            IAuthInterface authRepository,
            ILogger<AuthService> logger)
        {
            _authRepository = authRepository;
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

            user.Roles = await _authRepository.GetUserRolesAsync(user.UserId);

            return new LoginResult
            {
                Status = LoginStatus.Success,
                User = user
            };
        }
    }
}