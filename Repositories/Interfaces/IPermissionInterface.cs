using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Repositories.Interfaces
{
    public interface IPermissionInterface
    {
        Task<List<long>> GetParentFolderIdsAsync(long folderId);

    Task<List<long>> GetChildFolderIdsAsync(long folderId);

    Task<List<long>> GetFileIdsAsync(List<long> folderIds);

    Task<long> GetGroupIdByFolderIdAsync(long folderId);

    Task<List<long>> GetGroupRoleIdsAsync(long groupId);

    Task<List<long>> GetFolderRoleIdsAsync(long folderId);

    Task ReplaceFolderRolesAsync(
        long folderId,
        List<long> roleIds);

    Task ReplaceFileRolesAsync(
        long fileId,
        List<long> roleIds);

    Task EnsureGroupContainsRolesAsync(
        long groupId,
        List<long> roleIds);

    Task RemoveUnusedGroupRolesAsync(
        long groupId);

    Task RemoveRolesFromGroupFoldersAsync(
        long groupId,
        List<long> roleIds);

    Task RemoveRolesFromGroupFilesAsync(
        long groupId,
        List<long> roleIds);
    }
}
