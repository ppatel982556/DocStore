using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Repositories.Models;
using Repositories.Models.ViewModels.Workspace;

namespace Repositories.Services.WorkspaceService
{
    public interface IWorkspaceService
    {
        Task<WorkspaceVM> GetWorkspaceAsync();

        Task<ServiceResult> CreateGroupAsync(CreateGroupVM model);

        Task<ServiceResult> DeleteGroupAsync(long groupId, long userId);
        Task<List<FolderTreeVM>> GetFoldersByGroupIdAsync(long groupId);
        Task<List<FolderContentVM>> GetFolderContentsAsync(long groupId,long? folderId);
        Task<GroupDetailsVM?> GetGroupByIdAsync(long groupId);
        Task<ServiceResult> UpdateGroupAsync(UpdateGroupVM model);
        Task<ServiceResult> CreateFolderAsync(CreateFolderVM model);
        Task<ServiceResult> UploadFileAsync(UploadFileVM model);
        Task<FileDetailsVM?> GetFileByIdAsync(long fileId);
        Task<ServiceResult> UpdateFileAsync(UpdateFileVM model);
        Task<FolderDetailsVM?> GetFolderByIdAsync(long folderId);
        Task<ServiceResult> UpdateFolderAsync(UpdateFolderVM model);
        Task<List<MoveDestinationNodeVM>> GetMoveDestinationsAsync(string itemType, long itemId);
        Task<MovePermissionAnalysisVM> AnalyzeMovePermissionsAsync(MoveItemVM model);
        Task<ServiceResult> MoveItemAsync(MoveItemVM model);
    }
}
