using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Repositories.Models;
using Repositories.Services.LayoutService;
using Repositories.Services.RecycleBinService;
using System.Security.Claims;

namespace MVC.Controllers
{
    [Authorize]
    [Route("recycle-bin")]
    [Route("RecycleBin")]
    public class RecycleBinController : BaseController
    {
        private readonly IRecycleBinService _recycleBinService;
        public RecycleBinController(
            IRecycleBinService recycleBinService,
            ILayoutService layoutService)
            : base(layoutService)
        {
            _recycleBinService = recycleBinService;
        }

        [HttpGet("")]
        public async Task<IActionResult> Index()
        {
            var model = await _recycleBinService.GetRecycleBinItemsAsync();

            return View(model);
        }

        [HttpGet("items")]
        public async Task<IActionResult> Items()
        {
            var model = await _recycleBinService.GetRecycleBinItemsAsync();

            return Json(model);
        }

        [HttpGet("children")]
        public async Task<IActionResult> Children(
            string parentType,
            long parentId)
        {
            var model = await _recycleBinService.GetDeletedChildrenAsync(
                parentType,
                parentId);

            return Json(model);
        }

        [HttpPost("delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(
            string type,
            long id)
        {
            long userId = Convert.ToInt64(
                User.FindFirstValue(ClaimTypes.NameIdentifier));

            ServiceResult result =
                await _recycleBinService.DeleteAsync(
                    type,
                    id,
                    userId);

            return Json(result);
        }

        [HttpPost("restore")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Restore(
            string type,
            long id)
        {
            ServiceResult result =
                await _recycleBinService.RestoreAsync(
                    type,
                    id);

            return Json(result);
        }

        [HttpPost("permanent-delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> PermanentDelete(
            string type,
            long id)
        {
            ServiceResult result =
                await _recycleBinService.PermanentDeleteAsync(
                    type,
                    id);

            return Json(result);
        }
    }
}
