using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Repositories.Models.ViewModels.Workspace;
using Repositories.Services.LayoutService;
using Repositories.Services.WorkspaceService;
using System.Security.Claims;

namespace MVC.Controllers
{
    [Authorize]
    [Route("workspace")]
    public class WorkspaceController : BaseController
    {
        private readonly IWorkspaceService _workspaceService;

        public WorkspaceController(
            IWorkspaceService workspaceService,
            ILayoutService layoutService)
            : base(layoutService)
        {
            _workspaceService = workspaceService;
        }

        public async Task<IActionResult> Index()
        {
            var model = await _workspaceService.GetWorkspaceAsync();

            return View(model);
        }

        [HttpPost("create-group")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateGroup(CreateGroupVM model)
        {
            if (!ModelState.IsValid)
            {
                TempData["Error"] = "Please fill all required fields.";

                return RedirectToAction(nameof(Index));
            }

            model.CreatedBy = Convert.ToInt64(
                User.FindFirstValue(ClaimTypes.NameIdentifier));

            var result = await _workspaceService.CreateGroupAsync(model);

            TempData[result.Success ? "Success" : "Error"] = result.Message;

            return RedirectToAction(nameof(Index));
        }

        [HttpPost("delete-group")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteGroup(long groupId)
        {
            long userId = Convert.ToInt64(
                User.FindFirstValue(ClaimTypes.NameIdentifier));

            var result = await _workspaceService.DeleteGroupAsync(
                groupId,
                userId);

            TempData[result.Success ? "Success" : "Error"] = result.Message;

            return RedirectToAction(nameof(Index));
        }
        [HttpGet("folders/{groupId}")]
        public async Task<IActionResult> GetFolders(long groupId)
        {
            var folders = await _workspaceService.GetFoldersByGroupIdAsync(groupId);

            return Json(folders);
        }
        [HttpGet("folder-contents")]
        public async Task<IActionResult> GetFolderContents(
            long groupId,
            long? folderId)
        {
            var result = await _workspaceService.GetFolderContentsAsync(
                groupId,
                folderId);

            return Json(result);
        }
    }
}