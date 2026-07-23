using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Repositories.Services.StorageService
{
    public class StorageUploadResult
    {
        public bool Success { get; set; }

        public string ObjectKey { get; set; } = string.Empty;

        public string Message { get; set; } = string.Empty;
    }
}