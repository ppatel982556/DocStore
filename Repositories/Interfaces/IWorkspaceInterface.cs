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
        Task<List<Group>> GetGroupsAsync();

        Task<GroupDetailsVM?> GetGroupByIdAsync(long groupId);

        Task<bool> GroupExistsAsync(string groupName);

        Task<ServiceResult> CreateGroupAsync(CreateGroupVM model);

        Task<ServiceResult> DeleteGroupAsync(long groupId, long deletedBy);
        Task<List<GroupFolder>> GetFoldersByGroupIdAsync(long groupId);
        // Task<List<GroupFolder>> GetFolderContentsAsync(long groupId,long? parentFolderId);

        Task<List<FolderContentVM>> GetFolderContentsAsync(long groupId,long? parentFolderId);
            Task<ServiceResult> UpdateGroupAsync(UpdateGroupVM model);
        Task<bool> FolderExistsAsync(long groupId,long? parentFolderId,string folderName);

        Task<ServiceResult> CreateFolderAsync(CreateFolderVM model);
        Task<FolderDetailsVM?> GetFolderByIdAsync(long folderId);
        Task<ServiceResult> UploadFileAsync(UploadFileVM model);
        Task<FileDetailsVM?> GetFileByIdAsync(long fileId);
        Task<ServiceResult> UpdateFileAsync(UpdateFileVM model);
        Task UpdateObjectKeyAsync(long fileId,string objectKey);

        Task DeleteFileAsync(long fileId);
        Task RollbackFileAsync(long fileId);
        Task<ServiceResult> UpdateFolderAsync(UpdateFolderVM model);
        Task<List<MoveDestinationNodeVM>> GetMoveDestinationsAsync(string itemType, long itemId);
        Task<MovePermissionAnalysisVM> AnalyzeMovePermissionsAsync(MoveItemVM model);
        Task<ServiceResult> MoveFileAsync(MoveItemVM model);
        Task<ServiceResult> MoveFolderAsync(MoveItemVM model);
    }
}
