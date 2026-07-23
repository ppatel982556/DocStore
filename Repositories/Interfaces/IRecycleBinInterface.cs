using Repositories.Models.ViewModels.RecycleBin;

namespace Repositories.Interfaces
{
    public interface IRecycleBinInterface
    {
        Task<List<RecycleBinItemVM>> GetRecycleBinItemsAsync();

        Task<List<RecycleBinItemVM>> GetDeletedChildrenAsync(string parentType, long parentId);

        Task DeleteGroupAsync(long groupId, long deletedBy);

        Task DeleteFolderAsync(long folderId, long deletedBy);

        Task DeleteFileAsync(long fileId, long deletedBy);

        Task RestoreGroupAsync(long groupId);

        Task RestoreFolderAsync(long folderId);

        Task RestoreFileAsync(long fileId);

        Task PermanentDeleteGroupAsync(long groupId);

        Task PermanentDeleteFolderAsync(long folderId);

        Task PermanentDeleteFileAsync(long fileId);

        Task<List<string>> GetGroupObjectKeysAsync(long groupId);

        Task<List<string>> GetFolderObjectKeysAsync(long folderId);

        Task<List<string>> GetFileObjectKeysAsync(long fileId);
    }
}
