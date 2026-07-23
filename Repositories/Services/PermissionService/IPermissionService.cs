using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Repositories.Services.PermissionService
{
    public interface IPermissionService
    {
Task SyncParentFoldersAsync(
    long folderId,
    List<long> roleIds);

Task SyncFolderAndParentFoldersAsync(
    long folderId,
    List<long> roleIds);

Task SyncChildFoldersAsync(
    long folderId,
    List<long> roleIds);

Task SyncFilesAsync(
    long folderId,
    List<long> roleIds);

    Task SyncGroupAsync(
    long groupId,
    List<long> roleIds);

Task<List<long>> GetGroupRoleIdsAsync(
    long groupId);

Task RemoveRolesFromGroupHierarchyAsync(
    long groupId,
    List<long> roleIds);
    }
}
