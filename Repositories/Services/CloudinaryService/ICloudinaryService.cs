using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Repositories.Models;

namespace Repositories.Services.CloudinaryService
{
    public interface ICloudinaryService
    {
        string GetProfilePictureUrl(string? publicId);
        Task<CloudinaryUploadResult?> UploadProfilePictureAsync(IFormFile file);
    }
}