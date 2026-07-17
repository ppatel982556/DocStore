using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Repositories.Models.ViewModels
{
    public class LayoutVM
    {
        public CurrentUserVM User { get; set; } = new();

    public List<RoleVM> Roles { get; set; } = new();

    public List<PageVM> Pages { get; set; } = new();

    public int NotificationCount { get; set; }
    }
}