using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Npgsql;
using Repositories.Constants.RolePermissions;
using Repositories.Interfaces;
using Repositories.Models;

namespace Repositories.Implementations
{
    public class RolePermissionRepository : IRolePermissionInterface
    {
        private readonly NpgsqlConnection _conn;
    private readonly ILogger<RolePermissionRepository> _logger;

    public RolePermissionRepository(
        NpgsqlConnection conn,
        ILogger<RolePermissionRepository> logger)
    {
        _conn = conn;
        _logger = logger;
    }
    public async Task<List<RolePermission>> GetPermissionsByRoleAsync(int roleId)
{
    List<RolePermission> permissions = new();

    try
    {
        string query = $@"
            SELECT
                {RolePermissionColumns.RolePermissionId},
                {RolePermissionColumns.RoleId},
                {RolePermissionColumns.PageId},
                {RolePermissionColumns.CanView},
                {RolePermissionColumns.CanOpen},
                {RolePermissionColumns.CanCreate},
                {RolePermissionColumns.CanEdit},
                {RolePermissionColumns.CanDelete},
                {RolePermissionColumns.CanRestore},
                {RolePermissionColumns.CanUpload},
                {RolePermissionColumns.CanDownload},
                {RolePermissionColumns.CanMove},
                {RolePermissionColumns.CanCopy},
                {RolePermissionColumns.CanRename},
                {RolePermissionColumns.CanExport}
            FROM {RolePermissionTable.TableName}
            WHERE {RolePermissionColumns.RoleId} = @RoleId;";

        await _conn.OpenAsync();

        await using var cmd = new NpgsqlCommand(query, _conn);

        cmd.Parameters.AddWithValue("@RoleId", roleId);

        await using var reader = await cmd.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            permissions.Add(MapPermission(reader));
        }

        return permissions;
    }
    catch (Exception ex)
    {
        _logger.LogError(ex,
            "Error retrieving permissions for RoleId {RoleId}",
            roleId);

        throw;
    }
    finally
    {
        if (_conn.State == System.Data.ConnectionState.Open)
            await _conn.CloseAsync();
    }
}
public async Task<RolePermission?> GetPermissionAsync(int roleId, int pageId)
{
    try
    {
        string query = $@"
            SELECT
                {RolePermissionColumns.RolePermissionId},
                {RolePermissionColumns.RoleId},
                {RolePermissionColumns.PageId},
                {RolePermissionColumns.CanView},
                {RolePermissionColumns.CanOpen},
                {RolePermissionColumns.CanCreate},
                {RolePermissionColumns.CanEdit},
                {RolePermissionColumns.CanDelete},
                {RolePermissionColumns.CanRestore},
                {RolePermissionColumns.CanUpload},
                {RolePermissionColumns.CanDownload},
                {RolePermissionColumns.CanMove},
                {RolePermissionColumns.CanCopy},
                {RolePermissionColumns.CanRename},
                {RolePermissionColumns.CanExport}
            FROM {RolePermissionTable.TableName}
            WHERE
                {RolePermissionColumns.RoleId} = @RoleId
                AND
                {RolePermissionColumns.PageId} = @PageId
            LIMIT 1;";

        await _conn.OpenAsync();

        await using var cmd = new NpgsqlCommand(query, _conn);

        cmd.Parameters.AddWithValue("@RoleId", roleId);
        cmd.Parameters.AddWithValue("@PageId", pageId);

        await using var reader = await cmd.ExecuteReaderAsync();

        if (!await reader.ReadAsync())
            return null;

        return MapPermission(reader);
    }
    catch (Exception ex)
    {
        _logger.LogError(ex,
            "Error retrieving permission for RoleId {RoleId} and PageId {PageId}",
            roleId,
            pageId);

        throw;
    }
    finally
    {
        if (_conn.State == System.Data.ConnectionState.Open)
            await _conn.CloseAsync();
    }
}
private static RolePermission MapPermission(NpgsqlDataReader reader)
{
    return new RolePermission
    {
        RolePermissionId = reader.GetInt32(reader.GetOrdinal(RolePermissionColumns.RolePermissionId)),
        RoleId = reader.GetInt32(reader.GetOrdinal(RolePermissionColumns.RoleId)),
        PageId = reader.GetInt32(reader.GetOrdinal(RolePermissionColumns.PageId)),
        CanView = reader.GetBoolean(reader.GetOrdinal(RolePermissionColumns.CanView)),
        CanOpen = reader.GetBoolean(reader.GetOrdinal(RolePermissionColumns.CanOpen)),
        CanCreate = reader.GetBoolean(reader.GetOrdinal(RolePermissionColumns.CanCreate)),
        CanEdit = reader.GetBoolean(reader.GetOrdinal(RolePermissionColumns.CanEdit)),
        CanDelete = reader.GetBoolean(reader.GetOrdinal(RolePermissionColumns.CanDelete)),
        CanRestore = reader.GetBoolean(reader.GetOrdinal(RolePermissionColumns.CanRestore)),
        CanUpload = reader.GetBoolean(reader.GetOrdinal(RolePermissionColumns.CanUpload)),
        CanDownload = reader.GetBoolean(reader.GetOrdinal(RolePermissionColumns.CanDownload)),
        CanMove = reader.GetBoolean(reader.GetOrdinal(RolePermissionColumns.CanMove)),
        CanCopy = reader.GetBoolean(reader.GetOrdinal(RolePermissionColumns.CanCopy)),
        CanRename = reader.GetBoolean(reader.GetOrdinal(RolePermissionColumns.CanRename)),
        CanExport = reader.GetBoolean(reader.GetOrdinal(RolePermissionColumns.CanExport))
    };
}
    }
}