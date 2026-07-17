using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Npgsql;
using Repositories.Constants.Workspace.Folders;
using Repositories.Constants.Workspace.Groups;
using Repositories.Constants.Workspace.Permissions;
using Repositories.Interfaces;
using Repositories.Models;
using Repositories.Models.DBModels.Workspace;
using Repositories.Models.ViewModels.Workspace;

namespace Repositories.Implementations
{
    public class WorkspaceRepository : IWorkspaceInterface
    {
       private readonly NpgsqlConnection _conn;

        private readonly ILogger<WorkspaceRepository> _logger;

        public WorkspaceRepository(
            NpgsqlConnection conn,
            ILogger<WorkspaceRepository> logger)
        {
            _conn = conn;
            _logger = logger;
        } 

        public async Task<List<Group>> GetGroupsAsync()
{
    List<Group> groups = new();

    try
    {
        string query = $@"
            SELECT
                {GroupColumns.GroupId},
                {GroupColumns.GroupName},
                {GroupColumns.Description},
                {GroupColumns.CreatedBy},
                {GroupColumns.UpdatedBy},
                {GroupColumns.DeletedBy},
                {GroupColumns.CreatedAt},
                {GroupColumns.UpdatedAt},
                {GroupColumns.DeletedAt},
                {GroupColumns.IsDeleted}
            FROM {GroupTable.TableName}
            WHERE {GroupColumns.IsDeleted} = FALSE
            ORDER BY {GroupColumns.GroupName};";

        await _conn.OpenAsync();

        await using var cmd = new NpgsqlCommand(query, _conn);

        await using var reader = await cmd.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            groups.Add(new Group
            {
                GroupId = reader.GetInt64(reader.GetOrdinal(GroupColumns.GroupId)),

                GroupName = reader.GetString(reader.GetOrdinal(GroupColumns.GroupName)),

                Description = reader.IsDBNull(reader.GetOrdinal(GroupColumns.Description))
                    ? null
                    : reader.GetString(reader.GetOrdinal(GroupColumns.Description)),

                CreatedBy = reader.GetInt64(reader.GetOrdinal(GroupColumns.CreatedBy)),

                UpdatedBy = reader.IsDBNull(reader.GetOrdinal(GroupColumns.UpdatedBy))
                    ? null
                    : reader.GetInt64(reader.GetOrdinal(GroupColumns.UpdatedBy)),

                DeletedBy = reader.IsDBNull(reader.GetOrdinal(GroupColumns.DeletedBy))
                    ? null
                    : reader.GetInt64(reader.GetOrdinal(GroupColumns.DeletedBy)),

                CreatedAt = reader.GetDateTime(reader.GetOrdinal(GroupColumns.CreatedAt)),

                UpdatedAt = reader.IsDBNull(reader.GetOrdinal(GroupColumns.UpdatedAt))
                    ? null
                    : reader.GetDateTime(reader.GetOrdinal(GroupColumns.UpdatedAt)),

                DeletedAt = reader.IsDBNull(reader.GetOrdinal(GroupColumns.DeletedAt))
                    ? null
                    : reader.GetDateTime(reader.GetOrdinal(GroupColumns.DeletedAt)),

                IsDeleted = reader.GetBoolean(reader.GetOrdinal(GroupColumns.IsDeleted))
            });
        }

        _logger.LogInformation(
            "Retrieved {Count} workspace group(s).",
            groups.Count);

        return groups;
    }
    catch (Exception ex)
    {
        _logger.LogError(ex,
            "Error while retrieving workspace groups.");

        throw;
    }
    finally
    {
        if (_conn.State == ConnectionState.Open)
        {
            await _conn.CloseAsync();
        }
    }
}

public async Task<bool> GroupExistsAsync(string groupName)
{
    try
    {
        string query = $@"
            SELECT COUNT(*)
            FROM {GroupTable.TableName}
            WHERE LOWER({GroupColumns.GroupName}) = LOWER(@GroupName)
            AND {GroupColumns.IsDeleted} = FALSE;";

        await _conn.OpenAsync();

        await using var cmd = new NpgsqlCommand(query, _conn);

        cmd.Parameters.AddWithValue("@GroupName", groupName);

        int count = Convert.ToInt32(await cmd.ExecuteScalarAsync());

        return count > 0;
    }
    catch (Exception ex)
    {
        _logger.LogError(ex,
            "Error while checking group existence.");

        throw;
    }
    finally
    {
        if (_conn.State == ConnectionState.Open)
        {
            await _conn.CloseAsync();
        }
    }
}
public async Task<ServiceResult> CreateGroupAsync(CreateGroupVM model)
{
    ServiceResult result = new();

    try
    {
        await _conn.OpenAsync();

        await using var transaction = await _conn.BeginTransactionAsync();

        #region Insert Group

        string insertGroupQuery = $@"
            INSERT INTO {GroupTable.TableName}
            (
                {GroupColumns.GroupName},
                {GroupColumns.Description},
                {GroupColumns.CreatedBy},
                {GroupColumns.CreatedAt},
                {GroupColumns.IsDeleted}
            )
            VALUES
            (
                @GroupName,
                @Description,
                @CreatedBy,
                @CreatedAt,
                @IsDeleted
            )
            RETURNING {GroupColumns.GroupId};";

        long groupId;

        await using (var cmd = new NpgsqlCommand(insertGroupQuery, _conn, transaction))
        {
            cmd.Parameters.AddWithValue("@GroupName", model.GroupName);

            cmd.Parameters.AddWithValue(
                "@Description",
                (object?)model.Description ?? DBNull.Value);

            cmd.Parameters.AddWithValue("@CreatedBy", model.CreatedBy);

            cmd.Parameters.AddWithValue("@CreatedAt", DateTime.UtcNow);

            cmd.Parameters.AddWithValue("@IsDeleted", false);

            groupId = Convert.ToInt64(await cmd.ExecuteScalarAsync());
        }

        #endregion

        #region Assign Roles

        if (model.SelectedRoleIds.Any())
        {
            string insertRoleQuery = $@"
                INSERT INTO {GroupRoleTable.TableName}
                (
                    {GroupRoleColumns.GroupId},
                    {GroupRoleColumns.RoleId}
                )
                VALUES
                (
                    @GroupId,
                    @RoleId
                );";

            foreach (long roleId in model.SelectedRoleIds)
            {
                await using var cmd = new NpgsqlCommand(insertRoleQuery, _conn, transaction);

                cmd.Parameters.AddWithValue("@GroupId", groupId);

                cmd.Parameters.AddWithValue("@RoleId", roleId);

                await cmd.ExecuteNonQueryAsync();
            }
        }

        #endregion

        await transaction.CommitAsync();

        result.Success = true;
        result.Message = "Group created successfully.";

        _logger.LogInformation(
            "Group '{GroupName}' created successfully with GroupId {GroupId}.",
            model.GroupName,
            groupId);

        return result;
    }
    catch (Exception ex)
    {
        _logger.LogError(
            ex,
            "Error while creating group '{GroupName}'.",
            model.GroupName);

        result.Success = false;
        result.Message = "Failed to create group.";

        return result;
    }
    finally
    {
        if (_conn.State == ConnectionState.Open)
        {
            await _conn.CloseAsync();
        }
    }
}

        public async Task<Group?> GetGroupByIdAsync(long groupId)
{
    try
    {
        string query = $@"
            SELECT
                {GroupColumns.GroupId},
                {GroupColumns.GroupName},
                {GroupColumns.Description},
                {GroupColumns.CreatedBy},
                {GroupColumns.UpdatedBy},
                {GroupColumns.DeletedBy},
                {GroupColumns.CreatedAt},
                {GroupColumns.UpdatedAt},
                {GroupColumns.DeletedAt},
                {GroupColumns.IsDeleted}
            FROM {GroupTable.TableName}
            WHERE {GroupColumns.GroupId} = @GroupId
              AND {GroupColumns.IsDeleted} = FALSE
            LIMIT 1;";

        await _conn.OpenAsync();

        await using var cmd = new NpgsqlCommand(query, _conn);

        cmd.Parameters.AddWithValue("@GroupId", groupId);

        await using var reader = await cmd.ExecuteReaderAsync();

        if (!await reader.ReadAsync())
        {
            return null;
        }

        return new Group
        {
            GroupId = reader.GetInt64(reader.GetOrdinal(GroupColumns.GroupId)),
            GroupName = reader.GetString(reader.GetOrdinal(GroupColumns.GroupName)),
            Description = reader.IsDBNull(reader.GetOrdinal(GroupColumns.Description))
                ? null
                : reader.GetString(reader.GetOrdinal(GroupColumns.Description)),
            CreatedBy = reader.GetInt64(reader.GetOrdinal(GroupColumns.CreatedBy)),
            UpdatedBy = reader.IsDBNull(reader.GetOrdinal(GroupColumns.UpdatedBy))
                ? null
                : reader.GetInt64(reader.GetOrdinal(GroupColumns.UpdatedBy)),
            DeletedBy = reader.IsDBNull(reader.GetOrdinal(GroupColumns.DeletedBy))
                ? null
                : reader.GetInt64(reader.GetOrdinal(GroupColumns.DeletedBy)),
            CreatedAt = reader.GetDateTime(reader.GetOrdinal(GroupColumns.CreatedAt)),
            UpdatedAt = reader.IsDBNull(reader.GetOrdinal(GroupColumns.UpdatedAt))
                ? null
                : reader.GetDateTime(reader.GetOrdinal(GroupColumns.UpdatedAt)),
            DeletedAt = reader.IsDBNull(reader.GetOrdinal(GroupColumns.DeletedAt))
                ? null
                : reader.GetDateTime(reader.GetOrdinal(GroupColumns.DeletedAt)),
            IsDeleted = reader.GetBoolean(reader.GetOrdinal(GroupColumns.IsDeleted))
        };
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Error retrieving group with Id {GroupId}", groupId);
        throw;
    }
    finally
    {
        if (_conn.State == ConnectionState.Open)
        {
            await _conn.CloseAsync();
        }
    }
}


        public async Task<ServiceResult> DeleteGroupAsync(long groupId, long deletedBy)
{
    ServiceResult result = new();

    try
    {
        string query = $@"
            UPDATE {GroupTable.TableName}
            SET
                {GroupColumns.IsDeleted} = TRUE,
                {GroupColumns.DeletedBy} = @DeletedBy,
                {GroupColumns.DeletedAt} = @DeletedAt
            WHERE {GroupColumns.GroupId} = @GroupId
              AND {GroupColumns.IsDeleted} = FALSE;";

        await _conn.OpenAsync();

        await using var cmd = new NpgsqlCommand(query, _conn);

        cmd.Parameters.AddWithValue("@GroupId", groupId);
        cmd.Parameters.AddWithValue("@DeletedBy", deletedBy);
        cmd.Parameters.AddWithValue("@DeletedAt", DateTime.UtcNow);

        int rowsAffected = await cmd.ExecuteNonQueryAsync();

        if (rowsAffected > 0)
        {
            result.Success = true;
            result.Message = "Group deleted successfully.";

            _logger.LogInformation(
                "Group {GroupId} deleted successfully by User {UserId}.",
                groupId,
                deletedBy);
        }
        else
        {
            result.Success = false;
            result.Message = "Group not found.";
        }

        return result;
    }
    catch (Exception ex)
    {
        _logger.LogError(
            ex,
            "Error deleting group {GroupId}.",
            groupId);

        result.Success = false;
        result.Message = "Failed to delete group.";

        return result;
    }
    finally
    {
        if (_conn.State == ConnectionState.Open)
        {
            await _conn.CloseAsync();
        }
    }
}

    public async Task<List<GroupFolder>> GetFoldersByGroupIdAsync(long groupId)
{
    List<GroupFolder> folders = new();

    try
    {
        string query = $@"
            SELECT
                {GroupFolderColumns.FolderId},
                {GroupFolderColumns.GroupId},
                {GroupFolderColumns.ParentFolderId},
                {GroupFolderColumns.FolderName},
                {GroupFolderColumns.Description},
                {GroupFolderColumns.DisplayOrder},
                {GroupFolderColumns.CreatedBy},
                {GroupFolderColumns.UpdatedBy},
                {GroupFolderColumns.DeletedBy},
                {GroupFolderColumns.CreatedAt},
                {GroupFolderColumns.UpdatedAt},
                {GroupFolderColumns.DeletedAt},
                {GroupFolderColumns.IsDeleted}
            FROM {GroupFolderTable.TableName}
            WHERE {GroupFolderColumns.GroupId} = @GroupId
              AND {GroupFolderColumns.IsDeleted} = FALSE
            ORDER BY
                {GroupFolderColumns.DisplayOrder},
                {GroupFolderColumns.FolderName};";

        await _conn.OpenAsync();

        await using var cmd = new NpgsqlCommand(query, _conn);

        cmd.Parameters.AddWithValue("@GroupId", groupId);

        await using var reader = await cmd.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            folders.Add(new GroupFolder
            {
                FolderId = reader.GetInt64(reader.GetOrdinal(GroupFolderColumns.FolderId)),

                GroupId = reader.GetInt64(reader.GetOrdinal(GroupFolderColumns.GroupId)),

                ParentFolderId = reader.IsDBNull(reader.GetOrdinal(GroupFolderColumns.ParentFolderId))
                    ? null
                    : reader.GetInt64(reader.GetOrdinal(GroupFolderColumns.ParentFolderId)),

                FolderName = reader.GetString(reader.GetOrdinal(GroupFolderColumns.FolderName)),

                Description = reader.IsDBNull(reader.GetOrdinal(GroupFolderColumns.Description))
                    ? null
                    : reader.GetString(reader.GetOrdinal(GroupFolderColumns.Description)),

                DisplayOrder = reader.GetInt32(reader.GetOrdinal(GroupFolderColumns.DisplayOrder)),

                CreatedBy = reader.GetInt64(reader.GetOrdinal(GroupFolderColumns.CreatedBy)),

                UpdatedBy = reader.IsDBNull(reader.GetOrdinal(GroupFolderColumns.UpdatedBy))
                    ? null
                    : reader.GetInt64(reader.GetOrdinal(GroupFolderColumns.UpdatedBy)),

                DeletedBy = reader.IsDBNull(reader.GetOrdinal(GroupFolderColumns.DeletedBy))
                    ? null
                    : reader.GetInt64(reader.GetOrdinal(GroupFolderColumns.DeletedBy)),

                CreatedAt = reader.GetDateTime(reader.GetOrdinal(GroupFolderColumns.CreatedAt)),

                UpdatedAt = reader.IsDBNull(reader.GetOrdinal(GroupFolderColumns.UpdatedAt))
                    ? null
                    : reader.GetDateTime(reader.GetOrdinal(GroupFolderColumns.UpdatedAt)),

                DeletedAt = reader.IsDBNull(reader.GetOrdinal(GroupFolderColumns.DeletedAt))
                    ? null
                    : reader.GetDateTime(reader.GetOrdinal(GroupFolderColumns.DeletedAt)),

                IsDeleted = reader.GetBoolean(reader.GetOrdinal(GroupFolderColumns.IsDeleted))
            });
        }

        _logger.LogInformation(
            "Retrieved {Count} folder(s) for GroupId {GroupId}.",
            folders.Count,
            groupId);

        return folders;
    }
    catch (Exception ex)
    {
        _logger.LogError(
            ex,
            "Error while retrieving folders for GroupId {GroupId}.",
            groupId);

        throw;
    }
    finally
    {
        if (_conn.State == ConnectionState.Open)
        {
            await _conn.CloseAsync();
        }
    }
}

        public async Task<List<GroupFolder>> GetFolderContentsAsync(
    long groupId,
    long? parentFolderId)
{
    List<GroupFolder> folders = new();

    try
    {
        string query;

        if (parentFolderId == null)
        {
            query = $@"
                SELECT
                    {GroupFolderColumns.FolderId},
                    {GroupFolderColumns.GroupId},
                    {GroupFolderColumns.ParentFolderId},
                    {GroupFolderColumns.FolderName},
                    {GroupFolderColumns.Description},
                    {GroupFolderColumns.DisplayOrder},
                    {GroupFolderColumns.CreatedBy},
                    {GroupFolderColumns.UpdatedBy},
                    {GroupFolderColumns.DeletedBy},
                    {GroupFolderColumns.CreatedAt},
                    {GroupFolderColumns.UpdatedAt},
                    {GroupFolderColumns.DeletedAt},
                    {GroupFolderColumns.IsDeleted}
                FROM {GroupFolderTable.TableName}
                WHERE
                    {GroupFolderColumns.GroupId} = @GroupId
                    AND {GroupFolderColumns.ParentFolderId} IS NULL
                    AND {GroupFolderColumns.IsDeleted} = FALSE
                ORDER BY
                    {GroupFolderColumns.DisplayOrder},
                    {GroupFolderColumns.FolderName};";
        }
        else
        {
            query = $@"
                SELECT
                    {GroupFolderColumns.FolderId},
                    {GroupFolderColumns.GroupId},
                    {GroupFolderColumns.ParentFolderId},
                    {GroupFolderColumns.FolderName},
                    {GroupFolderColumns.Description},
                    {GroupFolderColumns.DisplayOrder},
                    {GroupFolderColumns.CreatedBy},
                    {GroupFolderColumns.UpdatedBy},
                    {GroupFolderColumns.DeletedBy},
                    {GroupFolderColumns.CreatedAt},
                    {GroupFolderColumns.UpdatedAt},
                    {GroupFolderColumns.DeletedAt},
                    {GroupFolderColumns.IsDeleted}
                FROM {GroupFolderTable.TableName}
                WHERE
                    {GroupFolderColumns.GroupId} = @GroupId
                    AND {GroupFolderColumns.ParentFolderId} = @ParentFolderId
                    AND {GroupFolderColumns.IsDeleted} = FALSE
                ORDER BY
                    {GroupFolderColumns.DisplayOrder},
                    {GroupFolderColumns.FolderName};";
        }

        await _conn.OpenAsync();

        await using var cmd = new NpgsqlCommand(query, _conn);

        cmd.Parameters.AddWithValue("@GroupId", groupId);

        if (parentFolderId.HasValue)
        {
            cmd.Parameters.AddWithValue("@ParentFolderId", parentFolderId.Value);
        }

        await using var reader = await cmd.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            folders.Add(new GroupFolder
            {
                FolderId = reader.GetInt64(reader.GetOrdinal(GroupFolderColumns.FolderId)),

                GroupId = reader.GetInt64(reader.GetOrdinal(GroupFolderColumns.GroupId)),

                ParentFolderId = reader.IsDBNull(reader.GetOrdinal(GroupFolderColumns.ParentFolderId))
                    ? null
                    : reader.GetInt64(reader.GetOrdinal(GroupFolderColumns.ParentFolderId)),

                FolderName = reader.GetString(reader.GetOrdinal(GroupFolderColumns.FolderName)),

                Description = reader.IsDBNull(reader.GetOrdinal(GroupFolderColumns.Description))
                    ? null
                    : reader.GetString(reader.GetOrdinal(GroupFolderColumns.Description)),

                DisplayOrder = reader.GetInt32(reader.GetOrdinal(GroupFolderColumns.DisplayOrder)),

                CreatedBy = reader.GetInt64(reader.GetOrdinal(GroupFolderColumns.CreatedBy)),

                UpdatedBy = reader.IsDBNull(reader.GetOrdinal(GroupFolderColumns.UpdatedBy))
                    ? null
                    : reader.GetInt64(reader.GetOrdinal(GroupFolderColumns.UpdatedBy)),

                DeletedBy = reader.IsDBNull(reader.GetOrdinal(GroupFolderColumns.DeletedBy))
                    ? null
                    : reader.GetInt64(reader.GetOrdinal(GroupFolderColumns.DeletedBy)),

                CreatedAt = reader.GetDateTime(reader.GetOrdinal(GroupFolderColumns.CreatedAt)),

                UpdatedAt = reader.IsDBNull(reader.GetOrdinal(GroupFolderColumns.UpdatedAt))
                    ? null
                    : reader.GetDateTime(reader.GetOrdinal(GroupFolderColumns.UpdatedAt)),

                DeletedAt = reader.IsDBNull(reader.GetOrdinal(GroupFolderColumns.DeletedAt))
                    ? null
                    : reader.GetDateTime(reader.GetOrdinal(GroupFolderColumns.DeletedAt)),

                IsDeleted = reader.GetBoolean(reader.GetOrdinal(GroupFolderColumns.IsDeleted))
            });
        }

        _logger.LogInformation(
            "Retrieved {Count} folder(s) for GroupId {GroupId} and ParentFolderId {ParentFolderId}.",
            folders.Count,
            groupId,
            parentFolderId);

        return folders;
    }
    catch (Exception ex)
    {
        _logger.LogError(
            ex,
            "Error retrieving folder contents for GroupId {GroupId}.",
            groupId);

        throw;
    }
    finally
    {
        if (_conn.State == ConnectionState.Open)
        {
            await _conn.CloseAsync();
        }
    }
}
    }
}