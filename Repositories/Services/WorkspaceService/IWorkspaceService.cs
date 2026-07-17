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
        Task<List<FolderContentVM>> GetFolderContentsAsync(long groupId,long? parentFolderId);
    }
}