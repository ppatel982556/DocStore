using Repositories.Models;
using Repositories.Models.ViewModels.RecycleBin;

namespace Repositories.Services.RecycleBinService
{
    public interface IRecycleBinService
    {
        Task<List<RecycleBinItemVM>> GetRecycleBinItemsAsync();

        Task<List<RecycleBinItemVM>> GetDeletedChildrenAsync(string parentType, long parentId);

        Task<ServiceResult> DeleteAsync(string type, long id, long userId);

        Task<ServiceResult> RestoreAsync(string type, long id);

        Task<ServiceResult> PermanentDeleteAsync(string type, long id);
    }
}
