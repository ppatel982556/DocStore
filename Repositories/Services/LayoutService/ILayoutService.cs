using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Repositories.Models.ViewModels;

namespace Repositories.Services.LayoutService
{
    public interface ILayoutService
    {
        Task<LayoutVM> GetLayoutAsync(int userId);
    }
}