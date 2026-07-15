using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Repositories.Constants.Enums;

namespace Repositories.Models.ViewModels.Auth
{
    public class LoginResult
    {
        public LoginStatus Status { get; set; }

        public User? User { get; set; }
    }
}