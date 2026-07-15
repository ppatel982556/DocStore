using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Repositories.Constants.ResetPassword
{
    public class PasswordResetColumns
    {
        public const string ID = "c_id";
        public const string USERID = "c_userid";
        public const string TOKEN = "c_token";
        public const string EXPIRY = "c_expiry";
        public const string ISUSED = "c_isused";
    }
}