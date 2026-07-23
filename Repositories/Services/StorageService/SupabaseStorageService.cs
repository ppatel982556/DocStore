using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Repositories.Models.Configurations;

namespace Repositories.Services.StorageService
{
    public class SupabaseStorageService : IStorageService
    {
        private readonly HttpClient _httpClient;

        private readonly ILogger<SupabaseStorageService> _logger;

        private readonly SupabaseSettings _settings;

        public SupabaseStorageService(
            HttpClient httpClient,
            IOptions<SupabaseSettings> options,
            ILogger<SupabaseStorageService> logger)
        {
            _httpClient = httpClient;

            _logger = logger;

            _settings = options.Value;

            _httpClient.BaseAddress =
                new Uri($"{_settings.ProjectUrl}/storage/v1/");

            _httpClient.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue(
                    "Bearer",
                    _settings.ServiceRoleKey);

            _httpClient.DefaultRequestHeaders.Add(
                "apikey",
                _settings.ServiceRoleKey);

            _httpClient.DefaultRequestHeaders.Add(
                "x-upsert",
                "true");
        }

        public async Task<StorageUploadResult> UploadAsync(
    IFormFile file,
    string objectKey)
{
    StorageUploadResult result = new();

    try
    {
        if (file == null || file.Length == 0)
        {
            result.Success = false;
            result.Message = "No file selected.";

            return result;
        }

        await using Stream stream = file.OpenReadStream();

        using StreamContent streamContent = new(stream);

        streamContent.Headers.ContentType =
            new MediaTypeHeaderValue(file.ContentType);

        HttpResponseMessage response =
            await _httpClient.PostAsync(
                $"object/{_settings.BucketName}/{objectKey}",
                streamContent);

        string responseBody =
            await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError(
                "Supabase upload failed. Status: {Status}. Response: {Response}",
                response.StatusCode,
                responseBody);

            result.Success = false;
            result.Message = responseBody;

            return result;
        }

        _logger.LogInformation(
            "Uploaded file {FileName} to Supabase successfully.",
            file.FileName);

        result.Success = true;

        result.ObjectKey = objectKey;

        result.Message = "Upload successful.";

        return result;
    }
    catch (Exception ex)
    {
        _logger.LogError(
            ex,
            "Error uploading file to Supabase.");

        result.Success = false;

        result.Message = ex.Message;

        return result;
    }
}

        public async Task DeleteAsync(string objectKey)
{
    try
    {
        var payload = new
        {
            prefixes = new[]
            {
                objectKey
            }
        };

        using StringContent content = new(
            System.Text.Json.JsonSerializer.Serialize(payload),
            System.Text.Encoding.UTF8,
            "application/json");

        HttpResponseMessage response =
            await _httpClient.PostAsync(
                $"object/remove/{_settings.BucketName}",
                content);

        if (!response.IsSuccessStatusCode)
        {
            string error =
                await response.Content.ReadAsStringAsync();

            _logger.LogError(
                "Supabase delete failed: {Error}",
                error);

            throw new Exception(error);
        }

        _logger.LogInformation(
            "Deleted object {ObjectKey}",
            objectKey);
    }
    catch (Exception ex)
    {
        _logger.LogError(
            ex,
            "Error deleting {ObjectKey}",
            objectKey);

        throw;
    }
}

        public async Task<Stream> DownloadAsync(string objectKey)
{
    HttpResponseMessage response =
        await _httpClient.GetAsync(
            $"object/{_settings.BucketName}/{objectKey}");

    if (!response.IsSuccessStatusCode)
    {
        string error =
            await response.Content.ReadAsStringAsync();

        throw new Exception(error);
    }

    return await response.Content.ReadAsStreamAsync();
}



        public async Task<string> GetDownloadUrlAsync(
    string objectKey,
    int expiryMinutes = 10)
{
    var payload = new
    {
        expiresIn = expiryMinutes * 60
    };

    using StringContent content = new(
        System.Text.Json.JsonSerializer.Serialize(payload),
        System.Text.Encoding.UTF8,
        "application/json");

    HttpResponseMessage response =
        await _httpClient.PostAsync(
            $"object/sign/{_settings.BucketName}/{objectKey}",
            content);

    string json =
        await response.Content.ReadAsStringAsync();

    if (!response.IsSuccessStatusCode)
    {
        throw new Exception(json);
    }

    using JsonDocument document =
        JsonDocument.Parse(json);

    string signedPath =
        document.RootElement
                .GetProperty("signedURL")
                .GetString()!;

    return $"{_settings.ProjectUrl}/storage/v1{signedPath}";
}
    }
}