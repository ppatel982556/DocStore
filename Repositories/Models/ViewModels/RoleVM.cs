using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Repositories.Models.ViewModels
{
    public class RoleVM
    {
        public int RoleId { get; set; }

    public string RoleName { get; set; }
    public string? Description { get; set; }

    }
}