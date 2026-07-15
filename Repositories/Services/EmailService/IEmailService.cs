using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Repositories.Services.EmailService
{
    public interface IEmailService
    {
        Task SendResetPasswordEmail(string email, string resetLink);
    }
}