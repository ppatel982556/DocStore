using Microsoft.Extensions.Logging;
using Repositories.Interfaces;
using Repositories.Models;
using Repositories.Models.ViewModels.RecycleBin;
using Repositories.Services.StorageService;

namespace Repositories.Services.RecycleBinService
{
    public class RecycleBinService : IRecycleBinService
    {
        private readonly IRecycleBinInterface _recycleBinRepository;

        private readonly IStorageService _storageService;

        private readonly ILogger<RecycleBinService> _logger;

        public RecycleBinService(
            IRecycleBinInterface recycleBinRepository,
            IStorageService storageService,
            ILogger<RecycleBinService> logger)
        {
            _recycleBinRepository = recycleBinRepository;
            _storageService = storageService;
            _logger = logger;
        }

        public async Task<List<RecycleBinItemVM>> GetRecycleBinItemsAsync()
        {
            return await _recycleBinRepository.GetRecycleBinItemsAsync();
        }

        public async Task<List<RecycleBinItemVM>> GetDeletedChildrenAsync(
            string parentType,
            long parentId)
        {
            return await _recycleBinRepository.GetDeletedChildrenAsync(
                NormalizeType(parentType),
                parentId);
        }

        public async Task<ServiceResult> DeleteAsync(
            string type,
            long id,
            long userId)
        {
            ServiceResult result = new();

            try
            {
                switch (NormalizeType(type))
                {
                    case "group":
                        await _recycleBinRepository.DeleteGroupAsync(id, userId);
                        break;

                    case "folder":
                        await _recycleBinRepository.DeleteFolderAsync(id, userId);
                        break;

                    case "file":
                        await _recycleBinRepository.DeleteFileAsync(id, userId);
                        break;

                    default:
                        return InvalidTypeResult();
                }

                result.Success = true;
                result.Message = "Item moved to trash successfully.";

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Error moving {Type} {Id} to trash.",
                    type,
                    id);

                result.Success = false;
                result.Message = "Unable to move item to trash.";

                return result;
            }
        }

        public async Task<ServiceResult> RestoreAsync(
            string type,
            long id)
        {
            ServiceResult result = new();

            try
            {
                switch (NormalizeType(type))
                {
                    case "group":
                        await _recycleBinRepository.RestoreGroupAsync(id);
                        break;

                    case "folder":
                        await _recycleBinRepository.RestoreFolderAsync(id);
                        break;

                    case "file":
                        await _recycleBinRepository.RestoreFileAsync(id);
                        break;

                    default:
                        return InvalidTypeResult();
                }

                result.Success = true;
                result.Message = "Item restored successfully.";

                return result;
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogWarning(
                    ex,
                    "Recycle bin restore blocked for {Type} {Id}.",
                    type,
                    id);

                result.Success = false;
                result.Message = ex.Message;

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Error restoring {Type} {Id}.",
                    type,
                    id);

                result.Success = false;
                result.Message = "Unable to restore item.";

                return result;
            }
        }

        public async Task<ServiceResult> PermanentDeleteAsync(
            string type,
            long id)
        {
            ServiceResult result = new();

            try
            {
                string normalizedType = NormalizeType(type);
                List<string> objectKeys = normalizedType switch
                {
                    "group" => await _recycleBinRepository.GetGroupObjectKeysAsync(id),
                    "folder" => await _recycleBinRepository.GetFolderObjectKeysAsync(id),
                    "file" => await _recycleBinRepository.GetFileObjectKeysAsync(id),
                    _ => new List<string>()
                };

                if (normalizedType is not ("group" or "folder" or "file"))
                {
                    return InvalidTypeResult();
                }

                foreach (string objectKey in objectKeys.Where(key => !string.IsNullOrWhiteSpace(key)))
                {
                    try
                    {
                        await _storageService.DeleteAsync(objectKey);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(
                            ex,
                            "Storage object {ObjectKey} could not be deleted during recycle bin permanent delete. Continuing with database cleanup.",
                            objectKey);
                    }
                }

                switch (normalizedType)
                {
                    case "group":
                        await _recycleBinRepository.PermanentDeleteGroupAsync(id);
                        break;

                    case "folder":
                        await _recycleBinRepository.PermanentDeleteFolderAsync(id);
                        break;

                    case "file":
                        await _recycleBinRepository.PermanentDeleteFileAsync(id);
                        break;
                }

                result.Success = true;
                result.Message = "Item permanently deleted successfully.";

                return result;
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogWarning(
                    ex,
                    "Recycle bin permanent delete blocked for {Type} {Id}.",
                    type,
                    id);

                result.Success = false;
                result.Message = ex.Message;

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Error permanently deleting {Type} {Id}.",
                    type,
                    id);

                result.Success = false;
                result.Message = "Unable to permanently delete item.";

                return result;
            }
        }

        private static string NormalizeType(string type)
        {
            return (type ?? string.Empty).Trim().ToLowerInvariant();
        }

        private static ServiceResult InvalidTypeResult()
        {
            return new ServiceResult
            {
                Success = false,
                Message = "Invalid recycle bin item type."
            };
        }
    }
}
