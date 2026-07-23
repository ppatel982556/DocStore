using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Repositories.Models;
using Repositories.Models.ViewModels.Workspace;
using Repositories.Services.LayoutService;
using Repositories.Services.StorageService;
using Repositories.Services.WorkspaceService;
using System.Security.Claims;

namespace MVC.Controllers
{
    [Authorize]
    [Route("workspace")]
    public class WorkspaceController : BaseController
    {
        private readonly IWorkspaceService _workspaceService;

        private readonly IStorageService _storageService;

public WorkspaceController(
    IWorkspaceService workspaceService,
    ILayoutService layoutService,
    IStorageService storageService)
    : base(layoutService)
{
    _workspaceService = workspaceService;
    _storageService = storageService;
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
                return BadRequest(new
                {
                    success = false,
                    message = "Please fill all required fields."
                });
            }

            model.CreatedBy = Convert.ToInt64(
                User.FindFirstValue(ClaimTypes.NameIdentifier));

            var result = await _workspaceService.CreateGroupAsync(model);

            return Json(result);
        }

        [HttpGet("group/{groupId}")]
public async Task<IActionResult> GetGroup(long groupId)
{
    var result = await _workspaceService.GetGroupByIdAsync(groupId);

    if (result == null)
        return NotFound();

    return Json(result);
}

    [HttpPost("update-group")]
[ValidateAntiForgeryToken]
public async Task<IActionResult> UpdateGroup(UpdateGroupVM model)
{
    if (!ModelState.IsValid)
    {
        return BadRequest(new
        {
            success = false,
            message = "Invalid data."
        });
    }

    model.UpdatedBy = Convert.ToInt64(
        User.FindFirstValue(ClaimTypes.NameIdentifier));

    var result = await _workspaceService.UpdateGroupAsync(model);

    return Json(result);
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

    return Json(result);
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
[HttpPost("create-folder")]
[ValidateAntiForgeryToken]
public async Task<IActionResult> CreateFolder(CreateFolderVM model)
{
    if (!ModelState.IsValid)
    {
        return BadRequest(new
        {
            success = false,
            message = "Please fill all required fields."
        });
    }

    model.CreatedBy = Convert.ToInt64(
        User.FindFirstValue(ClaimTypes.NameIdentifier));

    var result = await _workspaceService.CreateFolderAsync(model);

    return Json(result);
}

[HttpPost("upload-file")]
[ValidateAntiForgeryToken]
public async Task<IActionResult> UploadFile(
    UploadFileVM model)
{
    if (!ModelState.IsValid)
    {
        return BadRequest(new
        {
            success = false,
            message = "Please select a folder and choose a file to upload."
        });
    }

string? userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);

if (!long.TryParse(userIdClaim, out long userId))
{
    return Unauthorized(new
    {
        success = false,
        message = "User is not authenticated."
    });
}

model.CreatedBy = userId;
    ServiceResult result =
        await _workspaceService.UploadFileAsync(model);

    return Json(result);
}
[HttpGet("folder/{folderId}")]
public async Task<IActionResult> GetFolder(long folderId)
{
    var result = await _workspaceService.GetFolderByIdAsync(folderId);

    if (result == null)
        return NotFound();

    return Json(result);
}

[HttpGet("file/{fileId}")]
public async Task<IActionResult> GetFile(long fileId)
{
    var result = await _workspaceService.GetFileByIdAsync(fileId);

    if (result == null)
        return NotFound();

    return Json(result);
}

[HttpPost("update-folder")]
[ValidateAntiForgeryToken]
public async Task<IActionResult> UpdateFolder(UpdateFolderVM model)
{
    model.UpdatedBy = Convert.ToInt64(
        User.FindFirstValue(ClaimTypes.NameIdentifier));

    ServiceResult result =
        await _workspaceService.UpdateFolderAsync(model);

    return Json(result);
}

[HttpPost("update-file")]
[ValidateAntiForgeryToken]
public async Task<IActionResult> UpdateFile(UpdateFileVM model)
{
    model.UpdatedBy = Convert.ToInt64(
        User.FindFirstValue(ClaimTypes.NameIdentifier));

    ServiceResult result =
        await _workspaceService.UpdateFileAsync(model);

    return Json(result);
}

[HttpGet("move-destinations")]
public async Task<IActionResult> GetMoveDestinations(
    string itemType,
    long itemId)
{
    var result = await _workspaceService.GetMoveDestinationsAsync(
        itemType,
        itemId);

    return Json(result);
}

[HttpPost("analyze-move")]
[ValidateAntiForgeryToken]
public async Task<IActionResult> AnalyzeMove(MoveItemVM model)
{
    model.UserId = Convert.ToInt64(
        User.FindFirstValue(ClaimTypes.NameIdentifier));

    int.TryParse(
        User.FindFirstValue("ActiveRoleId"),
        out int activeRoleId);

    model.ActiveRoleId = activeRoleId;

    var result = await _workspaceService.AnalyzeMovePermissionsAsync(model);

    return Json(result);
}

[HttpPost("move")]
[ValidateAntiForgeryToken]
public async Task<IActionResult> Move(MoveItemVM model)
{
    model.UserId = Convert.ToInt64(
        User.FindFirstValue(ClaimTypes.NameIdentifier));

    int.TryParse(
        User.FindFirstValue("ActiveRoleId"),
        out int activeRoleId);

    model.ActiveRoleId = activeRoleId;

    ServiceResult result = await _workspaceService.MoveItemAsync(model);

    return Json(result);
}
    }
}
