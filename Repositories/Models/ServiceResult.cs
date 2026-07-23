using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Repositories.Models
{
    public class ServiceResult
    {
        public bool Success { get; set; }

        public string Message { get; set; } = string.Empty;
        public long? Id { get; set; }

    }
}