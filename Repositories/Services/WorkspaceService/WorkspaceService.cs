using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Repositories.Interfaces;
using Repositories.Models;
using Repositories.Models.ViewModels.Workspace;

namespace Repositories.Services.WorkspaceService
{
    public class WorkspaceService : IWorkspaceService
    {
        private readonly IWorkspaceInterface _workspaceRepository;

        private readonly ILogger<WorkspaceService> _logger;

        public WorkspaceService(
            IWorkspaceInterface workspaceRepository,
            ILogger<WorkspaceService> logger)
        {
            _workspaceRepository = workspaceRepository;
            _logger = logger;
        }

        public async Task<WorkspaceVM> GetWorkspaceAsync()
        {
            WorkspaceVM vm = new();

            var groups = await _workspaceRepository.GetGroupsAsync();

            vm.Groups = groups.Select(g => new GroupVM
            {
                GroupId = g.GroupId,
                GroupName = g.GroupName,
                Description = g.Description
            }).ToList();

            return vm;
        }

        public async Task<ServiceResult> CreateGroupAsync(CreateGroupVM model)
        {
            ServiceResult result = new();

            try
            {
                if (string.IsNullOrWhiteSpace(model.GroupName))
                {
                    result.Success = false;
                    result.Message = "Group name is required.";

                    return result;
                }

                bool exists = await _workspaceRepository.GroupExistsAsync(model.GroupName);

                if (exists)
                {
                    result.Success = false;
                    result.Message = "Group name already exists.";

                    return result;
                }

                result = await _workspaceRepository.CreateGroupAsync(model);

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Error while creating group.");

                result.Success = false;
                result.Message = "Unable to create group.";

                return result;
            }
        }

        public async Task<ServiceResult> DeleteGroupAsync(
            long groupId,
            long userId)
        {
            return await _workspaceRepository.DeleteGroupAsync(
                groupId,
                userId);
        }
        public async Task<List<FolderTreeVM>> GetFoldersByGroupIdAsync(long groupId)
{
    var folders = await _workspaceRepository.GetFoldersByGroupIdAsync(groupId);

    return folders.Select(f => new FolderTreeVM
    {
        Id = f.FolderId,
        ParentId = f.ParentFolderId,
        Text = f.FolderName,
        Expanded = true,
        HasChildren = folders.Any(x => x.ParentFolderId == f.FolderId)
    }).ToList();
}
public async Task<List<FolderContentVM>> GetFolderContentsAsync(
    long groupId,
    long? parentFolderId)
{
    var folders = await _workspaceRepository.GetFolderContentsAsync(groupId,parentFolderId);

    List<FolderContentVM> result = new();

    foreach (var folder in folders)
    {
        result.Add(new FolderContentVM
        {
            Id = folder.FolderId,

            Name = folder.FolderName,

            IsFolder = true,

            Type = "Folder",

            Path = folder.ParentFolderId == null
                ? "/"
                : "...",

            Size = null,

            CreatedAt = folder.CreatedAt
        });
    }

    return result;
}
    }
    }
