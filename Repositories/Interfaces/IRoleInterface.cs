using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Repositories.Models;
using Repositories.Models.ViewModels;

namespace Repositories.Interfaces
{
    public interface IRoleInterface
    {
                Task<List<RoleVM>> GetAllRolesAsync();

        Task<RoleVM?> GetRoleByIdAsync(int roleId);

        Task<List<RoleVM>> GetUserRolesAsync(int userId);

        Task<ServiceResult> SwitchActiveRoleAsync(int userId, int roleId);
    }
}