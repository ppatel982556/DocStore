using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Repositories.Models.ViewModels.Auth;

namespace Repositories.Interfaces
{
    public interface IPasswordResetInterface
    {
        Task SaveToken(int userId, string token, DateTime expiry);

        Task<ResetPasswordVm?> GetToken(string token);

        Task MarkUsed(int id);

        Task UpdatePassword(int userId, string passwordHash);

    }
}