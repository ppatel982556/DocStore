using Repositories.Models;
using Repositories.Models.ViewModels.Auth;

namespace Services.AuthService
{
    public interface IAuthService
    {
        Task<LoginResult> LoginAsync(vmLogin model);
    }
}