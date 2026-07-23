using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;

namespace Repositories.Services.StorageService
{
    public interface IStorageService
    {
        Task<StorageUploadResult> UploadAsync(
            IFormFile file,
            string objectKey);

        Task DeleteAsync(string objectKey);

        Task<Stream> DownloadAsync(string objectKey);


        Task<string> GetDownloadUrlAsync(
            string objectKey,
            int expiryMinutes = 10);
    }
}