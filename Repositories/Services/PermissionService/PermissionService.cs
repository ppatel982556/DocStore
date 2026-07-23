using Microsoft.Extensions.Logging;
using Repositories.Interfaces;

namespace Repositories.Services.PermissionService
{
    public class PermissionService : IPermissionService
    {
        private readonly IPermissionInterface _permissionRepository;

        private readonly ILogger<PermissionService> _logger;

        public PermissionService(
            IPermissionInterface permissionRepository,
            ILogger<PermissionService> logger)
        {
            _permissionRepository = permissionRepository;
            _logger = logger;
        }

        public async Task SyncParentFoldersAsync(
            long folderId,
            List<long> roleIds)
        {
            try
            {
                var parentFolderIds =
                    await _permissionRepository.GetParentFolderIdsAsync(folderId);

                foreach (long parentFolderId in parentFolderIds)
                {
                    List<long> existingRoles =
                        await _permissionRepository.GetFolderRoleIdsAsync(parentFolderId);

                    List<long> mergedRoles = existingRoles
                        .Union(roleIds)
                        .Distinct()
                        .ToList();

                    await _permissionRepository.ReplaceFolderRolesAsync(
                        parentFolderId,
                        mergedRoles);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Error synchronizing parent folders for FolderId {FolderId}.",
                    folderId);

                throw;
            }
        }

        public async Task SyncFolderAndParentFoldersAsync(
            long folderId,
            List<long> roleIds)
        {
            try
            {
                List<long> currentFolderRoles =
                    await _permissionRepository.GetFolderRoleIdsAsync(folderId);

                List<long> mergedRoles = currentFolderRoles
                    .Union(roleIds)
                    .Distinct()
                    .ToList();

                await _permissionRepository.ReplaceFolderRolesAsync(
                    folderId,
                    mergedRoles);

                await SyncParentFoldersAsync(
                    folderId,
                    roleIds);
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Error synchronizing folder and parents for FolderId {FolderId}.",
                    folderId);

                throw;
            }
        }

        public async Task SyncChildFoldersAsync(
    long folderId,
    List<long> roleIds)
{
    try
    {
        List<long> folderIds =
            await _permissionRepository.GetChildFolderIdsAsync(folderId);

        foreach (long childFolderId in folderIds)
        {
            await _permissionRepository.ReplaceFolderRolesAsync(
                childFolderId,
                roleIds);
        }
    }
    catch (Exception ex)
    {
        _logger.LogError(
            ex,
            "Error synchronizing child folders for FolderId {FolderId}.",
            folderId);

        throw;
    }
}

        public async Task SyncFilesAsync(
    long folderId,
    List<long> roleIds)
{
    try
    {
        List<long> folderIds =
            await _permissionRepository.GetChildFolderIdsAsync(folderId);

        List<long> fileIds =
            await _permissionRepository.GetFileIdsAsync(folderIds);

        foreach (long fileId in fileIds)
        {
            await _permissionRepository.ReplaceFileRolesAsync(
                fileId,
                roleIds);
        }
    }
    catch (Exception ex)
    {
        _logger.LogError(
            ex,
            "Error synchronizing files for FolderId {FolderId}.",
            folderId);

        throw;
    }
}

        public async Task SyncGroupAsync(
    long groupId,
    List<long> roleIds)
{
    try
    {
        await _permissionRepository.EnsureGroupContainsRolesAsync(
            groupId,
            roleIds);

        await _permissionRepository.RemoveUnusedGroupRolesAsync(
            groupId);
    }
    catch (Exception ex)
    {
        _logger.LogError(
            ex,
            "Error synchronizing group permissions for GroupId {GroupId}.",
            groupId);

            throw;
    }
}

        public async Task<List<long>> GetGroupRoleIdsAsync(
    long groupId)
{
    try
    {
        return await _permissionRepository.GetGroupRoleIdsAsync(groupId);
    }
    catch (Exception ex)
    {
        _logger.LogError(
            ex,
            "Error retrieving roles for GroupId {GroupId}.",
            groupId);

        throw;
    }
}

        public async Task RemoveRolesFromGroupHierarchyAsync(
    long groupId,
    List<long> roleIds)
{
    try
    {
        if (roleIds == null || !roleIds.Any())
        {
            return;
        }

        await _permissionRepository.RemoveRolesFromGroupFoldersAsync(
            groupId,
            roleIds);

        await _permissionRepository.RemoveRolesFromGroupFilesAsync(
            groupId,
            roleIds);
    }
    catch (Exception ex)
    {
        _logger.LogError(
            ex,
            "Error removing roles from group hierarchy for GroupId {GroupId}.",
            groupId);

        throw;
    }
}
    }
}
