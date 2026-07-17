using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CloudinaryDotNet;
using CloudinaryDotNet.Actions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Repositories.Models;

namespace Repositories.Services.CloudinaryService
{
    public class CloudinaryService : ICloudinaryService
    {
        private readonly Cloudinary _cloudinary;

        public CloudinaryService(IConfiguration configuration)
        {
            Account account = new Account(
                configuration["Cloudinary:CloudName"],
                configuration["Cloudinary:ApiKey"],
                configuration["Cloudinary:ApiSecret"]);

            _cloudinary = new Cloudinary(account);

            _cloudinary.Api.Secure = true;
        }
        public async Task<CloudinaryUploadResult?> UploadProfilePictureAsync(IFormFile file)
        {
            if (file == null || file.Length == 0)
                return null;

            await using var stream = file.OpenReadStream();

            ImageUploadParams uploadParams = new()
{
    File = new FileDescription(file.FileName, stream),

    Folder = "DocStore/Users",

    PublicId = Guid.NewGuid().ToString(),

    UseFilename = false,

    UniqueFilename = false,

    Overwrite = false,

    Transformation = new Transformation()
        .Width(500)
        .Height(500)
        .Crop("fill")
        .Gravity("face")
        .FetchFormat("auto")
        .Quality("auto")
};

            ImageUploadResult uploadResult =
                await _cloudinary.UploadAsync(uploadParams);

            if (uploadResult.Error != null)
            {
                throw new Exception(uploadResult.Error.Message);
            }

            return new CloudinaryUploadResult
            {
                PublicId = uploadResult.PublicId,
                Url = uploadResult.SecureUrl.ToString()
            };
        }

        public string GetProfilePictureUrl(string? publicId)
        {
            if (string.IsNullOrWhiteSpace(publicId))
            {
                return null;
            }

            return _cloudinary.Api.UrlImgUp
                .Secure(true)
                .Transform(new Transformation()
                    .Width(250)
                    .Height(250)
                    .Crop("fill")
                    .Gravity("face")
                    .FetchFormat("auto")
                    .Quality("auto"))
                .BuildUrl(publicId);
        }

    }
}