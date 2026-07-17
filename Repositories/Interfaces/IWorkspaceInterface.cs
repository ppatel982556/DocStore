using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Repositories.Models;
using Repositories.Models.DBModels.Workspace;
using Repositories.Models.ViewModels.Workspace;

namespace Repositories.Interfaces
{
    public interface IWorkspaceInterface
    {
        // #region Groups

        // Task<List<Group>> GetGroupsAsync();

        // Task<Group?> GetGroupByIdAsync(long groupId);

        // Task<bool> GroupExistsAsync(string groupName);

        // Task<ServiceResult> CreateGroupAsync(CreateGroupVM model);
        // Task<bool> UpdateGroupAsync(Group group);

        // Task<bool> DeleteGroupAsync(long groupId, long deletedBy);

        // #endregion

        // #region Group Roles

        // Task<bool> AssignRolesAsync(long groupId, List<long> roleIds);

        // Task<List<long>> GetAssignedRolesAsync(long groupId);

        // Task<bool> RemoveAssignedRolesAsync(long groupId);

        // #endregion
        Task<List<Group>> GetGroupsAsync();

        Task<Group?> GetGroupByIdAsync(long groupId);

        Task<bool> GroupExistsAsync(string groupName);

        Task<ServiceResult> CreateGroupAsync(CreateGroupVM model);

        Task<ServiceResult> DeleteGroupAsync(long groupId, long deletedBy);
        Task<List<GroupFolder>> GetFoldersByGroupIdAsync(long groupId);
        Task<List<GroupFolder>> GetFolderContentsAsync(long groupId,long? parentFolderId);
    }
}