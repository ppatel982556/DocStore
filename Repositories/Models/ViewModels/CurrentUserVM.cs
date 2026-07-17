using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Repositories.Models.ViewModels
{
    public class CurrentUserVM
    {
        public int UserId { get; set; }

    public string FirstName { get; set; }

    public string LastName { get; set; }

    public string Email { get; set; }

    public string? ProfileImageUrl { get; set; }

    public string Initial =>
        FirstName.Substring(0,1).ToUpper();

    public int ActiveRoleId { get; set; }

    public string ActiveRoleName { get; set; }
    }
}