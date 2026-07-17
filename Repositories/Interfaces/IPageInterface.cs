using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Repositories.Models.ViewModels;

namespace Repositories.Interfaces
{
    public interface IPageInterface
    {
        Task<List<PageVM>> GetAllPagesAsync();

        Task<PageVM?> GetPageByIdAsync(int pageId);

        Task<List<PageVM>> GetMenuPagesAsync();

        Task<List<PageVM>> GetPagesByRoleAsync(int roleId);
        
    }
}