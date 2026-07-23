using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Npgsql;
using Repositories.Constants.Workspace.Files;
using Repositories.Constants.Workspace.Folders;
using Repositories.Constants.Workspace.Permissions;
using Repositories.Interfaces;

namespace Repositories.Implementations
{
    public class PermissionRepository : IPermissionInterface
    {
        private readonly NpgsqlConnection _conn;

        private readonly ILogger<PermissionRepository> _logger;

        public PermissionRepository(
            NpgsqlConnection conn,
            ILogger<PermissionRepository> logger)
        {
            _conn = conn;

            _logger = logger;
        }

        public async Task<List<long>> GetParentFolderIdsAsync(long folderId)
{
    List<long> parentFolderIds = new();

    try
    {
        string query = $@"
            WITH RECURSIVE ParentFolders AS
            (
                SELECT
                    {GroupFolderColumns.FolderId},
                    {GroupFolderColumns.ParentFolderId}
                FROM {GroupFolderTable.TableName}
                WHERE
                    {GroupFolderColumns.FolderId} = @FolderId
                    AND {GroupFolderColumns.IsDeleted} = FALSE

                UNION ALL

                SELECT
                    f.{GroupFolderColumns.FolderId},
                    f.{GroupFolderColumns.ParentFolderId}
                FROM {GroupFolderTable.TableName} f
                INNER JOIN ParentFolders pf
                    ON f.{GroupFolderColumns.FolderId} = pf.{GroupFolderColumns.ParentFolderId}
                WHERE
                    f.{GroupFolderColumns.IsDeleted} = FALSE
            )

            SELECT
                {GroupFolderColumns.FolderId}
            FROM ParentFolders
            WHERE
                {GroupFolderColumns.FolderId} <> @FolderId;";

        await _conn.OpenAsync();

        await using var cmd = new NpgsqlCommand(query, _conn);

        cmd.Parameters.AddWithValue("@FolderId", folderId);

        await using var reader = await cmd.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            parentFolderIds.Add(
                reader.GetInt64(
                    reader.GetOrdinal(GroupFolderColumns.FolderId)));
        }

        return parentFolderIds;
    }
    catch (Exception ex)
    {
        _logger.LogError(
            ex,
            "Error retrieving parent folders for FolderId {FolderId}.",
            folderId);

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

        public async Task<List<long>> GetChildFolderIdsAsync(long folderId)
{
    List<long> folderIds = new();

    try
    {
        string query = $@"
            WITH RECURSIVE FolderTree AS
            (
                SELECT
                    {GroupFolderColumns.FolderId}
                FROM {GroupFolderTable.TableName}
                WHERE
                    {GroupFolderColumns.FolderId} = @FolderId
                    AND {GroupFolderColumns.IsDeleted} = FALSE

                UNION ALL

                SELECT
                    child.{GroupFolderColumns.FolderId}
                FROM {GroupFolderTable.TableName} child

                INNER JOIN FolderTree parent
                    ON parent.{GroupFolderColumns.FolderId} =
                       child.{GroupFolderColumns.ParentFolderId}

                WHERE
                    child.{GroupFolderColumns.IsDeleted} = FALSE
            )

            SELECT
                {GroupFolderColumns.FolderId}
            FROM FolderTree;";

        await _conn.OpenAsync();

        await using var cmd = new NpgsqlCommand(query, _conn);

        cmd.Parameters.AddWithValue("@FolderId", folderId);

        await using var reader = await cmd.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            folderIds.Add(
                reader.GetInt64(
                    reader.GetOrdinal(GroupFolderColumns.FolderId)));
        }

        return folderIds;
    }
    catch (Exception ex)
    {
        _logger.LogError(
            ex,
            "Error retrieving child folders for FolderId {FolderId}.",
            folderId);

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

        public async Task<List<long>> GetFileIdsAsync(List<long> folderIds)
{
    List<long> fileIds = new();

    if (folderIds == null || !folderIds.Any())
        return fileIds;

    try
    {
        string query = $@"
            SELECT
                {GroupFileColumns.FileId}
            FROM {GroupFileTable.TableName}
            WHERE
                {GroupFileColumns.FolderId} = ANY(@FolderIds)
                AND {GroupFileColumns.IsDeleted} = FALSE;";

        await _conn.OpenAsync();

        await using var cmd = new NpgsqlCommand(query, _conn);

        cmd.Parameters.AddWithValue("@FolderIds", folderIds.ToArray());

        await using var reader = await cmd.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            fileIds.Add(
                reader.GetInt64(
                    reader.GetOrdinal(GroupFileColumns.FileId)));
        }

        return fileIds;
    }
    catch (Exception ex)
    {
        _logger.LogError(
            ex,
            "Error retrieving files for folders.");

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

        public async Task<long> GetGroupIdByFolderIdAsync(long folderId)
{
    bool shouldCloseConnection = false;

    try
    {
        if (_conn.State != ConnectionState.Open)
        {
            await _conn.OpenAsync();
            shouldCloseConnection = true;
        }

        string query = $@"
            SELECT
                {GroupFolderColumns.GroupId}
            FROM {GroupFolderTable.TableName}
            WHERE
                {GroupFolderColumns.FolderId} = @FolderId
                AND {GroupFolderColumns.IsDeleted} = FALSE;";

        await using var cmd = new NpgsqlCommand(query, _conn);

        cmd.Parameters.AddWithValue("@FolderId", folderId);

        object? result = await cmd.ExecuteScalarAsync();

        if (result == null || result == DBNull.Value)
            throw new Exception("Folder not found.");

        return Convert.ToInt64(result);
    }
    catch (Exception ex)
    {
        _logger.LogError(ex,
            "Error getting GroupId for FolderId {FolderId}.",
            folderId);

        throw;
    }
    finally
    {
        if (shouldCloseConnection &&
            _conn.State == ConnectionState.Open)
        {
            await _conn.CloseAsync();
        }
    }
}

        public async Task<List<long>> GetGroupRoleIdsAsync(long groupId)
{
    List<long> roleIds = new();
    bool shouldCloseConnection = false;

    try
    {
        if (_conn.State != ConnectionState.Open)
        {
            await _conn.OpenAsync();
            shouldCloseConnection = true;
        }

        string query = $@"
            SELECT
                {GroupRoleColumns.RoleId}
            FROM {GroupRoleTable.TableName}
            WHERE
                {GroupRoleColumns.GroupId} = @GroupId;";

        await using var cmd = new NpgsqlCommand(query, _conn);

        cmd.Parameters.AddWithValue("@GroupId", groupId);

        await using var reader = await cmd.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            roleIds.Add(
                reader.GetInt64(
                    reader.GetOrdinal(GroupRoleColumns.RoleId)));
        }

        return roleIds;
    }
    catch (Exception ex)
    {
        _logger.LogError(
            ex,
            "Error getting group roles for GroupId {GroupId}.",
            groupId);

        throw;
    }
    finally
    {
        if (shouldCloseConnection &&
            _conn.State == ConnectionState.Open)
        {
            await _conn.CloseAsync();
        }
    }
}

        public async Task<List<long>> GetFolderRoleIdsAsync(long folderId)
{
    List<long> roleIds = new();
    bool shouldCloseConnection = false;

    try
    {
        if (_conn.State != ConnectionState.Open)
        {
            await _conn.OpenAsync();
            shouldCloseConnection = true;
        }

        string query = $@"
            SELECT
                {GroupFolderRoleColumns.RoleId}
            FROM {GroupFolderRoleTable.TableName}
            WHERE
                {GroupFolderRoleColumns.FolderId} = @FolderId;";

        await using var cmd = new NpgsqlCommand(query, _conn);

        cmd.Parameters.AddWithValue("@FolderId", folderId);

        await using var reader = await cmd.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            roleIds.Add(
                reader.GetInt64(
                    reader.GetOrdinal(GroupFolderRoleColumns.RoleId)));
        }

        return roleIds;
    }
    catch (Exception ex)
    {
        _logger.LogError(ex,
            "Error getting folder roles for FolderId {FolderId}.",
            folderId);

        throw;
    }
    finally
    {
        if (shouldCloseConnection &&
            _conn.State == ConnectionState.Open)
        {
            await _conn.CloseAsync();
        }
    }
}

        public async Task ReplaceFolderRolesAsync(
    long folderId,
    List<long> roleIds)
{
    bool shouldCloseConnection = false;

    try
    {
        if (_conn.State != ConnectionState.Open)
        {
            await _conn.OpenAsync();
            shouldCloseConnection = true;
        }

        string deleteQuery = $@"
            DELETE FROM {GroupFolderRoleTable.TableName}
            WHERE
                {GroupFolderRoleColumns.FolderId}=@FolderId;";

        await using (var deleteCmd = new NpgsqlCommand(deleteQuery, _conn))
        {
            deleteCmd.Parameters.AddWithValue("@FolderId", folderId);

            await deleteCmd.ExecuteNonQueryAsync();
        }

        if (roleIds == null || !roleIds.Any())
        {
            return;
        }

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

        foreach (long roleId in roleIds)
        {
            await using var insertCmd =
                new NpgsqlCommand(insertQuery, _conn);

            insertCmd.Parameters.AddWithValue("@FolderId", folderId);
            insertCmd.Parameters.AddWithValue("@RoleId", roleId);

            await insertCmd.ExecuteNonQueryAsync();
        }
    }
    catch (Exception ex)
    {
        _logger.LogError(
            ex,
            "Error replacing roles for FolderId {FolderId}.",
            folderId);

        throw;
    }
    finally
    {
        if (shouldCloseConnection &&
            _conn.State == ConnectionState.Open)
        {
            await _conn.CloseAsync();
        }
    }
}

        public async Task ReplaceFileRolesAsync(
    long fileId,
    List<long> roleIds)
{
    bool shouldCloseConnection = false;

    try
    {
        if (_conn.State != ConnectionState.Open)
        {
            await _conn.OpenAsync();
            shouldCloseConnection = true;
        }

        string deleteQuery = $@"
            DELETE FROM {GroupFileRoleTable.TableName}
            WHERE
                {GroupFileRoleColumns.FileId} = @FileId;";

        await using (var deleteCmd = new NpgsqlCommand(deleteQuery, _conn))
        {
            deleteCmd.Parameters.AddWithValue("@FileId", fileId);

            await deleteCmd.ExecuteNonQueryAsync();
        }

        if (roleIds == null || !roleIds.Any())
        {
            return;
        }

        string insertQuery = $@"
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

        foreach (long roleId in roleIds)
        {
            await using var insertCmd =
                new NpgsqlCommand(insertQuery, _conn);

            insertCmd.Parameters.AddWithValue("@FileId", fileId);
            insertCmd.Parameters.AddWithValue("@RoleId", roleId);

            await insertCmd.ExecuteNonQueryAsync();
        }
    }
    catch (Exception ex)
    {
        _logger.LogError(
            ex,
            "Error replacing roles for FileId {FileId}.",
            fileId);

        throw;
    }
    finally
    {
        if (shouldCloseConnection &&
            _conn.State == ConnectionState.Open)
        {
            await _conn.CloseAsync();
        }
    }
}

        public async Task EnsureGroupContainsRolesAsync(
    long groupId,
    List<long> roleIds)
{
    bool shouldCloseConnection = false;

    try
    {
        if (roleIds == null || !roleIds.Any())
            return;

        if (_conn.State != ConnectionState.Open)
        {
            await _conn.OpenAsync();
            shouldCloseConnection = true;
        }

        string existsQuery = $@"
            SELECT COUNT(*)
            FROM {GroupRoleTable.TableName}
            WHERE
                {GroupRoleColumns.GroupId} = @GroupId
                AND {GroupRoleColumns.RoleId} = @RoleId;";

        string insertQuery = $@"
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

        foreach (long roleId in roleIds)
        {
            bool exists;

            await using (var existsCmd = new NpgsqlCommand(existsQuery, _conn))
            {
                existsCmd.Parameters.AddWithValue("@GroupId", groupId);
                existsCmd.Parameters.AddWithValue("@RoleId", roleId);

                exists = Convert.ToInt32(
                    await existsCmd.ExecuteScalarAsync()) > 0;
            }

            if (exists)
                continue;

            await using var insertCmd =
                new NpgsqlCommand(insertQuery, _conn);

            insertCmd.Parameters.AddWithValue("@GroupId", groupId);
            insertCmd.Parameters.AddWithValue("@RoleId", roleId);

            await insertCmd.ExecuteNonQueryAsync();
        }
    }
    catch (Exception ex)
    {
        _logger.LogError(
            ex,
            "Error synchronizing roles for GroupId {GroupId}.",
            groupId);

        throw;
    }
    finally
    {
        if (shouldCloseConnection &&
            _conn.State == ConnectionState.Open)
        {
            await _conn.CloseAsync();
        }
    }
}

        public async Task RemoveUnusedGroupRolesAsync(long groupId)
{
    bool shouldCloseConnection = false;

    try
    {
        if (_conn.State != ConnectionState.Open)
        {
            await _conn.OpenAsync();
            shouldCloseConnection = true;
        }

        string query = $@"
DELETE FROM {GroupRoleTable.TableName} gr

WHERE
    gr.{GroupRoleColumns.GroupId} = @GroupId

    AND NOT EXISTS
    (
        SELECT 1

        FROM {GroupFolderRoleTable.TableName} fr

        INNER JOIN {GroupFolderTable.TableName} f

            ON f.{GroupFolderColumns.FolderId} =
               fr.{GroupFolderRoleColumns.FolderId}

        WHERE

            f.{GroupFolderColumns.GroupId} = gr.{GroupRoleColumns.GroupId}

            AND

            fr.{GroupFolderRoleColumns.RoleId} =
                gr.{GroupRoleColumns.RoleId}
    )

    AND NOT EXISTS
    (
        SELECT 1

        FROM {GroupFileRoleTable.TableName} fr

        INNER JOIN {GroupFileTable.TableName} gf

            ON gf.{GroupFileColumns.FileId} =
               fr.{GroupFileRoleColumns.FileId}

        WHERE

            gf.{GroupFileColumns.GroupId} =
                gr.{GroupRoleColumns.GroupId}

            AND

            fr.{GroupFileRoleColumns.RoleId} =
                gr.{GroupRoleColumns.RoleId}
    );";

        await using var cmd = new NpgsqlCommand(query, _conn);

        cmd.Parameters.AddWithValue("@GroupId", groupId);

        await cmd.ExecuteNonQueryAsync();
    }
    catch (Exception ex)
    {
        _logger.LogError(ex,
            "Error removing unused roles from GroupId {GroupId}.",
            groupId);

        throw;
    }
    finally
    {
        if (shouldCloseConnection &&
            _conn.State == ConnectionState.Open)
        {
            await _conn.CloseAsync();
        }
    }
}

        public async Task RemoveRolesFromGroupFoldersAsync(
    long groupId,
    List<long> roleIds)
{
    bool shouldCloseConnection = false;

    try
    {
        if (roleIds == null || !roleIds.Any())
        {
            return;
        }

        if (_conn.State != ConnectionState.Open)
        {
            await _conn.OpenAsync();
            shouldCloseConnection = true;
        }

        string query = $@"
            DELETE FROM {GroupFolderRoleTable.TableName} fr
            USING {GroupFolderTable.TableName} gf
            WHERE
                fr.{GroupFolderRoleColumns.FolderId} =
                    gf.{GroupFolderColumns.FolderId}
                AND gf.{GroupFolderColumns.GroupId} = @GroupId
                AND gf.{GroupFolderColumns.IsDeleted} = FALSE
                AND fr.{GroupFolderRoleColumns.RoleId} = ANY(@RoleIds);";

        await using var cmd = new NpgsqlCommand(query, _conn);

        cmd.Parameters.AddWithValue("@GroupId", groupId);
        cmd.Parameters.AddWithValue("@RoleIds", roleIds.ToArray());

        await cmd.ExecuteNonQueryAsync();
    }
    catch (Exception ex)
    {
        _logger.LogError(
            ex,
            "Error removing roles from folders for GroupId {GroupId}.",
            groupId);

        throw;
    }
    finally
    {
        if (shouldCloseConnection &&
            _conn.State == ConnectionState.Open)
        {
            await _conn.CloseAsync();
        }
    }
}

        public async Task RemoveRolesFromGroupFilesAsync(
    long groupId,
    List<long> roleIds)
{
    bool shouldCloseConnection = false;

    try
    {
        if (roleIds == null || !roleIds.Any())
        {
            return;
        }

        if (_conn.State != ConnectionState.Open)
        {
            await _conn.OpenAsync();
            shouldCloseConnection = true;
        }

        string query = $@"
            DELETE FROM {GroupFileRoleTable.TableName} fr
            USING {GroupFileTable.TableName} gf
            WHERE
                fr.{GroupFileRoleColumns.FileId} =
                    gf.{GroupFileColumns.FileId}
                AND gf.{GroupFileColumns.GroupId} = @GroupId
                AND gf.{GroupFileColumns.IsDeleted} = FALSE
                AND fr.{GroupFileRoleColumns.RoleId} = ANY(@RoleIds);";

        await using var cmd = new NpgsqlCommand(query, _conn);

        cmd.Parameters.AddWithValue("@GroupId", groupId);
        cmd.Parameters.AddWithValue("@RoleIds", roleIds.ToArray());

        await cmd.ExecuteNonQueryAsync();
    }
    catch (Exception ex)
    {
        _logger.LogError(
            ex,
            "Error removing roles from files for GroupId {GroupId}.",
            groupId);

        throw;
    }
    finally
    {
        if (shouldCloseConnection &&
            _conn.State == ConnectionState.Open)
        {
            await _conn.CloseAsync();
        }
    }
}
    }
}
