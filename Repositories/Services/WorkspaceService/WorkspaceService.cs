using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Repositories.Interfaces;
using Repositories.Models;
using Repositories.Models.ViewModels.Workspace;
using Repositories.Constants.Workspace.Move;
using Repositories.Services.PermissionService;
using Repositories.Services.StorageService;

namespace Repositories.Services.WorkspaceService
{
    public class WorkspaceService : IWorkspaceService
    {
        private readonly IWorkspaceInterface _workspaceRepository;

        private readonly ILogger<WorkspaceService> _logger;

        private readonly IRoleInterface _roleRepo;

        private readonly IStorageService _storageService;
        private readonly IWorkspaceInterface _workspaceInterface;

private readonly IPermissionService _permissionService;

        public WorkspaceService(
    IWorkspaceInterface workspaceRepository,
    ILogger<WorkspaceService> logger,
    IRoleInterface roleRepo,
    IStorageService storageService,
    IWorkspaceInterface workspaceInterface,
    IPermissionService permissionService)
{
    _workspaceRepository = workspaceRepository;

    _logger = logger;

    _roleRepo = roleRepo;

    _storageService = storageService;
    _workspaceInterface = workspaceInterface;
    _permissionService = permissionService;
}

        public async Task<WorkspaceVM> GetWorkspaceAsync()
{
    WorkspaceVM vm = new();

    var groups = await _workspaceRepository.GetGroupsAsync();

    vm.Groups = groups.Select(g => new GroupVM
    {
        GroupId = g.GroupId,
        GroupName = g.GroupName,
        Description = g.Description
    }).ToList();

    vm.Roles = await _roleRepo.GetAllRolesAsync();

    return vm;
}

        public async Task<ServiceResult> CreateGroupAsync(CreateGroupVM model)
        {
            ServiceResult result = new();

            try
            {
                if (string.IsNullOrWhiteSpace(model.GroupName))
                {
                    result.Success = false;
                    result.Message = "Group name is required.";

                    return result;
                }

                bool exists = await _workspaceRepository.GroupExistsAsync(model.GroupName);

                if (exists)
                {
                    result.Success = false;
                    result.Message = "Group name already exists.";

                    return result;
                }

                result = await _workspaceRepository.CreateGroupAsync(model);

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Error while creating group.");

                result.Success = false;
                result.Message = "Unable to create group.";

                return result;
            }
        }

        public async Task<ServiceResult> DeleteGroupAsync(
            long groupId,
            long userId)
        {
            return await _workspaceRepository.DeleteGroupAsync(
                groupId,
                userId);
        }
        public async Task<List<FolderTreeVM>> GetFoldersByGroupIdAsync(long groupId)
{
    var folders = await _workspaceRepository.GetFoldersByGroupIdAsync(groupId);

    return folders.Select(f => new FolderTreeVM
    {
        Id = f.FolderId,

        ParentId = f.ParentFolderId,

        Text = f.FolderName,

        Expanded = true,

        HasChildren = f.HasChildren,

        Level = f.Level
    }).ToList();
}

public async Task<List<FolderContentVM>> GetFolderContentsAsync(
    long groupId,
    long? folderId)
{
    return await _workspaceRepository.GetFolderContentsAsync(
        groupId,
        folderId);
}
public async Task<GroupDetailsVM?> GetGroupByIdAsync(long groupId)
{
    return await _workspaceRepository.GetGroupByIdAsync(groupId);
}
public async Task<ServiceResult> UpdateGroupAsync(UpdateGroupVM model)
{
    ServiceResult result = new();

    try
    {
        if (string.IsNullOrWhiteSpace(model.GroupName))
        {
            result.Success = false;
            result.Message = "Group name is required.";

            return result;
        }

        List<long> existingRoleIds =
            await _permissionService.GetGroupRoleIdsAsync(model.GroupId);

        result = await _workspaceRepository.UpdateGroupAsync(model);

        if (!result.Success)
        {
            return result;
        }

        List<long> removedRoleIds = existingRoleIds
            .Except(model.SelectedRoleIds)
            .ToList();

        if (removedRoleIds.Any())
        {
            await _permissionService.RemoveRolesFromGroupHierarchyAsync(
                model.GroupId,
                removedRoleIds);
        }

        return result;
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Error updating group.");

        result.Success = false;
        result.Message = "Unable to update group.";

        return result;
    }
}
public async Task<ServiceResult> CreateFolderAsync(CreateFolderVM model)
{
    ServiceResult result = new();

    try
    {
        if (string.IsNullOrWhiteSpace(model.FolderName))
        {
            result.Success = false;
            result.Message = "Folder name is required.";

            return result;
        }

        bool exists = await _workspaceRepository.FolderExistsAsync(
            model.GroupId,
            model.ParentFolderId,
            model.FolderName);

        if (exists)
        {
            result.Success = false;
            result.Message = "A folder with the same name already exists.";

            return result;
        }

        result = await _workspaceRepository.CreateFolderAsync(model);

        if (!result.Success)
        {
            return result;
        }

        if (model.SelectedRoleIds.Any())
        {
            if (result.Id.HasValue &&
                model.ParentFolderId.HasValue)
            {
                await _permissionService.SyncParentFoldersAsync(
                    result.Id.Value,
                    model.SelectedRoleIds);
            }

            await _permissionService.SyncGroupAsync(
                model.GroupId,
                model.SelectedRoleIds);
        }

        return result;
    }
    catch (Exception ex)
    {
        _logger.LogError(
            ex,
            "Error while creating folder.");

        result.Success = false;
        result.Message = "Unable to create folder.";

        return result;
    }
}

public async Task<ServiceResult> UploadFileAsync(UploadFileVM model)
{
    ServiceResult result = new();

    try
    {
        if (model.GroupId <= 0)
        {
            result.Success = false;
            result.Message = "Please select a group.";

            return result;
        }

        if (model.FolderId <= 0)
        {
            result.Success = false;
            result.Message = "Please select a folder. Files cannot be uploaded at root level.";

            return result;
        }

        if (model.File == null || model.File.Length == 0)
        {
            result.Success = false;
            result.Message = "Please select a file.";

            return result;
        }

        var dbResult = await _workspaceRepository.UploadFileAsync(model);

        if (!dbResult.Success)
        {
            return dbResult;
        }

        long fileId = dbResult.Id!.Value;
        string objectKey =
            $"groups/{model.GroupId}/folders/{model.FolderId}/files/{fileId}_{model.File.FileName}";

        var uploadResult =
            await _storageService.UploadAsync(
                model.File,
                objectKey);

        if (!uploadResult.Success)
        {
            await _workspaceRepository.RollbackFileAsync(fileId);
            return new ServiceResult
            {
                Success = false,
                Message = uploadResult.Message
            };
        }

        await _workspaceRepository.UpdateObjectKeyAsync(
            fileId,
            objectKey);

        if (model.SelectedRoleIds.Any())
        {
            List<long> selectedRoleIds = model.SelectedRoleIds
                .Select(roleId => Convert.ToInt64(roleId))
                .ToList();

            await _permissionService.SyncFolderAndParentFoldersAsync(
                model.FolderId,
                selectedRoleIds);

            await _permissionService.SyncGroupAsync(
                model.GroupId,
                selectedRoleIds);
        }

        return new ServiceResult
        {
            Success = true,
            Message = "File uploaded successfully."
        };
    }
    catch (Exception ex)
    {
        _logger.LogError(
            ex,
            "Error uploading file.");

        return new ServiceResult
        {
            Success = false,
            Message = "Unable to upload file."
        };
    }
}
public async Task<FolderDetailsVM?> GetFolderByIdAsync(long folderId)
{
    return await _workspaceRepository.GetFolderByIdAsync(folderId);
}

public async Task<FileDetailsVM?> GetFileByIdAsync(long fileId)
{
    return await _workspaceRepository.GetFileByIdAsync(fileId);
}

public async Task<ServiceResult> UpdateFileAsync(UpdateFileVM model)
{
    ServiceResult result = new();

    try
    {
        if (string.IsNullOrWhiteSpace(model.FileName))
        {
            result.Success = false;
            result.Message = "File name is required.";

            return result;
        }

        FileDetailsVM? file =
            await _workspaceRepository.GetFileByIdAsync(model.FileId);

        if (file == null)
        {
            result.Success = false;
            result.Message = "File not found.";

            return result;
        }

        result = await _workspaceRepository.UpdateFileAsync(model);

        if (!result.Success)
        {
            return result;
        }

        if (model.SelectedRoleIds.Any())
        {
            await _permissionService.SyncFolderAndParentFoldersAsync(
                file.FolderId,
                model.SelectedRoleIds);

            await _permissionService.SyncGroupAsync(
                file.GroupId,
                model.SelectedRoleIds);
        }

        return result;
    }
    catch (Exception ex)
    {
        _logger.LogError(
            ex,
            "Error updating file {FileId}.",
            model.FileId);

        result.Success = false;
        result.Message = "Unable to update file.";

        return result;
    }
}

public async Task<ServiceResult> UpdateFolderAsync(UpdateFolderVM model)
{
    ServiceResult result =
        await _workspaceInterface.UpdateFolderAsync(model);

    if (!result.Success)
    {
        return result;
    }

    if (model.PermissionOptions.SyncParents)
    {
        await _permissionService.SyncParentFoldersAsync(
            model.FolderId,
            model.SelectedRoleIds);
    }

    if (model.PermissionOptions.ApplyToChildren)
    {
        await _permissionService.SyncChildFoldersAsync(
            model.FolderId,
            model.SelectedRoleIds);

        if (model.PermissionOptions.ApplyToFiles)
        {
            await _permissionService.SyncFilesAsync(
                model.FolderId,
                model.SelectedRoleIds);
        }
    }

    if (model.PermissionOptions.SyncGroup)
    {
        await _permissionService.SyncGroupAsync(
            model.GroupId,
            model.SelectedRoleIds);
    }

    return result;
}

public async Task<List<MoveDestinationNodeVM>> GetMoveDestinationsAsync(
    string itemType,
    long itemId)
{
    return await _workspaceRepository.GetMoveDestinationsAsync(
        itemType,
        itemId);
}

public async Task<MovePermissionAnalysisVM> AnalyzeMovePermissionsAsync(
    MoveItemVM model)
{
    return await _workspaceRepository.AnalyzeMovePermissionsAsync(model);
}

public async Task<ServiceResult> MoveItemAsync(MoveItemVM model)
{
    try
    {
        string itemType = (model.ItemType ?? string.Empty)
            .Trim()
            .ToLowerInvariant();

        if (itemType == MoveConstants.ItemTypeFolder)
        {
            return await _workspaceRepository.MoveFolderAsync(model);
        }

        if (itemType == MoveConstants.ItemTypeFile)
        {
            return await _workspaceRepository.MoveFileAsync(model);
        }

        return new ServiceResult
        {
            Success = false,
            Message = "Invalid move item type."
        };
    }
    catch (Exception ex)
    {
        _logger.LogError(
            ex,
            "Error moving {ItemType} {ItemId}.",
            model.ItemType,
            model.ItemId);

        return new ServiceResult
        {
            Success = false,
            Message = "Unable to move item."
        };
    }
}
    }
    }
