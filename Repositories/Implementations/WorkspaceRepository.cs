using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Npgsql;
using Repositories.Constants.Roles;
using Repositories.Constants.RolePermissions;
using Repositories.Constants.Users;
using Repositories.Constants.Workspace.Activity;
using Repositories.Constants.Workspace.Files;
using Repositories.Constants.Workspace.Folders;
using Repositories.Constants.Workspace.Groups;
using Repositories.Constants.Workspace.Move;
using Repositories.Constants.Workspace.Permissions;
using Repositories.Helpers;
using Repositories.Interfaces;
using Repositories.Models;
using Repositories.Models.DBModels.Workspace;
using Repositories.Models.ViewModels;
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

        public async Task<GroupDetailsVM?> GetGroupByIdAsync(long groupId)
        {
            try
            {
                await _conn.OpenAsync();

                GroupDetailsVM? group = null;

                #region Group Details

                string groupQuery = $@"
SELECT

    g.{GroupColumns.GroupId},
    g.{GroupColumns.GroupName},
    g.{GroupColumns.Description},
    g.{GroupColumns.CreatedAt},
    g.{GroupColumns.UpdatedAt},

    creator.{UserColumns.FirstName} || ' ' ||
    creator.{UserColumns.LastName} AS CreatedBy,

    CASE
        WHEN updater.{UserColumns.UserId} IS NULL
        THEN NULL
        ELSE updater.{UserColumns.FirstName} || ' ' ||
             updater.{UserColumns.LastName}
    END AS UpdatedBy,

    (
        SELECT COUNT(*)
        FROM {GroupFolderTable.TableName} gf
        WHERE
            gf.{GroupFolderColumns.GroupId}=g.{GroupColumns.GroupId}
            AND gf.{GroupFolderColumns.IsDeleted}=FALSE
    ) FolderCount,

    (
        SELECT COUNT(*)
        FROM {GroupFileTable.TableName} f
        WHERE
            f.{GroupFileColumns.GroupId}=g.{GroupColumns.GroupId}
            AND f.{GroupFileColumns.IsDeleted}=FALSE
    ) FileCount,

    (
        SELECT COALESCE(SUM(f.{GroupFileColumns.FileSize}),0)
        FROM {GroupFileTable.TableName} f
        WHERE
            f.{GroupFileColumns.GroupId}=g.{GroupColumns.GroupId}
            AND f.{GroupFileColumns.IsDeleted}=FALSE
    ) TotalSize

FROM {GroupTable.TableName} g

INNER JOIN {UserTable.TableName} creator
    ON creator.{UserColumns.UserId}=g.{GroupColumns.CreatedBy}

LEFT JOIN {UserTable.TableName} updater
    ON updater.{UserColumns.UserId}=g.{GroupColumns.UpdatedBy}

WHERE

    g.{GroupColumns.GroupId}=@GroupId

    AND

    g.{GroupColumns.IsDeleted}=FALSE;";

                await using (var cmd = new NpgsqlCommand(groupQuery, _conn))
                {
                    cmd.Parameters.AddWithValue("@GroupId", groupId);

                    await using var reader = await cmd.ExecuteReaderAsync();

                    if (!await reader.ReadAsync())
                    {
                        return null;
                    }

                    group = new GroupDetailsVM
                    {
                        GroupId = reader.GetInt64(
                reader.GetOrdinal(GroupColumns.GroupId)),

                        GroupName = reader.GetString(
                reader.GetOrdinal(GroupColumns.GroupName)),

                        Description = reader.IsDBNull(
                reader.GetOrdinal(GroupColumns.Description))
                    ? string.Empty
                    : reader.GetString(
                        reader.GetOrdinal(GroupColumns.Description)),

                        CreatedBy = reader.GetString(
                reader.GetOrdinal("CreatedBy")),

                        CreatedAt = reader.GetDateTime(
                reader.GetOrdinal(GroupColumns.CreatedAt)),

                        UpdatedBy = reader.IsDBNull(
                reader.GetOrdinal("UpdatedBy"))
                    ? string.Empty
                    : reader.GetString(
                        reader.GetOrdinal("UpdatedBy")),

                        UpdatedAt = reader.IsDBNull(
                reader.GetOrdinal(GroupColumns.UpdatedAt))
                    ? null
                    : reader.GetDateTime(
                        reader.GetOrdinal(GroupColumns.UpdatedAt)),

                        FolderCount = reader.GetInt32(
                reader.GetOrdinal("FolderCount")),

                        FileCount = reader.GetInt32(
                reader.GetOrdinal("FileCount")),

                        TotalSize = reader.GetInt64(
                reader.GetOrdinal("TotalSize"))
                    };
                }

                #endregion

                #region Assigned Roles

                string roleQuery = $@"
            SELECT
                r.{RoleColumns.RoleId},
                r.{RoleColumns.RoleName},
                r.{RoleColumns.Description}
            FROM {GroupRoleTable.TableName} gr
            INNER JOIN {RoleTable.TableName} r
                ON r.{RoleColumns.RoleId}=gr.{GroupRoleColumns.RoleId}
            WHERE
                gr.{GroupRoleColumns.GroupId}=@GroupId
            ORDER BY
                r.{RoleColumns.RoleName};";

                await using (var cmd = new NpgsqlCommand(roleQuery, _conn))
                {
                    cmd.Parameters.AddWithValue("@GroupId", groupId);

                    await using var reader = await cmd.ExecuteReaderAsync();

                    while (await reader.ReadAsync())
                    {
                        group!.Roles.Add(new RoleVM
                        {
                            RoleId = reader.GetInt32(
                                reader.GetOrdinal(RoleColumns.RoleId)),

                            RoleName = reader.GetString(
                                reader.GetOrdinal(RoleColumns.RoleName)),

                            Description = reader.IsDBNull(
                                reader.GetOrdinal(RoleColumns.Description))
                                    ? string.Empty
                                    : reader.GetString(
                                        reader.GetOrdinal(RoleColumns.Description))
                        });
                        group.SelectedRoleIds.Add(
            reader.GetInt32(
                reader.GetOrdinal(RoleColumns.RoleId)));
                        group.TotalSizeDisplay =
                    FileSizeHelper.Format(group.TotalSize);
                    }
                }

                #endregion
            return group;
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Error retrieving group details for GroupId {GroupId}.",
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
                {GroupFolderColumns.FullPath},
                {GroupFolderColumns.Level},
                {GroupFolderColumns.HasChildren},
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
                AND {GroupFolderColumns.IsDeleted} = FALSE
            ORDER BY
                {GroupFolderColumns.Level},
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

                        FullPath = reader.GetString(reader.GetOrdinal(GroupFolderColumns.FullPath)),

                        Level = reader.GetInt32(reader.GetOrdinal(GroupFolderColumns.Level)),

                        HasChildren = reader.GetBoolean(reader.GetOrdinal(GroupFolderColumns.HasChildren)),

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

        public async Task<List<FolderContentVM>> GetFolderContentsAsync(
    long groupId,
    long? parentFolderId)
        {
            List<FolderContentVM> contents = new();

            try
            {
                string query;

                if (parentFolderId == null)
                {
                    query = $@"
SELECT
    gf.{GroupFolderColumns.FolderId} AS Id,
    gf.{GroupFolderColumns.FolderName} AS Name,
    TRUE AS IsFolder,
    'Folder' AS Type,
    gf.{GroupFolderColumns.FullPath} AS Path,
    gf.{GroupFolderColumns.Level} AS Level,
    NULL::BIGINT AS Size,
    u.c_firstname || ' ' || u.c_lastname AS CreatedBy,
    gf.{GroupFolderColumns.CreatedAt} AS CreatedAt
FROM {GroupFolderTable.TableName} gf
INNER JOIN t_users u
    ON u.c_userid = gf.{GroupFolderColumns.CreatedBy}
WHERE
    gf.{GroupFolderColumns.GroupId} = @GroupId
    AND gf.{GroupFolderColumns.ParentFolderId} IS NULL
    AND gf.{GroupFolderColumns.IsDeleted} = FALSE

ORDER BY Name;";
                }
                else
                {
                    query = $@"

SELECT
    gf.{GroupFolderColumns.FolderId} AS Id,
    gf.{GroupFolderColumns.FolderName} AS Name,
    TRUE AS IsFolder,
    'Folder' AS Type,
    gf.{GroupFolderColumns.FullPath} AS Path,
    gf.{GroupFolderColumns.Level} AS Level,
    NULL::BIGINT AS Size,
    u1.c_firstname || ' ' || u1.c_lastname AS CreatedBy,
    gf.{GroupFolderColumns.CreatedAt} AS CreatedAt
FROM {GroupFolderTable.TableName} gf
INNER JOIN t_users u1
    ON u1.c_userid = gf.{GroupFolderColumns.CreatedBy}
WHERE
    gf.{GroupFolderColumns.GroupId} = @GroupId
    AND gf.{GroupFolderColumns.ParentFolderId} = @ParentFolderId
    AND gf.{GroupFolderColumns.IsDeleted} = FALSE

UNION ALL

SELECT
    f.{GroupFileColumns.FileId} AS Id,
    f.{GroupFileColumns.FileName} AS Name,
    FALSE AS IsFolder,
    UPPER(REPLACE(f.{GroupFileColumns.Extension},'.','')) AS Type,
    fol.{GroupFolderColumns.FullPath} || '/' || f.{GroupFileColumns.FileName} AS Path,
    fol.{GroupFolderColumns.Level} AS Level,
    f.{GroupFileColumns.FileSize} AS Size,
    u2.c_firstname || ' ' || u2.c_lastname AS CreatedBy,
    f.{GroupFileColumns.CreatedAt} AS CreatedAt
FROM {GroupFileTable.TableName} f

INNER JOIN {GroupFolderTable.TableName} fol
    ON fol.{GroupFolderColumns.FolderId} = f.{GroupFileColumns.FolderId}

INNER JOIN t_users u2
    ON u2.c_userid = f.{GroupFileColumns.CreatedBy}

WHERE
    f.{GroupFileColumns.GroupId} = @GroupId
    AND f.{GroupFileColumns.FolderId} = @ParentFolderId
    AND f.{GroupFileColumns.IsDeleted} = FALSE

ORDER BY Name;";
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
                    contents.Add(new FolderContentVM
                    {
                        Id = reader.GetInt64(reader.GetOrdinal("Id")),

                        Name = reader.GetString(reader.GetOrdinal("Name")),

                        IsFolder = reader.GetBoolean(reader.GetOrdinal("IsFolder")),

                        Type = reader.GetString(reader.GetOrdinal("Type")),

                        Path = reader.GetString(reader.GetOrdinal("Path")),

                        Level = reader.GetInt32(reader.GetOrdinal("Level")),

                        Size = reader.IsDBNull(reader.GetOrdinal("Size"))
                            ? null
                            : reader.GetInt64(reader.GetOrdinal("Size")),

                        CreatedBy = reader.GetString(reader.GetOrdinal("CreatedBy")),

                        CreatedAt = reader.GetDateTime(reader.GetOrdinal("CreatedAt"))
                    });
                }

                await reader.CloseAsync();

                foreach (var item in contents)
                {
                    if (item.IsFolder)
                    {
                        item.Size = await GetFolderSizeAsync(item.Id);
                    }

                    item.SizeDisplay = FileSizeHelper.Format(item.Size);
                }

                return contents;


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
        public async Task<ServiceResult> UpdateGroupAsync(UpdateGroupVM model)
        {
            ServiceResult result = new();

            try
            {
                await _conn.OpenAsync();

                await using var transaction = await _conn.BeginTransactionAsync();

                #region Update Group

                string updateQuery = $@"
            UPDATE {GroupTable.TableName}
            SET
                {GroupColumns.GroupName}=@GroupName,
                {GroupColumns.Description}=@Description,
                {GroupColumns.UpdatedBy}=@UpdatedBy,
                {GroupColumns.UpdatedAt}=@UpdatedAt
            WHERE
                {GroupColumns.GroupId}=@GroupId
                AND {GroupColumns.IsDeleted}=FALSE;";

                await using (var cmd = new NpgsqlCommand(updateQuery, _conn, transaction))
                {
                    cmd.Parameters.AddWithValue("@GroupId", model.GroupId);
                    cmd.Parameters.AddWithValue("@GroupName", model.GroupName);
                    cmd.Parameters.AddWithValue("@Description",
                        (object?)model.Description ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@UpdatedBy", model.UpdatedBy);
                    cmd.Parameters.AddWithValue("@UpdatedAt", DateTime.UtcNow);

                    await cmd.ExecuteNonQueryAsync();
                }

                #endregion

                #region Remove Existing Roles

                string deleteRolesQuery = $@"
            DELETE FROM {GroupRoleTable.TableName}
            WHERE {GroupRoleColumns.GroupId}=@GroupId;";

                await using (var cmd = new NpgsqlCommand(deleteRolesQuery, _conn, transaction))
                {
                    cmd.Parameters.AddWithValue("@GroupId", model.GroupId);

                    await cmd.ExecuteNonQueryAsync();
                }

                #endregion

                #region Insert Roles

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

                    foreach (var roleId in model.SelectedRoleIds)
                    {
                        await using var cmd = new NpgsqlCommand(insertRoleQuery, _conn, transaction);

                        cmd.Parameters.AddWithValue("@GroupId", model.GroupId);
                        cmd.Parameters.AddWithValue("@RoleId", roleId);

                        await cmd.ExecuteNonQueryAsync();
                    }
                }

                #endregion

                await transaction.CommitAsync();

                result.Success = true;
                result.Message = "Group updated successfully.";

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating group.");

                result.Success = false;
                result.Message = "Failed to update group.";

                return result;
            }
            finally
            {
                if (_conn.State == ConnectionState.Open)
                    await _conn.CloseAsync();
            }
        }
        public async Task<bool> FolderExistsAsync(
            long groupId,
            long? parentFolderId,
            string folderName)
        {
            try
            {
                string query;

                if (parentFolderId == null)
                {
                    query = $@"
                SELECT COUNT(*)
                FROM {GroupFolderTable.TableName}
                WHERE
                    {GroupFolderColumns.GroupId}=@GroupId
                    AND {GroupFolderColumns.ParentFolderId} IS NULL
                    AND LOWER({GroupFolderColumns.FolderName})=LOWER(@FolderName)
                    AND {GroupFolderColumns.IsDeleted}=FALSE;";
                }
                else
                {
                    query = $@"
                SELECT COUNT(*)
                FROM {GroupFolderTable.TableName}
                WHERE
                    {GroupFolderColumns.GroupId}=@GroupId
                    AND {GroupFolderColumns.ParentFolderId}=@ParentFolderId
                    AND LOWER({GroupFolderColumns.FolderName})=LOWER(@FolderName)
                    AND {GroupFolderColumns.IsDeleted}=FALSE;";
                }

                await _conn.OpenAsync();

                await using var cmd = new NpgsqlCommand(query, _conn);

                cmd.Parameters.AddWithValue("@GroupId", groupId);

                cmd.Parameters.AddWithValue("@FolderName", folderName);

                if (parentFolderId.HasValue)
                    cmd.Parameters.AddWithValue("@ParentFolderId", parentFolderId.Value);

                int count = Convert.ToInt32(await cmd.ExecuteScalarAsync());

                return count > 0;
            }
            finally
            {
                if (_conn.State == ConnectionState.Open)
                    await _conn.CloseAsync();
            }
        }
        public async Task<ServiceResult> CreateFolderAsync(CreateFolderVM model)
        {
            ServiceResult result = new();

            try
            {
                await _conn.OpenAsync();

                await using var transaction = await _conn.BeginTransactionAsync();

                string fullPath;
                int level;

                if (model.ParentFolderId == null)
                {
                    fullPath = "/" + model.FolderName;
                    level = 0;
                }
                else
                {
                    string parentQuery = $@"
                SELECT
                    {GroupFolderColumns.FullPath},
                    {GroupFolderColumns.Level}
                FROM {GroupFolderTable.TableName}
                WHERE
                    {GroupFolderColumns.FolderId}=@FolderId
                    AND {GroupFolderColumns.IsDeleted}=FALSE;";

                    await using var parentCmd =
                        new NpgsqlCommand(parentQuery, _conn, transaction);

                    parentCmd.Parameters.AddWithValue(
                        "@FolderId",
                        model.ParentFolderId.Value);

                    await using var reader =
                        await parentCmd.ExecuteReaderAsync();

                    if (!await reader.ReadAsync())
                    {
                        result.Success = false;
                        result.Message = "Parent folder not found.";

                        return result;
                    }

                    string parentPath =
                        reader.GetString(reader.GetOrdinal(GroupFolderColumns.FullPath));

                    int parentLevel =
                        reader.GetInt32(reader.GetOrdinal(GroupFolderColumns.Level));

                    await reader.CloseAsync();

                    fullPath = parentPath + "/" + model.FolderName;

                    level = parentLevel + 1;
                }

                string insertFolder = $@"
            INSERT INTO {GroupFolderTable.TableName}
            (
                {GroupFolderColumns.GroupId},
                {GroupFolderColumns.ParentFolderId},
                {GroupFolderColumns.FolderName},
                {GroupFolderColumns.Description},
                {GroupFolderColumns.FullPath},
                {GroupFolderColumns.Level},
                {GroupFolderColumns.HasChildren},
                {GroupFolderColumns.DisplayOrder},
                {GroupFolderColumns.CreatedBy},
                {GroupFolderColumns.CreatedAt},
                {GroupFolderColumns.IsDeleted}
            )
            VALUES
            (
                @GroupId,
                @ParentFolderId,
                @FolderName,
                @Description,
                @FullPath,
                @Level,
                FALSE,
                0,
                @CreatedBy,
                @CreatedAt,
                FALSE
            )
            RETURNING {GroupFolderColumns.FolderId};";

                long folderId;

                await using (var cmd =
                    new NpgsqlCommand(insertFolder, _conn, transaction))
                {
                    cmd.Parameters.AddWithValue("@GroupId", model.GroupId);

                    cmd.Parameters.AddWithValue(
                        "@ParentFolderId",
                        (object?)model.ParentFolderId ?? DBNull.Value);

                    cmd.Parameters.AddWithValue("@FolderName", model.FolderName);

                    cmd.Parameters.AddWithValue(
                        "@Description",
                        (object?)model.Description ?? DBNull.Value);

                    cmd.Parameters.AddWithValue("@FullPath", fullPath);

                    cmd.Parameters.AddWithValue("@Level", level);

                    cmd.Parameters.AddWithValue("@CreatedBy", model.CreatedBy);

                    cmd.Parameters.AddWithValue("@CreatedAt", DateTime.UtcNow);

                    folderId = Convert.ToInt64(await cmd.ExecuteScalarAsync());
                }

                if (model.SelectedRoleIds.Any())
                {
                    string roleQuery = $@"
                INSERT INTO {GroupFolderRoleTable.TableName}
                (
                    {GroupFolderRoleColumns.FolderId},
                    {GroupFolderRoleColumns.RoleId}
                )
                VALUES
                (
                    @FolderId,
                    @RoleId
                );";

                    foreach (long roleId in model.SelectedRoleIds)
                    {
                        await using var roleCmd =
                            new NpgsqlCommand(roleQuery, _conn, transaction);

                        roleCmd.Parameters.AddWithValue("@FolderId", folderId);

                        roleCmd.Parameters.AddWithValue("@RoleId", roleId);

                        await roleCmd.ExecuteNonQueryAsync();
                    }
                }

                if (model.ParentFolderId.HasValue)
                {
                    string updateParent = $@"
                UPDATE {GroupFolderTable.TableName}
                SET {GroupFolderColumns.HasChildren}=TRUE
                WHERE {GroupFolderColumns.FolderId}=@FolderId;";

                    await using var updateCmd =
                        new NpgsqlCommand(updateParent, _conn, transaction);

                    updateCmd.Parameters.AddWithValue(
                        "@FolderId",
                        model.ParentFolderId.Value);

                    await updateCmd.ExecuteNonQueryAsync();
                }

                await transaction.CommitAsync();

                result.Success = true;
                result.Id = folderId;
                result.Message = "Folder created successfully.";

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating folder.");

                result.Success = false;
                result.Message = "Unable to create folder.";

                return result;
            }
            finally
            {
                if (_conn.State == ConnectionState.Open)
                    await _conn.CloseAsync();
            }
        }
        public async Task<FolderDetailsVM?> GetFolderByIdAsync(long folderId)
        {
            try
            {
                await _conn.OpenAsync();

                string query = $@"
SELECT

    f.{GroupFolderColumns.FolderId},
    f.{GroupFolderColumns.FolderName},
    f.{GroupFolderColumns.Description},
    f.{GroupFolderColumns.FullPath},
    f.{GroupFolderColumns.Level},
    f.{GroupFolderColumns.CreatedAt},
    f.{GroupFolderColumns.UpdatedAt},

    g.{GroupColumns.GroupName},

    COALESCE(parent.{GroupFolderColumns.FolderName}, 'Root') AS ParentFolder,

    creator.{UserColumns.FirstName} || ' ' ||
    creator.{UserColumns.LastName} AS CreatedBy,

    CASE
        WHEN updater.{UserColumns.UserId} IS NULL
        THEN NULL
        ELSE updater.{UserColumns.FirstName} || ' ' ||
             updater.{UserColumns.LastName}
    END AS UpdatedBy,

    (
        SELECT COUNT(*)
        FROM {GroupFolderTable.TableName} child
        WHERE
            child.{GroupFolderColumns.ParentFolderId}=f.{GroupFolderColumns.FolderId}
            AND child.{GroupFolderColumns.IsDeleted}=FALSE
    ) ChildFolderCount,

    (
        SELECT COUNT(*)
        FROM {GroupFileTable.TableName} gf
        WHERE
            gf.{GroupFileColumns.FolderId}=f.{GroupFolderColumns.FolderId}
            AND gf.{GroupFileColumns.IsDeleted}=FALSE
    ) FileCount,

    (
        WITH RECURSIVE FolderTree AS
        (
            SELECT
                {GroupFolderColumns.FolderId}
            FROM {GroupFolderTable.TableName}
            WHERE
                {GroupFolderColumns.FolderId}=f.{GroupFolderColumns.FolderId}

            UNION ALL

            SELECT
                child.{GroupFolderColumns.FolderId}
            FROM {GroupFolderTable.TableName} child
            INNER JOIN FolderTree ft
                ON ft.{GroupFolderColumns.FolderId}=child.{GroupFolderColumns.ParentFolderId}
            WHERE
                child.{GroupFolderColumns.IsDeleted}=FALSE
        )

        SELECT
            COALESCE(SUM(file.{GroupFileColumns.FileSize}),0)
        FROM {GroupFileTable.TableName} file
        WHERE
            file.{GroupFileColumns.FolderId} IN
            (
                SELECT {GroupFolderColumns.FolderId}
                FROM FolderTree
            )
            AND file.{GroupFileColumns.IsDeleted}=FALSE
    ) TotalSize

FROM {GroupFolderTable.TableName} f

INNER JOIN {GroupTable.TableName} g
    ON g.{GroupColumns.GroupId}=f.{GroupFolderColumns.GroupId}

LEFT JOIN {GroupFolderTable.TableName} parent
    ON parent.{GroupFolderColumns.FolderId}=f.{GroupFolderColumns.ParentFolderId}

INNER JOIN {UserTable.TableName} creator
    ON creator.{UserColumns.UserId}=f.{GroupFolderColumns.CreatedBy}

LEFT JOIN {UserTable.TableName} updater
    ON updater.{UserColumns.UserId}=f.{GroupFolderColumns.UpdatedBy}

WHERE

    f.{GroupFolderColumns.FolderId}=@FolderId

    AND

    f.{GroupFolderColumns.IsDeleted}=FALSE;";

                await using var cmd = new NpgsqlCommand(query, _conn);

                cmd.Parameters.AddWithValue("@FolderId", folderId);

                await using var reader = await cmd.ExecuteReaderAsync();

                if (!await reader.ReadAsync())
                    return null;

                FolderDetailsVM vm = new()
                {
                    FolderId = reader.GetInt64(reader.GetOrdinal(GroupFolderColumns.FolderId)),

                    FolderName = reader.GetString(reader.GetOrdinal(GroupFolderColumns.FolderName)),

                    Description = reader.IsDBNull(reader.GetOrdinal(GroupFolderColumns.Description))
                        ? string.Empty
                        : reader.GetString(reader.GetOrdinal(GroupFolderColumns.Description)),

                    Path = reader.GetString(reader.GetOrdinal(GroupFolderColumns.FullPath)),

                    Level = reader.GetInt32(reader.GetOrdinal(GroupFolderColumns.Level)),

                    GroupName = reader.GetString(reader.GetOrdinal(GroupColumns.GroupName)),

                    ParentFolder = reader.GetString(reader.GetOrdinal("ParentFolder")),

                    CreatedBy = reader.GetString(reader.GetOrdinal("CreatedBy")),

                    CreatedAt = reader.GetDateTime(reader.GetOrdinal(GroupFolderColumns.CreatedAt)),

                    UpdatedBy = reader.IsDBNull(reader.GetOrdinal("UpdatedBy"))
                        ? string.Empty
                        : reader.GetString(reader.GetOrdinal("UpdatedBy")),

                    UpdatedAt = reader.IsDBNull(reader.GetOrdinal(GroupFolderColumns.UpdatedAt))
                        ? null
                        : reader.GetDateTime(reader.GetOrdinal(GroupFolderColumns.UpdatedAt)),

                    ChildFolderCount = reader.GetInt32(reader.GetOrdinal("ChildFolderCount")),

                    FileCount = reader.GetInt32(reader.GetOrdinal("FileCount")),
                    TotalSize = reader.GetInt64(
    reader.GetOrdinal("TotalSize"))
                };
vm.TotalSizeDisplay =
    FileSizeHelper.Format(vm.TotalSize);
                await reader.CloseAsync();

                string roleQuery = $@"

            SELECT

                r.{RoleColumns.RoleId},
                r.{RoleColumns.RoleName},
                r.{RoleColumns.Description}

            FROM {GroupFolderRoleTable.TableName} fr

            INNER JOIN {RoleTable.TableName} r

                ON r.{RoleColumns.RoleId}=fr.{GroupFolderRoleColumns.RoleId}

            WHERE

                fr.{GroupFolderRoleColumns.FolderId}=@FolderId

            ORDER BY

                r.{RoleColumns.RoleName};";

                await using var roleCmd = new NpgsqlCommand(roleQuery, _conn);

                roleCmd.Parameters.AddWithValue("@FolderId", folderId);

                await using var roleReader = await roleCmd.ExecuteReaderAsync();

                while (await roleReader.ReadAsync())
                {
                    int roleId = roleReader.GetInt32(
                        roleReader.GetOrdinal(RoleColumns.RoleId));

                    vm.Roles.Add(new RoleVM
                    {
                        RoleId = roleId,

                        RoleName = roleReader.GetString(
                            roleReader.GetOrdinal(RoleColumns.RoleName)),

                        Description = roleReader.IsDBNull(
                            roleReader.GetOrdinal(RoleColumns.Description))
                                ? string.Empty
                                : roleReader.GetString(
                                    roleReader.GetOrdinal(RoleColumns.Description))
                    });

                    vm.SelectedRoleIds.Add(roleId);
                }

                return vm;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Error retrieving folder details.");

                throw;
            }
            finally
            {
                if (_conn.State == ConnectionState.Open)
                    await _conn.CloseAsync();
            }
        }

        public async Task<ServiceResult> UploadFileAsync(UploadFileVM model)
        {
            ServiceResult result = new();

            await _conn.OpenAsync();

            await using var transaction = await _conn.BeginTransactionAsync();

            try
            {
                string query = $@"
            INSERT INTO {GroupFileTable.TableName}
            (
                {GroupFileColumns.GroupId},
                {GroupFileColumns.FolderId},
                {GroupFileColumns.FileName},
                {GroupFileColumns.OriginalFileName},
                {GroupFileColumns.Extension},
                {GroupFileColumns.ContentType},
                {GroupFileColumns.FileCategory},
                {GroupFileColumns.FileSize},
                {GroupFileColumns.StorageProvider},
                {GroupFileColumns.ObjectKey},
                {GroupFileColumns.Description},
                {GroupFileColumns.CreatedBy}
            )
            VALUES
            (
                @GroupId,
                @FolderId,
                @FileName,
                @OriginalFileName,
                @Extension,
                @ContentType,
                @FileCategory,
                @FileSize,
                @StorageProvider,
                '',
                @Description,
                @CreatedBy
            )
            RETURNING {GroupFileColumns.FileId};";

                await using var cmd = new NpgsqlCommand(
                    query,
                    _conn,
                    transaction);

                cmd.Parameters.AddWithValue("@GroupId", model.GroupId);

                cmd.Parameters.AddWithValue("@FolderId", model.FolderId);

                cmd.Parameters.AddWithValue("@FileName", model.File.FileName);

                cmd.Parameters.AddWithValue("@OriginalFileName", model.File.FileName);

                cmd.Parameters.AddWithValue(
                    "@Extension",
                    Path.GetExtension(model.File.FileName)
                        .TrimStart('.')
                        .ToLower());

                cmd.Parameters.AddWithValue(
                    "@ContentType",
                    model.File.ContentType);

                cmd.Parameters.AddWithValue(
                    "@FileCategory",
                    GetFileCategory(model.File.FileName));

                cmd.Parameters.AddWithValue(
                    "@FileSize",
                    model.File.Length);

                cmd.Parameters.AddWithValue(
                    "@StorageProvider",
                    "Supabase");

                cmd.Parameters.AddWithValue(
                    "@Description",
                    (object?)model.Description ?? DBNull.Value);

                cmd.Parameters.AddWithValue(
                    "@CreatedBy",
                    model.CreatedBy);

                long fileId =
                    Convert.ToInt64(await cmd.ExecuteScalarAsync());

                if (model.SelectedRoleIds.Any())
                {
                    foreach (int roleId in model.SelectedRoleIds)
                    {
                        string roleQuery = $@"
                    INSERT INTO {GroupFileRoleTable.TableName}
                    (
                        {GroupFileRoleColumns.FileId},
                        {GroupFileRoleColumns.RoleId}
                    )
                    VALUES
                    (
                        @FileId,
                        @RoleId
                    );";

                        await using var roleCmd = new NpgsqlCommand(
                            roleQuery,
                            _conn,
                            transaction);

                        roleCmd.Parameters.AddWithValue(
                            "@FileId",
                            fileId);

                        roleCmd.Parameters.AddWithValue(
                            "@RoleId",
                            roleId);

                        await roleCmd.ExecuteNonQueryAsync();
                    }
                }

                await transaction.CommitAsync();

                result.Success = true;

                result.Id = fileId;

                result.Message = "File metadata created successfully.";

                _logger.LogInformation(
                    "File metadata created successfully. FileId: {FileId}",
                    fileId);

                return result;
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();

                _logger.LogError(
                    ex,
                    "Error uploading file metadata.");

                result.Success = false;

                result.Message = "Unable to upload file.";

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

        private static string GetFileCategory(string fileName)
        {
            string extension =
                Path.GetExtension(fileName)
                    .TrimStart('.')
                    .ToLower();

            return extension switch
            {
                "pdf" => "PDF",

                "doc" or "docx" => "Document",

                "xls" or "xlsx" => "Spreadsheet",

                "ppt" or "pptx" => "Presentation",

                "jpg" or "jpeg" or "png" or "gif" or "bmp" or "webp"
                    => "Image",

                "mp4" or "avi" or "mov" or "mkv"
                    => "Video",

                "mp3" or "wav"
                    => "Audio",

                "zip" or "rar" or "7z"
                    => "Archive",

                _ => "Other"
            };
        }

        public async Task UpdateObjectKeyAsync(
    long fileId,
    string objectKey)
        {
            try
            {
                await _conn.OpenAsync();

                string query = $@"
            UPDATE {GroupFileTable.TableName}
            SET
                {GroupFileColumns.ObjectKey} = @ObjectKey
            WHERE
                {GroupFileColumns.FileId} = @FileId;";

                await using var cmd = new NpgsqlCommand(query, _conn);

                cmd.Parameters.AddWithValue("@FileId", fileId);

                cmd.Parameters.AddWithValue("@ObjectKey", objectKey);

                await cmd.ExecuteNonQueryAsync();

                _logger.LogInformation(
                    "Updated object key for FileId {FileId}.",
                    fileId);
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Error updating object key for FileId {FileId}.",
                    fileId);

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

        public async Task<FileDetailsVM?> GetFileByIdAsync(long fileId)
        {
            try
            {
                await _conn.OpenAsync();

                FileDetailsVM? vm = null;

                string fileQuery = $@"
SELECT

    f.{GroupFileColumns.FileId},
    f.{GroupFileColumns.GroupId},
    f.{GroupFileColumns.FolderId},
    f.{GroupFileColumns.FileName},
    f.{GroupFileColumns.OriginalFileName},
    f.{GroupFileColumns.Description},
    f.{GroupFileColumns.Extension},
    f.{GroupFileColumns.ContentType},
    f.{GroupFileColumns.FileCategory},
    f.{GroupFileColumns.FileSize},
    f.{GroupFileColumns.CreatedAt},
    f.{GroupFileColumns.UpdatedAt},

    g.{GroupColumns.GroupName},

    fol.{GroupFolderColumns.FolderName},

    fol.{GroupFolderColumns.FullPath}
        || '/'
        || f.{GroupFileColumns.FileName}
        || f.{GroupFileColumns.Extension}
        AS Path,

    creator.{UserColumns.FirstName}
        || ' '
        || creator.{UserColumns.LastName}
        AS CreatedBy,

    CASE
        WHEN updater.{UserColumns.UserId} IS NULL
        THEN NULL
        ELSE
            updater.{UserColumns.FirstName}
            || ' '
            || updater.{UserColumns.LastName}
    END AS UpdatedBy

FROM {GroupFileTable.TableName} f

INNER JOIN {GroupTable.TableName} g
ON g.{GroupColumns.GroupId}
    = f.{GroupFileColumns.GroupId}

INNER JOIN {GroupFolderTable.TableName} fol
ON fol.{GroupFolderColumns.FolderId}
    = f.{GroupFileColumns.FolderId}

INNER JOIN {UserTable.TableName} creator
ON creator.{UserColumns.UserId}
    = f.{GroupFileColumns.CreatedBy}

LEFT JOIN {UserTable.TableName} updater
ON updater.{UserColumns.UserId}
    = f.{GroupFileColumns.UpdatedBy}

WHERE

    f.{GroupFileColumns.FileId}=@FileId

    AND

    f.{GroupFileColumns.IsDeleted}=FALSE;";

                await using (var cmd = new NpgsqlCommand(fileQuery, _conn))
                {
                    cmd.Parameters.AddWithValue("@FileId", fileId);

                    await using var reader = await cmd.ExecuteReaderAsync();

                    if (!await reader.ReadAsync())
                    {
                        return null;
                    }

                    vm = new FileDetailsVM
                    {
                        FileId = reader.GetInt64(reader.GetOrdinal(GroupFileColumns.FileId)),

                        GroupId = reader.GetInt64(reader.GetOrdinal(GroupFileColumns.GroupId)),

                        FolderId = reader.GetInt64(reader.GetOrdinal(GroupFileColumns.FolderId)),

                        FileName = reader.GetString(reader.GetOrdinal(GroupFileColumns.FileName)),

                        OriginalFileName = reader.GetString(reader.GetOrdinal(GroupFileColumns.OriginalFileName)),

                        Description = reader.IsDBNull(reader.GetOrdinal(GroupFileColumns.Description))
                            ? string.Empty
                            : reader.GetString(reader.GetOrdinal(GroupFileColumns.Description)),

                        Extension = reader.GetString(reader.GetOrdinal(GroupFileColumns.Extension)),

                        ContentType = reader.GetString(reader.GetOrdinal(GroupFileColumns.ContentType)),

                        FileCategory = reader.GetString(reader.GetOrdinal(GroupFileColumns.FileCategory)),

                        FileSize = reader.GetInt64(reader.GetOrdinal(GroupFileColumns.FileSize)),

                        CreatedBy = reader.GetString(reader.GetOrdinal("createdby")),

                        CreatedAt = reader.GetDateTime(reader.GetOrdinal(GroupFileColumns.CreatedAt)),

                        UpdatedBy = reader.IsDBNull(reader.GetOrdinal("updatedby"))
                            ? null
                            : reader.GetString(reader.GetOrdinal("updatedby")),

                        UpdatedAt = reader.IsDBNull(reader.GetOrdinal(GroupFileColumns.UpdatedAt))
                            ? null
                            : reader.GetDateTime(reader.GetOrdinal(GroupFileColumns.UpdatedAt)),

                        Path = reader.GetString(reader.GetOrdinal("path")),
                        GroupName = reader.GetString(
    reader.GetOrdinal(GroupColumns.GroupName)),

FolderName = reader.GetString(
    reader.GetOrdinal(GroupFolderColumns.FolderName)),
                    };
                }

                string roleQuery = $@"
            SELECT
                r.{RoleColumns.RoleId},
                r.{RoleColumns.RoleName},
                r.{RoleColumns.Description}
            FROM {GroupFileRoleTable.TableName} fr
            INNER JOIN {RoleTable.TableName} r
                ON r.{RoleColumns.RoleId}=fr.{GroupFileRoleColumns.RoleId}
            WHERE
                fr.{GroupFileRoleColumns.FileId}=@FileId
            ORDER BY
                r.{RoleColumns.RoleName};";

                await using (var roleCmd = new NpgsqlCommand(roleQuery, _conn))
                {
                    roleCmd.Parameters.AddWithValue("@FileId", fileId);

                    await using var roleReader = await roleCmd.ExecuteReaderAsync();

                    while (await roleReader.ReadAsync())
                    {
                        long roleId = roleReader.GetInt64(
                            roleReader.GetOrdinal(RoleColumns.RoleId));

                        vm!.Roles.Add(new RoleVM
                        {
                            RoleId = Convert.ToInt32(roleId),

                            RoleName = roleReader.GetString(
                                roleReader.GetOrdinal(RoleColumns.RoleName)),

                            Description = roleReader.IsDBNull(
                                roleReader.GetOrdinal(RoleColumns.Description))
                                    ? string.Empty
                                    : roleReader.GetString(
                                        roleReader.GetOrdinal(RoleColumns.Description))
                        });

                        vm.SelectedRoleIds.Add(roleId);
                    }
                }
vm.FileSizeDisplay =
    FileSizeHelper.Format(vm.FileSize);

vm.StorageProvider = "Supabase";
                return vm;
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Error retrieving file details for FileId {FileId}.",
                    fileId);

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

        public async Task<ServiceResult> UpdateFileAsync(UpdateFileVM model)
        {
            ServiceResult result = new();

            try
            {
                await _conn.OpenAsync();

                await using var transaction =
                    await _conn.BeginTransactionAsync();

                try
                {
                    string updateFileQuery = $@"
                UPDATE {GroupFileTable.TableName}
                SET
                    {GroupFileColumns.FileName}=@FileName,
                    {GroupFileColumns.Description}=@Description,
                    {GroupFileColumns.UpdatedBy}=@UpdatedBy,
                    {GroupFileColumns.UpdatedAt}=CURRENT_TIMESTAMP
                WHERE
                    {GroupFileColumns.FileId}=@FileId
                    AND {GroupFileColumns.IsDeleted}=FALSE;";

                    await using (var updateCmd =
                        new NpgsqlCommand(updateFileQuery, _conn, transaction))
                    {
                        updateCmd.Parameters.AddWithValue("@FileId", model.FileId);
                        updateCmd.Parameters.AddWithValue("@FileName", model.FileName);
                        updateCmd.Parameters.AddWithValue(
                            "@Description",
                            (object?)model.Description ?? DBNull.Value);
                        updateCmd.Parameters.AddWithValue("@UpdatedBy", model.UpdatedBy);

                        int rows = await updateCmd.ExecuteNonQueryAsync();

                        if (rows == 0)
                        {
                            await transaction.RollbackAsync();

                            result.Success = false;
                            result.Message = "File not found.";

                            return result;
                        }
                    }

                    string deleteRolesQuery = $@"
                DELETE
                FROM {GroupFileRoleTable.TableName}
                WHERE
                    {GroupFileRoleColumns.FileId}=@FileId;";

                    await using (var deleteCmd =
                        new NpgsqlCommand(deleteRolesQuery, _conn, transaction))
                    {
                        deleteCmd.Parameters.AddWithValue("@FileId", model.FileId);

                        await deleteCmd.ExecuteNonQueryAsync();
                    }

                    if (model.SelectedRoleIds.Any())
                    {
                        string insertRoleQuery = $@"
                    INSERT INTO {GroupFileRoleTable.TableName}
                    (
                        {GroupFileRoleColumns.FileId},
                        {GroupFileRoleColumns.RoleId}
                    )
                    VALUES
                    (
                        @FileId,
                        @RoleId
                    );";

                        foreach (long roleId in model.SelectedRoleIds)
                        {
                            await using var insertCmd =
                                new NpgsqlCommand(insertRoleQuery, _conn, transaction);

                            insertCmd.Parameters.AddWithValue("@FileId", model.FileId);
                            insertCmd.Parameters.AddWithValue("@RoleId", roleId);

                            await insertCmd.ExecuteNonQueryAsync();
                        }
                    }

                    await transaction.CommitAsync();

                    result.Success = true;
                    result.Id = model.FileId;
                    result.Message = "File updated successfully.";

                    return result;
                }
                catch
                {
                    await transaction.RollbackAsync();
                    throw;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Error updating file {FileId}.",
                    model.FileId);

                result.Success = false;
                result.Message = "Failed to update file.";

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

        public async Task DeleteFileAsync(long fileId)
        {
            try
            {
                await _conn.OpenAsync();

                string query = $@"
            UPDATE {GroupFileTable.TableName}
            SET
                {GroupFileColumns.IsDeleted} = TRUE,
                {GroupFileColumns.DeletedAt} = CURRENT_TIMESTAMP
            WHERE
                {GroupFileColumns.FileId} = @FileId;";

                await using var cmd = new NpgsqlCommand(query, _conn);

                cmd.Parameters.AddWithValue("@FileId", fileId);

                await cmd.ExecuteNonQueryAsync();

                _logger.LogInformation(
                    "Soft deleted file metadata for FileId {FileId}.",
                    fileId);
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Error deleting file metadata for FileId {FileId}.",
                    fileId);

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
        public async Task RollbackFileAsync(long fileId)
        {
            try
            {
                await _conn.OpenAsync();

                string query = $@"
            DELETE FROM {GroupFileTable.TableName}
            WHERE
                {GroupFileColumns.FileId} = @FileId;";

                await using var cmd = new NpgsqlCommand(query, _conn);

                cmd.Parameters.AddWithValue("@FileId", fileId);

                await cmd.ExecuteNonQueryAsync();

                _logger.LogInformation(
                    "Rolled back file metadata for FileId {FileId}.",
                    fileId);
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Error rolling back file metadata for FileId {FileId}.",
                    fileId);

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
        private async Task<long> GetFolderSizeAsync(long folderId)
        {
            const string query = @"
        WITH RECURSIVE FolderTree AS
        (
            SELECT c_folder_id
            FROM t_group_folders
            WHERE c_folder_id = @FolderId
              AND c_is_deleted = FALSE

            UNION ALL

            SELECT f.c_folder_id
            FROM t_group_folders f
            INNER JOIN FolderTree ft
                ON f.c_parent_folder_id = ft.c_folder_id
            WHERE f.c_is_deleted = FALSE
        )

        SELECT COALESCE(SUM(gf.c_file_size),0)
        FROM t_group_files gf
        WHERE gf.c_folder_id IN
        (
            SELECT c_folder_id
            FROM FolderTree
        )
        AND gf.c_is_deleted = FALSE;";

            await using var cmd = new NpgsqlCommand(query, _conn);

            cmd.Parameters.AddWithValue("@FolderId", folderId);

            object? result = await cmd.ExecuteScalarAsync();

            return Convert.ToInt64(result);
        }
        public async Task<ServiceResult> UpdateFolderAsync(UpdateFolderVM model)
        {
            ServiceResult result = new();

            try
            {
                bool exists = await FolderExistsAsync(
                    model.GroupId,
                    model.ParentFolderId,
                    model.FolderName,
                    model.FolderId);

                if (exists)
                {
                    result.Success = false;
                    result.Message = "A folder with the same name already exists.";

                    return result;
                }

                await _conn.OpenAsync();

                await using var transaction =
                    await _conn.BeginTransactionAsync();

                try
                {
                    string oldPathQuery = $@"
                SELECT
                    {GroupFolderColumns.FullPath}
                FROM {GroupFolderTable.TableName}
                WHERE
                    {GroupFolderColumns.FolderId}=@FolderId;";

                    string oldPath;

                    await using (var oldPathCmd =
                        new NpgsqlCommand(oldPathQuery, _conn, transaction))
                    {
                        oldPathCmd.Parameters.AddWithValue("@FolderId", model.FolderId);

                        oldPath = Convert.ToString(
                            await oldPathCmd.ExecuteScalarAsync())!;
                    }

                    string newPath;

                    if (model.ParentFolderId == null)
                    {
                        newPath = "/" + model.FolderName;
                    }
                    else
                    {
                        string parentPathQuery = $@"
                    SELECT
                        {GroupFolderColumns.FullPath}
                    FROM {GroupFolderTable.TableName}
                    WHERE
                        {GroupFolderColumns.FolderId}=@ParentFolderId;";

                        string parentPath;

                        await using (var parentCmd =
                            new NpgsqlCommand(parentPathQuery, _conn, transaction))
                        {
                            parentCmd.Parameters.AddWithValue(
                                "@ParentFolderId",
                                model.ParentFolderId.Value);

                            parentPath = Convert.ToString(
                                await parentCmd.ExecuteScalarAsync())!;
                        }

                        newPath = parentPath + "/" + model.FolderName;
                    }

                    string updateFolderQuery = $@"
                UPDATE {GroupFolderTable.TableName}
                SET
                    {GroupFolderColumns.FolderName}=@FolderName,
                    {GroupFolderColumns.Description}=@Description,
                    {GroupFolderColumns.FullPath}=@FullPath,
                    {GroupFolderColumns.UpdatedBy}=@UpdatedBy,
                    {GroupFolderColumns.UpdatedAt}=CURRENT_TIMESTAMP
                WHERE
                    {GroupFolderColumns.FolderId}=@FolderId
                    AND {GroupFolderColumns.IsDeleted}=FALSE;";

                    await using (var updateCmd =
                        new NpgsqlCommand(updateFolderQuery, _conn, transaction))
                    {
                        updateCmd.Parameters.AddWithValue("@FolderId", model.FolderId);
                        updateCmd.Parameters.AddWithValue("@FolderName", model.FolderName);
                        updateCmd.Parameters.AddWithValue("@Description",
                            (object?)model.Description ?? DBNull.Value);
                        updateCmd.Parameters.AddWithValue("@FullPath", newPath);
                        updateCmd.Parameters.AddWithValue("@UpdatedBy", model.UpdatedBy);

                        int rows = await updateCmd.ExecuteNonQueryAsync();

                        if (rows == 0)
                        {
                            await transaction.RollbackAsync();

                            result.Success = false;
                            result.Message = "Folder not found.";

                            return result;
                        }
                    }

                    string updateChildrenQuery = $@"
                UPDATE {GroupFolderTable.TableName}
                SET
                    {GroupFolderColumns.FullPath} =
                        REPLACE(
                            {GroupFolderColumns.FullPath},
                            @OldPath,
                            @NewPath)
                WHERE
                    {GroupFolderColumns.FullPath}
                    LIKE @OldPathLike;";

                    await using (var childCmd =
                        new NpgsqlCommand(updateChildrenQuery, _conn, transaction))
                    {
                        childCmd.Parameters.AddWithValue("@OldPath", oldPath);
                        childCmd.Parameters.AddWithValue("@NewPath", newPath);
                        childCmd.Parameters.AddWithValue("@OldPathLike", oldPath + "/%");

                        await childCmd.ExecuteNonQueryAsync();
                    }

                    string deleteRolesQuery = $@"
                DELETE
                FROM {GroupFolderRoleTable.TableName}
                WHERE
                    {GroupFolderRoleColumns.FolderId}=@FolderId;";

                    await using (var deleteCmd =
                        new NpgsqlCommand(deleteRolesQuery, _conn, transaction))
                    {
                        deleteCmd.Parameters.AddWithValue("@FolderId", model.FolderId);

                        await deleteCmd.ExecuteNonQueryAsync();
                    }

                    if (model.SelectedRoleIds.Any())
                    {
                        string insertQuery = $@"
                    INSERT INTO {GroupFolderRoleTable.TableName}
                    (
                        {GroupFolderRoleColumns.FolderId},
                        {GroupFolderRoleColumns.RoleId}
                    )
                    VALUES
                    (
                        @FolderId,
                        @RoleId
                    );";

                        foreach (long roleId in model.SelectedRoleIds)
                        {
                            await using var insertCmd =
                                new NpgsqlCommand(insertQuery, _conn, transaction);

                            insertCmd.Parameters.AddWithValue("@FolderId", model.FolderId);
                            insertCmd.Parameters.AddWithValue("@RoleId", roleId);

                            await insertCmd.ExecuteNonQueryAsync();
                        }
                    }

                    await transaction.CommitAsync();

                    result.Success = true;
                    result.Message = "Folder updated successfully.";

                    return result;
                }
                catch
                {
                    await transaction.RollbackAsync();
                    throw;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Error updating folder {FolderId}.",
                    model.FolderId);

                result.Success = false;
                result.Message = "Failed to update folder.";

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
        public async Task<bool> FolderExistsAsync(
            long groupId,
            long? parentFolderId,
            string folderName,
            long excludeFolderId)
        {
            try
            {
                string query;

                if (parentFolderId == null)
                {
                    query = $@"
                SELECT COUNT(*)
                FROM {GroupFolderTable.TableName}
                WHERE
                    {GroupFolderColumns.GroupId} = @GroupId
                    AND {GroupFolderColumns.ParentFolderId} IS NULL
                    AND LOWER({GroupFolderColumns.FolderName}) = LOWER(@FolderName)
                    AND {GroupFolderColumns.FolderId} <> @ExcludeFolderId
                    AND {GroupFolderColumns.IsDeleted} = FALSE;";
                }
                else
                {
                    query = $@"
                SELECT COUNT(*)
                FROM {GroupFolderTable.TableName}
                WHERE
                    {GroupFolderColumns.GroupId} = @GroupId
                    AND {GroupFolderColumns.ParentFolderId} = @ParentFolderId
                    AND LOWER({GroupFolderColumns.FolderName}) = LOWER(@FolderName)
                    AND {GroupFolderColumns.FolderId} <> @ExcludeFolderId
                    AND {GroupFolderColumns.IsDeleted} = FALSE;";
                }

                await _conn.OpenAsync();

                await using var cmd = new NpgsqlCommand(query, _conn);

                cmd.Parameters.AddWithValue("@GroupId", groupId);
                cmd.Parameters.AddWithValue("@FolderName", folderName);
                cmd.Parameters.AddWithValue("@ExcludeFolderId", excludeFolderId);

                if (parentFolderId.HasValue)
                {
                    cmd.Parameters.AddWithValue("@ParentFolderId", parentFolderId.Value);
                }

                long count = Convert.ToInt64(await cmd.ExecuteScalarAsync());

                return count > 0;
            }
            finally
            {
                if (_conn.State == ConnectionState.Open)
                {
                    await _conn.CloseAsync();
                }
            }
        }

        public async Task<List<MoveDestinationNodeVM>> GetMoveDestinationsAsync(
            string itemType,
            long itemId)
        {
            List<MoveDestinationNodeVM> nodes = new();

            try
            {
                string normalizedType = NormalizeMoveItemType(itemType);
                HashSet<long> disabledFolderIds = new();

                if (normalizedType == MoveConstants.ItemTypeFolder)
                {
                    disabledFolderIds = await GetFolderTreeIdsForMoveAsync(itemId);
                }

                await _conn.OpenAsync();

                string groupQuery = $@"
                    SELECT
                        {GroupColumns.GroupId},
                        {GroupColumns.GroupName}
                    FROM {GroupTable.TableName}
                    WHERE {GroupColumns.IsDeleted}=FALSE
                    ORDER BY {GroupColumns.GroupName};";

                await using (var groupCmd = new NpgsqlCommand(groupQuery, _conn))
                await using (var groupReader = await groupCmd.ExecuteReaderAsync())
                {
                    while (await groupReader.ReadAsync())
                    {
                        nodes.Add(new MoveDestinationNodeVM
                        {
                            Id = $"group_{groupReader.GetInt64(groupReader.GetOrdinal(GroupColumns.GroupId))}",
                            Text = groupReader.GetString(groupReader.GetOrdinal(GroupColumns.GroupName)),
                            Type = "group",
                            GroupId = groupReader.GetInt64(groupReader.GetOrdinal(GroupColumns.GroupId)),
                            FolderId = null,
                            Enabled = normalizedType == MoveConstants.ItemTypeFolder
                        });
                    }
                }

                string folderQuery = $@"
                    SELECT
                        {GroupFolderColumns.FolderId},
                        {GroupFolderColumns.GroupId},
                        {GroupFolderColumns.ParentFolderId},
                        {GroupFolderColumns.FolderName}
                    FROM {GroupFolderTable.TableName}
                    WHERE {GroupFolderColumns.IsDeleted}=FALSE
                    ORDER BY {GroupFolderColumns.Level}, {GroupFolderColumns.FolderName};";

                List<MoveDestinationNodeVM> folders = new();

                await using (var folderCmd = new NpgsqlCommand(folderQuery, _conn))
                await using (var folderReader = await folderCmd.ExecuteReaderAsync())
                {
                    while (await folderReader.ReadAsync())
                    {
                        long folderId = folderReader.GetInt64(folderReader.GetOrdinal(GroupFolderColumns.FolderId));

                        folders.Add(new MoveDestinationNodeVM
                        {
                            Id = $"folder_{folderId}",
                            Text = folderReader.GetString(folderReader.GetOrdinal(GroupFolderColumns.FolderName)),
                            Type = "folder",
                            GroupId = folderReader.GetInt64(folderReader.GetOrdinal(GroupFolderColumns.GroupId)),
                            FolderId = folderId,
                            Enabled = !disabledFolderIds.Contains(folderId)
                        });
                    }
                }

                Dictionary<long, MoveDestinationNodeVM> folderLookup = folders
                    .Where(folder => folder.FolderId.HasValue)
                    .ToDictionary(folder => folder.FolderId!.Value);

                foreach (MoveDestinationNodeVM folder in folders)
                {
                    long? parentId = await GetParentFolderIdFromOpenConnectionAsync(folder.FolderId!.Value);

                    if (parentId.HasValue && folderLookup.TryGetValue(parentId.Value, out MoveDestinationNodeVM? parent))
                    {
                        parent.Items.Add(folder);
                        continue;
                    }

                    MoveDestinationNodeVM? groupNode =
                        nodes.FirstOrDefault(group => group.GroupId == folder.GroupId);

                    groupNode?.Items.Add(folder);
                }

                return nodes;
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Error retrieving move destinations for {ItemType} {ItemId}.",
                    itemType,
                    itemId);

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

        public async Task<MovePermissionAnalysisVM> AnalyzeMovePermissionsAsync(
            MoveItemVM model)
        {
            MovePermissionAnalysisVM analysis = new();

            try
            {
                await _conn.OpenAsync();

                MoveSourceInfo source = await GetMoveSourceInfoAsync(
                    model.ItemType,
                    model.ItemId);

                MoveDestinationInfo destination =
                    await GetMoveDestinationInfoAsync(
                        model.DestinationGroupId,
                        model.DestinationFolderId);

                ServiceResult validation = await ValidateMoveAsync(
                    model,
                    source,
                    destination);

                if (!validation.Success)
                {
                    analysis.Success = false;
                    analysis.Message = validation.Message;

                    return analysis;
                }

                List<long> subtreeFolderIds = source.IsFolder
                    ? await GetFolderTreeIdsOpenAsync(model.ItemId)
                    : new List<long>();

                List<long> subtreeFileIds = source.IsFolder
                    ? await GetFileIdsOpenAsync(subtreeFolderIds)
                    : new List<long> { model.ItemId };

                List<long> currentRoleIds = source.IsFolder
                    ? await GetFolderTreeRoleIdsOpenAsync(subtreeFolderIds, subtreeFileIds)
                    : await GetFileRoleIdsOpenAsync(model.ItemId);

                List<long> destinationGroupRoleIds =
                    await GetGroupRoleIdsOpenAsync(model.DestinationGroupId);

                List<long> destinationRoleIds =
                    await GetDestinationRoleIdsOpenAsync(
                        model.DestinationGroupId,
                        model.DestinationFolderId);

                List<long> missingRoleIds = currentRoleIds
                    .Except(destinationGroupRoleIds)
                    .Distinct()
                    .ToList();

                List<long> validExistingRoleIds = currentRoleIds
                    .Intersect(destinationGroupRoleIds)
                    .Distinct()
                    .ToList();

                List<long> afterMoveRoleIds = NormalizeMovePermissionOption(model.PermissionOption) switch
                {
                    MoveConstants.PermissionInherit => destinationRoleIds,
                    MoveConstants.PermissionMerge => validExistingRoleIds.Union(destinationRoleIds).Distinct().ToList(),
                    MoveConstants.PermissionRemoveUnavailable => validExistingRoleIds,
                    _ => source.GroupId == model.DestinationGroupId
                        ? currentRoleIds
                        : validExistingRoleIds
                };

                analysis.Success = true;
                analysis.IsCrossGroup = source.GroupId != model.DestinationGroupId;
                analysis.ItemName = source.Name;
                analysis.SourcePath = source.Path;
                analysis.DestinationPath = destination.Path;
                analysis.FoldersAffected = source.IsFolder ? subtreeFolderIds.Count : 0;
                analysis.FilesAffected = subtreeFileIds.Count;
                analysis.CurrentRoles = await GetRolesByIdsOpenAsync(currentRoleIds);
                analysis.DestinationRoles = await GetRolesByIdsOpenAsync(destinationRoleIds);
                analysis.MissingRoles = await GetRolesByIdsOpenAsync(missingRoleIds);
                analysis.AfterMoveRoles = await GetRolesByIdsOpenAsync(afterMoveRoleIds);

                return analysis;
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Error analyzing move permissions for {ItemType} {ItemId}.",
                    model.ItemType,
                    model.ItemId);

                return new MovePermissionAnalysisVM
                {
                    Success = false,
                    Message = "Unable to analyze move permissions."
                };
            }
            finally
            {
                if (_conn.State == ConnectionState.Open)
                {
                    await _conn.CloseAsync();
                }
            }
        }

        public async Task<ServiceResult> MoveFileAsync(MoveItemVM model)
        {
            await _conn.OpenAsync();
            await using var transaction = await _conn.BeginTransactionAsync();

            try
            {
                await EnsureWorkspaceActivityLogAsync(transaction);

                MoveSourceInfo source = await GetMoveSourceInfoAsync(
                    MoveConstants.ItemTypeFile,
                    model.ItemId,
                    transaction);

                MoveDestinationInfo destination =
                    await GetMoveDestinationInfoAsync(
                        model.DestinationGroupId,
                        model.DestinationFolderId,
                        transaction);

                ServiceResult validation = await ValidateMoveAsync(
                    model,
                    source,
                    destination,
                    transaction);

                if (!validation.Success)
                {
                    await transaction.RollbackAsync();
                    return validation;
                }

                List<long> removedRoleIds =
                    await ApplyFileMovePermissionsAsync(
                        model,
                        source,
                        destination,
                        transaction);

                string updateQuery = $@"
                    UPDATE {GroupFileTable.TableName}
                    SET
                        {GroupFileColumns.GroupId}=@DestinationGroupId,
                        {GroupFileColumns.FolderId}=@DestinationFolderId,
                        {GroupFileColumns.UpdatedBy}=@UserId,
                        {GroupFileColumns.UpdatedAt}=CURRENT_TIMESTAMP
                    WHERE {GroupFileColumns.FileId}=@FileId;";

                await using (var cmd = new NpgsqlCommand(updateQuery, _conn, transaction))
                {
                    cmd.Parameters.AddWithValue("@DestinationGroupId", model.DestinationGroupId);
                    cmd.Parameters.AddWithValue("@DestinationFolderId", model.DestinationFolderId!.Value);
                    cmd.Parameters.AddWithValue("@UserId", model.UserId);
                    cmd.Parameters.AddWithValue("@FileId", model.ItemId);

                    await cmd.ExecuteNonQueryAsync();
                }

                await InsertMoveActivityAsync(
                    model,
                    source,
                    destination,
                    removedRoleIds,
                    transaction);

                await transaction.CommitAsync();

                return new ServiceResult
                {
                    Success = true,
                    Message = "File moved successfully."
                };
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();

                _logger.LogError(
                    ex,
                    "Error moving file {FileId}.",
                    model.ItemId);

                return new ServiceResult
                {
                    Success = false,
                    Message = "Unable to move file."
                };
            }
            finally
            {
                if (_conn.State == ConnectionState.Open)
                {
                    await _conn.CloseAsync();
                }
            }
        }

        public async Task<ServiceResult> MoveFolderAsync(MoveItemVM model)
        {
            await _conn.OpenAsync();
            await using var transaction = await _conn.BeginTransactionAsync();

            try
            {
                await EnsureWorkspaceActivityLogAsync(transaction);

                MoveSourceInfo source = await GetMoveSourceInfoAsync(
                    MoveConstants.ItemTypeFolder,
                    model.ItemId,
                    transaction);

                MoveDestinationInfo destination =
                    await GetMoveDestinationInfoAsync(
                        model.DestinationGroupId,
                        model.DestinationFolderId,
                        transaction);

                ServiceResult validation = await ValidateMoveAsync(
                    model,
                    source,
                    destination,
                    transaction);

                if (!validation.Success)
                {
                    await transaction.RollbackAsync();
                    return validation;
                }

                List<long> removedRoleIds =
                    await ApplyFolderMovePermissionsAsync(
                        model,
                        source,
                        destination,
                        transaction);

                string newRootPath = destination.FolderId.HasValue
                    ? $"{destination.Path}/{source.Name}"
                    : $"/{source.Name}";

                int newRootLevel = destination.FolderId.HasValue
                    ? destination.Level + 1
                    : 1;

                await UpdateMovedFolderTreeAsync(
                    model,
                    source,
                    newRootPath,
                    newRootLevel,
                    transaction);

                await InsertMoveActivityAsync(
                    model,
                    source,
                    destination,
                    removedRoleIds,
                    transaction);

                await transaction.CommitAsync();

                return new ServiceResult
                {
                    Success = true,
                    Message = "Folder moved successfully."
                };
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();

                _logger.LogError(
                    ex,
                    "Error moving folder {FolderId}.",
                    model.ItemId);

                return new ServiceResult
                {
                    Success = false,
                    Message = "Unable to move folder."
                };
            }
            finally
            {
                if (_conn.State == ConnectionState.Open)
                {
                    await _conn.CloseAsync();
                }
            }
        }

        private async Task<HashSet<long>> GetFolderTreeIdsForMoveAsync(long folderId)
        {
            try
            {
                await _conn.OpenAsync();
                return (await GetFolderTreeIdsOpenAsync(folderId)).ToHashSet();
            }
            finally
            {
                if (_conn.State == ConnectionState.Open)
                {
                    await _conn.CloseAsync();
                }
            }
        }

        private async Task<long?> GetParentFolderIdFromOpenConnectionAsync(long folderId)
        {
            string query = $@"
                SELECT {GroupFolderColumns.ParentFolderId}
                FROM {GroupFolderTable.TableName}
                WHERE {GroupFolderColumns.FolderId}=@FolderId;";

            await using var cmd = new NpgsqlCommand(query, _conn);
            cmd.Parameters.AddWithValue("@FolderId", folderId);

            object? value = await cmd.ExecuteScalarAsync();

            return value == null || value == DBNull.Value
                ? null
                : Convert.ToInt64(value);
        }

        private async Task<MoveSourceInfo> GetMoveSourceInfoAsync(
            string itemType,
            long itemId,
            NpgsqlTransaction? transaction = null)
        {
            string normalizedType = NormalizeMoveItemType(itemType);

            string query = normalizedType == MoveConstants.ItemTypeFolder
                ? $@"
                    SELECT
                        folder.{GroupFolderColumns.FolderId} AS Id,
                        folder.{GroupFolderColumns.GroupId} AS GroupId,
                        folder.{GroupFolderColumns.ParentFolderId} AS ParentFolderId,
                        folder.{GroupFolderColumns.FolderName} AS Name,
                        folder.{GroupFolderColumns.FullPath} AS Path,
                        folder.{GroupFolderColumns.Level} AS Level
                    FROM {GroupFolderTable.TableName} folder
                    WHERE
                        folder.{GroupFolderColumns.FolderId}=@ItemId
                        AND folder.{GroupFolderColumns.IsDeleted}=FALSE;"
                : $@"
                    SELECT
                        file.{GroupFileColumns.FileId} AS Id,
                        file.{GroupFileColumns.GroupId} AS GroupId,
                        file.{GroupFileColumns.FolderId} AS ParentFolderId,
                        file.{GroupFileColumns.FileName} AS Name,
                        folder.{GroupFolderColumns.FullPath} || '/' || file.{GroupFileColumns.FileName} AS Path,
                        folder.{GroupFolderColumns.Level} AS Level
                    FROM {GroupFileTable.TableName} file
                    INNER JOIN {GroupFolderTable.TableName} folder
                        ON folder.{GroupFolderColumns.FolderId}=file.{GroupFileColumns.FolderId}
                    WHERE
                        file.{GroupFileColumns.FileId}=@ItemId
                        AND file.{GroupFileColumns.IsDeleted}=FALSE;";

            await using var cmd = transaction is null
                ? new NpgsqlCommand(query, _conn)
                : new NpgsqlCommand(query, _conn, transaction);

            cmd.Parameters.AddWithValue("@ItemId", itemId);

            await using var reader = await cmd.ExecuteReaderAsync();

            if (!await reader.ReadAsync())
            {
                throw new InvalidOperationException("Source item was not found.");
            }

            return new MoveSourceInfo
            {
                Id = reader.GetInt64(reader.GetOrdinal("id")),
                GroupId = reader.GetInt64(reader.GetOrdinal("groupid")),
                ParentFolderId = reader.IsDBNull(reader.GetOrdinal("parentfolderid"))
                    ? null
                    : reader.GetInt64(reader.GetOrdinal("parentfolderid")),
                Name = reader.GetString(reader.GetOrdinal("name")),
                Path = reader.GetString(reader.GetOrdinal("path")),
                Level = reader.GetInt32(reader.GetOrdinal("level")),
                IsFolder = normalizedType == MoveConstants.ItemTypeFolder
            };
        }

        private async Task<MoveDestinationInfo> GetMoveDestinationInfoAsync(
            long destinationGroupId,
            long? destinationFolderId,
            NpgsqlTransaction? transaction = null)
        {
            if (!destinationFolderId.HasValue)
            {
                string groupQuery = $@"
                    SELECT {GroupColumns.GroupName}
                    FROM {GroupTable.TableName}
                    WHERE
                        {GroupColumns.GroupId}=@GroupId
                        AND {GroupColumns.IsDeleted}=FALSE;";

                await using var groupCmd = transaction is null
                    ? new NpgsqlCommand(groupQuery, _conn)
                    : new NpgsqlCommand(groupQuery, _conn, transaction);

                groupCmd.Parameters.AddWithValue("@GroupId", destinationGroupId);

                object? groupName = await groupCmd.ExecuteScalarAsync();

                if (groupName == null || groupName == DBNull.Value)
                {
                    throw new InvalidOperationException("Destination group was not found.");
                }

                return new MoveDestinationInfo
                {
                    GroupId = destinationGroupId,
                    FolderId = null,
                    Name = Convert.ToString(groupName) ?? string.Empty,
                    Path = "/" + Convert.ToString(groupName),
                    Level = 0
                };
            }

            string folderQuery = $@"
                SELECT
                    folder.{GroupFolderColumns.GroupId},
                    folder.{GroupFolderColumns.FolderName},
                    folder.{GroupFolderColumns.FullPath},
                    folder.{GroupFolderColumns.Level}
                FROM {GroupFolderTable.TableName} folder
                WHERE
                    folder.{GroupFolderColumns.FolderId}=@FolderId
                    AND folder.{GroupFolderColumns.GroupId}=@GroupId
                    AND folder.{GroupFolderColumns.IsDeleted}=FALSE;";

            await using var folderCmd = transaction is null
                ? new NpgsqlCommand(folderQuery, _conn)
                : new NpgsqlCommand(folderQuery, _conn, transaction);

            folderCmd.Parameters.AddWithValue("@FolderId", destinationFolderId.Value);
            folderCmd.Parameters.AddWithValue("@GroupId", destinationGroupId);

            await using var reader = await folderCmd.ExecuteReaderAsync();

            if (!await reader.ReadAsync())
            {
                throw new InvalidOperationException("Destination folder was not found.");
            }

            return new MoveDestinationInfo
            {
                GroupId = reader.GetInt64(reader.GetOrdinal(GroupFolderColumns.GroupId)),
                FolderId = destinationFolderId,
                Name = reader.GetString(reader.GetOrdinal(GroupFolderColumns.FolderName)),
                Path = reader.GetString(reader.GetOrdinal(GroupFolderColumns.FullPath)),
                Level = reader.GetInt32(reader.GetOrdinal(GroupFolderColumns.Level))
            };
        }

        private async Task<ServiceResult> ValidateMoveAsync(
            MoveItemVM model,
            MoveSourceInfo source,
            MoveDestinationInfo destination,
            NpgsqlTransaction? transaction = null)
        {
            if (source.IsFolder &&
                destination.FolderId.HasValue &&
                destination.FolderId.Value == source.Id)
            {
                return MoveValidationFailed("Cannot move folder into itself.");
            }

            if (!source.IsFolder && !destination.FolderId.HasValue)
            {
                return MoveValidationFailed(MoveConstants.FileRootMoveMessage);
            }

            if (source.IsFolder && destination.FolderId.HasValue)
            {
                bool destinationIsDescendant =
                    await IsFolderDescendantOpenAsync(
                        source.Id,
                        destination.FolderId.Value,
                        transaction);

                if (destinationIsDescendant)
                {
                    return MoveValidationFailed("Cannot move folder into its descendant.");
                }
            }

            if (!await HasRolePermissionAsync(
                model.ActiveRoleId,
                RolePermissionColumns.CanMove,
                transaction))
            {
                return MoveValidationFailed("You do not have permission to move this item.");
            }

            if (!await HasRolePermissionAsync(
                model.ActiveRoleId,
                RolePermissionColumns.CanCreate,
                transaction))
            {
                return MoveValidationFailed("You do not have permission to create items in the destination.");
            }

            bool duplicate = source.IsFolder
                ? await DestinationFolderNameExistsAsync(
                    destination.GroupId,
                    destination.FolderId,
                    source.Name,
                    source.Id,
                    transaction)
                : await DestinationFileNameExistsAsync(
                    destination.GroupId,
                    destination.FolderId!.Value,
                    source.Name,
                    source.Id,
                    transaction);

            if (duplicate)
            {
                return MoveValidationFailed(
                    source.IsFolder
                        ? MoveConstants.DuplicateFolderMessage
                        : MoveConstants.DuplicateFileMessage);
            }

            return new ServiceResult
            {
                Success = true
            };
        }

        private static ServiceResult MoveValidationFailed(string message)
        {
            return new ServiceResult
            {
                Success = false,
                Message = message
            };
        }

        private async Task<bool> HasRolePermissionAsync(
            int roleId,
            string permissionColumn,
            NpgsqlTransaction? transaction)
        {
            if (roleId <= 0)
            {
                return false;
            }

            string query = $@"
                SELECT COUNT(*)
                FROM {RolePermissionTable.TableName}
                WHERE
                    {RolePermissionColumns.RoleId}=@RoleId
                    AND {permissionColumn}=TRUE;";

            await using var cmd = transaction is null
                ? new NpgsqlCommand(query, _conn)
                : new NpgsqlCommand(query, _conn, transaction);

            cmd.Parameters.AddWithValue("@RoleId", roleId);

            return Convert.ToInt64(await cmd.ExecuteScalarAsync()) > 0;
        }

        private async Task<bool> DestinationFolderNameExistsAsync(
            long groupId,
            long? parentFolderId,
            string folderName,
            long excludeFolderId,
            NpgsqlTransaction? transaction)
        {
            string parentCondition = parentFolderId.HasValue
                ? $"{GroupFolderColumns.ParentFolderId}=@ParentFolderId"
                : $"{GroupFolderColumns.ParentFolderId} IS NULL";

            string query = $@"
                SELECT COUNT(*)
                FROM {GroupFolderTable.TableName}
                WHERE
                    {GroupFolderColumns.GroupId}=@GroupId
                    AND {parentCondition}
                    AND LOWER({GroupFolderColumns.FolderName})=LOWER(@FolderName)
                    AND {GroupFolderColumns.FolderId}<>@ExcludeFolderId
                    AND {GroupFolderColumns.IsDeleted}=FALSE;";

            await using var cmd = transaction is null
                ? new NpgsqlCommand(query, _conn)
                : new NpgsqlCommand(query, _conn, transaction);

            cmd.Parameters.AddWithValue("@GroupId", groupId);
            cmd.Parameters.AddWithValue("@FolderName", folderName);
            cmd.Parameters.AddWithValue("@ExcludeFolderId", excludeFolderId);

            if (parentFolderId.HasValue)
            {
                cmd.Parameters.AddWithValue("@ParentFolderId", parentFolderId.Value);
            }

            return Convert.ToInt64(await cmd.ExecuteScalarAsync()) > 0;
        }

        private async Task<bool> DestinationFileNameExistsAsync(
            long groupId,
            long folderId,
            string fileName,
            long excludeFileId,
            NpgsqlTransaction? transaction)
        {
            string query = $@"
                SELECT COUNT(*)
                FROM {GroupFileTable.TableName}
                WHERE
                    {GroupFileColumns.GroupId}=@GroupId
                    AND {GroupFileColumns.FolderId}=@FolderId
                    AND LOWER({GroupFileColumns.FileName})=LOWER(@FileName)
                    AND {GroupFileColumns.FileId}<>@ExcludeFileId
                    AND {GroupFileColumns.IsDeleted}=FALSE;";

            await using var cmd = transaction is null
                ? new NpgsqlCommand(query, _conn)
                : new NpgsqlCommand(query, _conn, transaction);

            cmd.Parameters.AddWithValue("@GroupId", groupId);
            cmd.Parameters.AddWithValue("@FolderId", folderId);
            cmd.Parameters.AddWithValue("@FileName", fileName);
            cmd.Parameters.AddWithValue("@ExcludeFileId", excludeFileId);

            return Convert.ToInt64(await cmd.ExecuteScalarAsync()) > 0;
        }

        private async Task<bool> IsFolderDescendantOpenAsync(
            long sourceFolderId,
            long destinationFolderId,
            NpgsqlTransaction? transaction)
        {
            string query = $@"
                WITH RECURSIVE FolderTree AS
                (
                    SELECT {GroupFolderColumns.FolderId}
                    FROM {GroupFolderTable.TableName}
                    WHERE {GroupFolderColumns.FolderId}=@SourceFolderId

                    UNION ALL

                    SELECT child.{GroupFolderColumns.FolderId}
                    FROM {GroupFolderTable.TableName} child
                    INNER JOIN FolderTree parent
                        ON child.{GroupFolderColumns.ParentFolderId}=parent.{GroupFolderColumns.FolderId}
                    WHERE child.{GroupFolderColumns.IsDeleted}=FALSE
                )
                SELECT COUNT(*)
                FROM FolderTree
                WHERE {GroupFolderColumns.FolderId}=@DestinationFolderId;";

            await using var cmd = transaction is null
                ? new NpgsqlCommand(query, _conn)
                : new NpgsqlCommand(query, _conn, transaction);

            cmd.Parameters.AddWithValue("@SourceFolderId", sourceFolderId);
            cmd.Parameters.AddWithValue("@DestinationFolderId", destinationFolderId);

            return Convert.ToInt64(await cmd.ExecuteScalarAsync()) > 0;
        }

        private async Task<List<long>> GetFolderTreeIdsOpenAsync(
            long folderId,
            NpgsqlTransaction? transaction = null)
        {
            string query = $@"
                WITH RECURSIVE FolderTree AS
                (
                    SELECT {GroupFolderColumns.FolderId}
                    FROM {GroupFolderTable.TableName}
                    WHERE
                        {GroupFolderColumns.FolderId}=@FolderId
                        AND {GroupFolderColumns.IsDeleted}=FALSE

                    UNION ALL

                    SELECT child.{GroupFolderColumns.FolderId}
                    FROM {GroupFolderTable.TableName} child
                    INNER JOIN FolderTree parent
                        ON child.{GroupFolderColumns.ParentFolderId}=parent.{GroupFolderColumns.FolderId}
                    WHERE child.{GroupFolderColumns.IsDeleted}=FALSE
                )
                SELECT {GroupFolderColumns.FolderId}
                FROM FolderTree;";

            await using var cmd = transaction is null
                ? new NpgsqlCommand(query, _conn)
                : new NpgsqlCommand(query, _conn, transaction);
            cmd.Parameters.AddWithValue("@FolderId", folderId);

            List<long> ids = new();

            await using var reader = await cmd.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                ids.Add(reader.GetInt64(0));
            }

            return ids;
        }

        private async Task<List<long>> GetFileIdsOpenAsync(
            List<long> folderIds,
            NpgsqlTransaction? transaction = null)
        {
            if (folderIds == null || !folderIds.Any())
            {
                return new List<long>();
            }

            string query = $@"
                SELECT {GroupFileColumns.FileId}
                FROM {GroupFileTable.TableName}
                WHERE
                    {GroupFileColumns.FolderId}=ANY(@FolderIds)
                    AND {GroupFileColumns.IsDeleted}=FALSE;";

            await using var cmd = transaction is null
                ? new NpgsqlCommand(query, _conn)
                : new NpgsqlCommand(query, _conn, transaction);
            cmd.Parameters.AddWithValue("@FolderIds", folderIds.ToArray());

            List<long> ids = new();

            await using var reader = await cmd.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                ids.Add(reader.GetInt64(0));
            }

            return ids;
        }

        private async Task<List<long>> GetGroupRoleIdsOpenAsync(
            long groupId,
            NpgsqlTransaction? transaction = null)
        {
            string query = $@"
                SELECT {GroupRoleColumns.RoleId}
                FROM {GroupRoleTable.TableName}
                WHERE {GroupRoleColumns.GroupId}=@GroupId;";

            await using var cmd = transaction is null
                ? new NpgsqlCommand(query, _conn)
                : new NpgsqlCommand(query, _conn, transaction);
            cmd.Parameters.AddWithValue("@GroupId", groupId);

            return await ReadLongListAsync(cmd);
        }

        private async Task<List<long>> GetDestinationRoleIdsOpenAsync(
            long groupId,
            long? folderId,
            NpgsqlTransaction? transaction = null)
        {
            if (!folderId.HasValue)
            {
                return await GetGroupRoleIdsOpenAsync(groupId, transaction);
            }

            string query = $@"
                SELECT {GroupFolderRoleColumns.RoleId}
                FROM {GroupFolderRoleTable.TableName}
                WHERE {GroupFolderRoleColumns.FolderId}=@FolderId;";

            await using var cmd = transaction is null
                ? new NpgsqlCommand(query, _conn)
                : new NpgsqlCommand(query, _conn, transaction);
            cmd.Parameters.AddWithValue("@FolderId", folderId.Value);

            List<long> folderRoles = await ReadLongListAsync(cmd);

            return folderRoles.Any()
                ? folderRoles
                : await GetGroupRoleIdsOpenAsync(groupId, transaction);
        }

        private async Task<List<long>> GetFileRoleIdsOpenAsync(
            long fileId,
            NpgsqlTransaction? transaction = null)
        {
            string query = $@"
                SELECT {GroupFileRoleColumns.RoleId}
                FROM {GroupFileRoleTable.TableName}
                WHERE {GroupFileRoleColumns.FileId}=@FileId;";

            await using var cmd = transaction is null
                ? new NpgsqlCommand(query, _conn)
                : new NpgsqlCommand(query, _conn, transaction);
            cmd.Parameters.AddWithValue("@FileId", fileId);

            return await ReadLongListAsync(cmd);
        }

        private async Task<List<long>> GetFolderTreeRoleIdsOpenAsync(
            List<long> folderIds,
            List<long> fileIds,
            NpgsqlTransaction? transaction = null)
        {
            List<long> roleIds = new();

            if (folderIds.Any())
            {
                string folderRoleQuery = $@"
                    SELECT DISTINCT {GroupFolderRoleColumns.RoleId}
                    FROM {GroupFolderRoleTable.TableName}
                    WHERE {GroupFolderRoleColumns.FolderId}=ANY(@FolderIds);";

                await using var folderCmd = transaction is null
                    ? new NpgsqlCommand(folderRoleQuery, _conn)
                    : new NpgsqlCommand(folderRoleQuery, _conn, transaction);
                folderCmd.Parameters.AddWithValue("@FolderIds", folderIds.ToArray());
                roleIds.AddRange(await ReadLongListAsync(folderCmd));
            }

            if (fileIds.Any())
            {
                string fileRoleQuery = $@"
                    SELECT DISTINCT {GroupFileRoleColumns.RoleId}
                    FROM {GroupFileRoleTable.TableName}
                    WHERE {GroupFileRoleColumns.FileId}=ANY(@FileIds);";

                await using var fileCmd = transaction is null
                    ? new NpgsqlCommand(fileRoleQuery, _conn)
                    : new NpgsqlCommand(fileRoleQuery, _conn, transaction);
                fileCmd.Parameters.AddWithValue("@FileIds", fileIds.ToArray());
                roleIds.AddRange(await ReadLongListAsync(fileCmd));
            }

            return roleIds.Distinct().ToList();
        }

        private async Task<List<RoleVM>> GetRolesByIdsOpenAsync(List<long> roleIds)
        {
            if (roleIds == null || !roleIds.Any())
            {
                return new List<RoleVM>();
            }

            string query = $@"
                SELECT
                    {RoleColumns.RoleId},
                    {RoleColumns.RoleName},
                    {RoleColumns.Description}
                FROM {RoleTable.TableName}
                WHERE {RoleColumns.RoleId}=ANY(@RoleIds)
                ORDER BY {RoleColumns.RoleName};";

            await using var cmd = new NpgsqlCommand(query, _conn);
            cmd.Parameters.AddWithValue("@RoleIds", roleIds.Select(Convert.ToInt32).ToArray());

            List<RoleVM> roles = new();

            await using var reader = await cmd.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                roles.Add(new RoleVM
                {
                    RoleId = reader.GetInt32(reader.GetOrdinal(RoleColumns.RoleId)),
                    RoleName = reader.GetString(reader.GetOrdinal(RoleColumns.RoleName)),
                    Description = reader.IsDBNull(reader.GetOrdinal(RoleColumns.Description))
                        ? string.Empty
                        : reader.GetString(reader.GetOrdinal(RoleColumns.Description))
                });
            }

            return roles;
        }

        private async Task<List<long>> ReadLongListAsync(NpgsqlCommand cmd)
        {
            List<long> ids = new();

            await using var reader = await cmd.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                ids.Add(Convert.ToInt64(reader.GetValue(0)));
            }

            return ids;
        }

        private async Task<List<long>> ApplyFileMovePermissionsAsync(
            MoveItemVM model,
            MoveSourceInfo source,
            MoveDestinationInfo destination,
            NpgsqlTransaction transaction)
        {
            List<long> destinationGroupRoleIds =
                await GetGroupRoleIdsOpenAsync(destination.GroupId, transaction);

            List<long> existingRoleIds =
                await GetFileRoleIdsOpenAsync(source.Id, transaction);

            List<long> removedRoleIds = existingRoleIds
                .Except(destinationGroupRoleIds)
                .Distinct()
                .ToList();

            string option = NormalizeMovePermissionOption(model.PermissionOption);

            if (source.GroupId == destination.GroupId &&
                option == MoveConstants.PermissionKeep)
            {
                return removedRoleIds;
            }

            List<long> destinationRoleIds =
                await GetDestinationRoleIdsOpenAsync(
                    destination.GroupId,
                    destination.FolderId,
                    transaction);

            List<long> validExisting = existingRoleIds
                .Intersect(destinationGroupRoleIds)
                .Distinct()
                .ToList();

            List<long> roleIds = option switch
            {
                MoveConstants.PermissionInherit => destinationRoleIds,
                MoveConstants.PermissionMerge => validExisting.Union(destinationRoleIds).Distinct().ToList(),
                _ => validExisting
            };

            await ReplaceFileRolesInTransactionAsync(
                source.Id,
                roleIds,
                transaction);

            return removedRoleIds;
        }

        private async Task<List<long>> ApplyFolderMovePermissionsAsync(
            MoveItemVM model,
            MoveSourceInfo source,
            MoveDestinationInfo destination,
            NpgsqlTransaction transaction)
        {
            List<long> folderIds = await GetFolderTreeIdsOpenAsync(source.Id, transaction);
            List<long> fileIds = await GetFileIdsOpenAsync(folderIds, transaction);
            List<long> destinationGroupRoleIds =
                await GetGroupRoleIdsOpenAsync(destination.GroupId, transaction);
            List<long> existingRoleIds =
                await GetFolderTreeRoleIdsOpenAsync(folderIds, fileIds, transaction);

            List<long> removedRoleIds = existingRoleIds
                .Except(destinationGroupRoleIds)
                .Distinct()
                .ToList();

            string option = NormalizeMovePermissionOption(model.PermissionOption);

            if (source.GroupId == destination.GroupId &&
                option == MoveConstants.PermissionKeep)
            {
                return removedRoleIds;
            }

            List<long> destinationRoleIds =
                await GetDestinationRoleIdsOpenAsync(
                    destination.GroupId,
                    destination.FolderId,
                    transaction);

            foreach (long folderId in folderIds)
            {
                List<long> roleIds = option switch
                {
                    MoveConstants.PermissionInherit => destinationRoleIds,
                    MoveConstants.PermissionMerge => (await GetFolderRoleIdsOpenAsync(folderId, transaction))
                        .Intersect(destinationGroupRoleIds)
                        .Union(destinationRoleIds)
                        .Distinct()
                        .ToList(),
                    _ => (await GetFolderRoleIdsOpenAsync(folderId, transaction))
                        .Intersect(destinationGroupRoleIds)
                        .Distinct()
                        .ToList()
                };

                await ReplaceFolderRolesInTransactionAsync(
                    folderId,
                    roleIds,
                    transaction);
            }

            foreach (long fileId in fileIds)
            {
                List<long> roleIds = option switch
                {
                    MoveConstants.PermissionInherit => destinationRoleIds,
                    MoveConstants.PermissionMerge => (await GetFileRoleIdsOpenAsync(fileId, transaction))
                        .Intersect(destinationGroupRoleIds)
                        .Union(destinationRoleIds)
                        .Distinct()
                        .ToList(),
                    _ => (await GetFileRoleIdsOpenAsync(fileId, transaction))
                        .Intersect(destinationGroupRoleIds)
                        .Distinct()
                        .ToList()
                };

                await ReplaceFileRolesInTransactionAsync(
                    fileId,
                    roleIds,
                    transaction);
            }

            return removedRoleIds;
        }

        private async Task<List<long>> GetFolderRoleIdsOpenAsync(
            long folderId,
            NpgsqlTransaction? transaction = null)
        {
            string query = $@"
                SELECT {GroupFolderRoleColumns.RoleId}
                FROM {GroupFolderRoleTable.TableName}
                WHERE {GroupFolderRoleColumns.FolderId}=@FolderId;";

            await using var cmd = transaction is null
                ? new NpgsqlCommand(query, _conn)
                : new NpgsqlCommand(query, _conn, transaction);
            cmd.Parameters.AddWithValue("@FolderId", folderId);

            return await ReadLongListAsync(cmd);
        }

        private async Task ReplaceFolderRolesInTransactionAsync(
            long folderId,
            List<long> roleIds,
            NpgsqlTransaction transaction)
        {
            await ExecuteMoveNonQueryAsync($@"
                DELETE FROM {GroupFolderRoleTable.TableName}
                WHERE {GroupFolderRoleColumns.FolderId}=@FolderId;",
                transaction,
                cmd => cmd.Parameters.AddWithValue("@FolderId", folderId));

            foreach (long roleId in roleIds.Distinct())
            {
                await ExecuteMoveNonQueryAsync($@"
                    INSERT INTO {GroupFolderRoleTable.TableName}
                    (
                        {GroupFolderRoleColumns.FolderId},
                        {GroupFolderRoleColumns.RoleId}
                    )
                    VALUES
                    (
                        @FolderId,
                        @RoleId
                    );",
                    transaction,
                    cmd =>
                    {
                        cmd.Parameters.AddWithValue("@FolderId", folderId);
                        cmd.Parameters.AddWithValue("@RoleId", roleId);
                    });
            }
        }

        private async Task ReplaceFileRolesInTransactionAsync(
            long fileId,
            List<long> roleIds,
            NpgsqlTransaction transaction)
        {
            await ExecuteMoveNonQueryAsync($@"
                DELETE FROM {GroupFileRoleTable.TableName}
                WHERE {GroupFileRoleColumns.FileId}=@FileId;",
                transaction,
                cmd => cmd.Parameters.AddWithValue("@FileId", fileId));

            foreach (long roleId in roleIds.Distinct())
            {
                await ExecuteMoveNonQueryAsync($@"
                    INSERT INTO {GroupFileRoleTable.TableName}
                    (
                        {GroupFileRoleColumns.FileId},
                        {GroupFileRoleColumns.RoleId}
                    )
                    VALUES
                    (
                        @FileId,
                        @RoleId
                    );",
                    transaction,
                    cmd =>
                    {
                        cmd.Parameters.AddWithValue("@FileId", fileId);
                        cmd.Parameters.AddWithValue("@RoleId", roleId);
                    });
            }
        }

        private async Task UpdateMovedFolderTreeAsync(
            MoveItemVM model,
            MoveSourceInfo source,
            string newRootPath,
            int newRootLevel,
            NpgsqlTransaction transaction)
        {
            string folderQuery = $@"
                WITH RECURSIVE FolderTree AS
                (
                    SELECT
                        {GroupFolderColumns.FolderId},
                        {GroupFolderColumns.FullPath},
                        {GroupFolderColumns.Level}
                    FROM {GroupFolderTable.TableName}
                    WHERE {GroupFolderColumns.FolderId}=@FolderId

                    UNION ALL

                    SELECT
                        child.{GroupFolderColumns.FolderId},
                        child.{GroupFolderColumns.FullPath},
                        child.{GroupFolderColumns.Level}
                    FROM {GroupFolderTable.TableName} child
                    INNER JOIN FolderTree parent
                        ON child.{GroupFolderColumns.ParentFolderId}=parent.{GroupFolderColumns.FolderId}
                    WHERE child.{GroupFolderColumns.IsDeleted}=FALSE
                )
                UPDATE {GroupFolderTable.TableName} folder
                SET
                    {GroupFolderColumns.GroupId}=@DestinationGroupId,
                    {GroupFolderColumns.ParentFolderId}=CASE
                        WHEN folder.{GroupFolderColumns.FolderId}=@FolderId
                        THEN @DestinationFolderId
                        ELSE folder.{GroupFolderColumns.ParentFolderId}
                    END,
                    {GroupFolderColumns.FullPath}=CASE
                        WHEN folder.{GroupFolderColumns.FolderId}=@FolderId
                        THEN @NewRootPath
                        ELSE @NewRootPath || SUBSTRING(folder.{GroupFolderColumns.FullPath} FROM LENGTH(@OldRootPath) + 1)
                    END,
                    {GroupFolderColumns.Level}=@NewRootLevel + (folder.{GroupFolderColumns.Level} - @OldRootLevel),
                    {GroupFolderColumns.UpdatedBy}=@UserId,
                    {GroupFolderColumns.UpdatedAt}=CURRENT_TIMESTAMP
                WHERE folder.{GroupFolderColumns.FolderId} IN
                (
                    SELECT {GroupFolderColumns.FolderId}
                    FROM FolderTree
                );";

            await using (var cmd = new NpgsqlCommand(folderQuery, _conn, transaction))
            {
                cmd.Parameters.AddWithValue("@FolderId", model.ItemId);
                cmd.Parameters.AddWithValue("@DestinationGroupId", model.DestinationGroupId);
                cmd.Parameters.AddWithValue("@DestinationFolderId", (object?)model.DestinationFolderId ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@NewRootPath", newRootPath);
                cmd.Parameters.AddWithValue("@OldRootPath", source.Path);
                cmd.Parameters.AddWithValue("@NewRootLevel", newRootLevel);
                cmd.Parameters.AddWithValue("@OldRootLevel", source.Level);
                cmd.Parameters.AddWithValue("@UserId", model.UserId);

                await cmd.ExecuteNonQueryAsync();
            }

            string fileQuery = $@"
                WITH RECURSIVE FolderTree AS
                (
                    SELECT {GroupFolderColumns.FolderId}
                    FROM {GroupFolderTable.TableName}
                    WHERE {GroupFolderColumns.FolderId}=@FolderId

                    UNION ALL

                    SELECT child.{GroupFolderColumns.FolderId}
                    FROM {GroupFolderTable.TableName} child
                    INNER JOIN FolderTree parent
                        ON child.{GroupFolderColumns.ParentFolderId}=parent.{GroupFolderColumns.FolderId}
                    WHERE child.{GroupFolderColumns.IsDeleted}=FALSE
                )
                UPDATE {GroupFileTable.TableName} file
                SET
                    {GroupFileColumns.GroupId}=@DestinationGroupId,
                    {GroupFileColumns.UpdatedBy}=@UserId,
                    {GroupFileColumns.UpdatedAt}=CURRENT_TIMESTAMP
                WHERE file.{GroupFileColumns.FolderId} IN
                (
                    SELECT {GroupFolderColumns.FolderId}
                    FROM FolderTree
                );";

            await using var fileCmd = new NpgsqlCommand(fileQuery, _conn, transaction);
            fileCmd.Parameters.AddWithValue("@FolderId", model.ItemId);
            fileCmd.Parameters.AddWithValue("@DestinationGroupId", model.DestinationGroupId);
            fileCmd.Parameters.AddWithValue("@UserId", model.UserId);

            await fileCmd.ExecuteNonQueryAsync();
        }

        private async Task InsertMoveActivityAsync(
            MoveItemVM model,
            MoveSourceInfo source,
            MoveDestinationInfo destination,
            List<long> removedRoleIds,
            NpgsqlTransaction transaction)
        {
            string query = $@"
                INSERT INTO {WorkspaceActivityTable.TableName}
                (
                    {WorkspaceActivityColumns.UserId},
                    {WorkspaceActivityColumns.ItemType},
                    {WorkspaceActivityColumns.ItemId},
                    {WorkspaceActivityColumns.Action},
                    {WorkspaceActivityColumns.SourcePath},
                    {WorkspaceActivityColumns.DestinationPath},
                    {WorkspaceActivityColumns.PermissionOption},
                    {WorkspaceActivityColumns.RolesRemoved}
                )
                VALUES
                (
                    @UserId,
                    @ItemType,
                    @ItemId,
                    @Action,
                    @SourcePath,
                    @DestinationPath,
                    @PermissionOption,
                    @RolesRemoved
                );";

            await using var cmd = new NpgsqlCommand(query, _conn, transaction);
            cmd.Parameters.AddWithValue("@UserId", model.UserId);
            cmd.Parameters.AddWithValue("@ItemType", source.IsFolder ? MoveConstants.ItemTypeFolder : MoveConstants.ItemTypeFile);
            cmd.Parameters.AddWithValue("@ItemId", source.Id);
            cmd.Parameters.AddWithValue("@Action", MoveConstants.ActionMoved);
            cmd.Parameters.AddWithValue("@SourcePath", source.Path);
            cmd.Parameters.AddWithValue("@DestinationPath", destination.Path);
            cmd.Parameters.AddWithValue("@PermissionOption", NormalizeMovePermissionOption(model.PermissionOption));
            cmd.Parameters.AddWithValue("@RolesRemoved", string.Join(",", removedRoleIds.Distinct()));

            await cmd.ExecuteNonQueryAsync();
        }

        private async Task EnsureWorkspaceActivityLogAsync(NpgsqlTransaction transaction)
        {
            string query = $@"
                CREATE TABLE IF NOT EXISTS {WorkspaceActivityTable.TableName}
                (
                    {WorkspaceActivityColumns.ActivityId} BIGSERIAL PRIMARY KEY,
                    {WorkspaceActivityColumns.UserId} BIGINT NOT NULL,
                    {WorkspaceActivityColumns.ItemType} VARCHAR(20) NOT NULL,
                    {WorkspaceActivityColumns.ItemId} BIGINT NOT NULL,
                    {WorkspaceActivityColumns.Action} VARCHAR(50) NOT NULL,
                    {WorkspaceActivityColumns.SourcePath} TEXT,
                    {WorkspaceActivityColumns.DestinationPath} TEXT,
                    {WorkspaceActivityColumns.PermissionOption} VARCHAR(50),
                    {WorkspaceActivityColumns.RolesRemoved} TEXT,
                    {WorkspaceActivityColumns.CreatedAt} TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP
                );";

            await ExecuteMoveNonQueryAsync(
                query,
                transaction,
                _ => { });
        }

        private async Task ExecuteMoveNonQueryAsync(
            string query,
            NpgsqlTransaction transaction,
            Action<NpgsqlCommand> configureCommand)
        {
            await using var cmd = new NpgsqlCommand(query, _conn, transaction);

            configureCommand(cmd);

            await cmd.ExecuteNonQueryAsync();
        }

        private static string NormalizeMoveItemType(string itemType)
        {
            return (itemType ?? string.Empty).Trim().ToLowerInvariant();
        }

        private static string NormalizeMovePermissionOption(string permissionOption)
        {
            string option = (permissionOption ?? string.Empty).Trim();

            return option switch
            {
                MoveConstants.PermissionInherit => MoveConstants.PermissionInherit,
                MoveConstants.PermissionRemoveUnavailable => MoveConstants.PermissionRemoveUnavailable,
                MoveConstants.PermissionMerge => MoveConstants.PermissionMerge,
                _ => MoveConstants.PermissionKeep
            };
        }

        private class MoveSourceInfo
        {
            public long Id { get; set; }

            public long GroupId { get; set; }

            public long? ParentFolderId { get; set; }

            public string Name { get; set; } = string.Empty;

            public string Path { get; set; } = string.Empty;

            public int Level { get; set; }

            public bool IsFolder { get; set; }
        }

        private class MoveDestinationInfo
        {
            public long GroupId { get; set; }

            public long? FolderId { get; set; }

            public string Name { get; set; } = string.Empty;

            public string Path { get; set; } = string.Empty;

            public int Level { get; set; }
        }
    }
}
