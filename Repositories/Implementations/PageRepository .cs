using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Npgsql;
using Repositories.Constants.Pages;
using Repositories.Constants.RolePermissions;
using Repositories.Interfaces;
using Repositories.Models.ViewModels;

namespace Repositories.Implementations
{
    public class PageRepository : IPageInterface
    {
        private readonly NpgsqlConnection _conn;
    private readonly ILogger<PageRepository> _logger;

    public PageRepository(
        NpgsqlConnection conn,
        ILogger<PageRepository> logger)
    {
        _conn = conn;
        _logger = logger;
    }
    public async Task<List<PageVM>> GetAllPagesAsync()
{
    List<PageVM> pages = new();

    try
    {
        string query = $@"
            SELECT
                {PageColumns.PageId},
                {PageColumns.PageName},
                {PageColumns.Route},
                {PageColumns.Icon},
                {PageColumns.ParentPageId},
                {PageColumns.DisplayOrder},
                {PageColumns.IsMenu},
                {PageColumns.IsActive}
            FROM {PageTable.TableName}
            ORDER BY {PageColumns.DisplayOrder};";

        await _conn.OpenAsync();

        await using var cmd = new NpgsqlCommand(query, _conn);

        await using var reader = await cmd.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            pages.Add(MapPage(reader));
        }

        return pages;
    }
    finally
    {
        if (_conn.State == ConnectionState.Open)
            await _conn.CloseAsync();
    }
}
public async Task<PageVM?> GetPageByIdAsync(int pageId)
{
    try
    {
        string query = $@"
            SELECT
                {PageColumns.PageId},
                {PageColumns.PageName},
                {PageColumns.Route},
                {PageColumns.Icon},
                {PageColumns.ParentPageId},
                {PageColumns.DisplayOrder},
                {PageColumns.IsMenu},
                {PageColumns.IsActive}
            FROM {PageTable.TableName}
            WHERE {PageColumns.PageId}=@PageId
            LIMIT 1;";

        await _conn.OpenAsync();

        await using var cmd = new NpgsqlCommand(query, _conn);

        cmd.Parameters.AddWithValue("@PageId", pageId);

        await using var reader = await cmd.ExecuteReaderAsync();

        if (!await reader.ReadAsync())
            return null;

        return MapPage(reader);
    }
    finally
    {
        if (_conn.State == ConnectionState.Open)
            await _conn.CloseAsync();
    }
}
public async Task<List<PageVM>> GetMenuPagesAsync()
{
    List<PageVM> pages = new();

    try
    {
        string query = $@"
            SELECT
                {PageColumns.PageId},
                {PageColumns.PageName},
                {PageColumns.Route},
                {PageColumns.Icon},
                {PageColumns.ParentPageId},
                {PageColumns.DisplayOrder},
                {PageColumns.IsMenu},
                {PageColumns.IsActive}
            FROM {PageTable.TableName}
            WHERE
                {PageColumns.IsMenu}=TRUE
                AND {PageColumns.IsActive}=TRUE
            ORDER BY {PageColumns.DisplayOrder};";

        await _conn.OpenAsync();

        await using var cmd = new NpgsqlCommand(query, _conn);

        await using var reader = await cmd.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            pages.Add(MapPage(reader));
        }

        return pages;
    }
    finally
    {
        if (_conn.State == ConnectionState.Open)
            await _conn.CloseAsync();
    }
}
public async Task<List<PageVM>> GetPagesByRoleAsync(int roleId)
{
    List<PageVM> pages = new();

    try
    {
        string query = $@"
            SELECT
                p.{PageColumns.PageId},
                p.{PageColumns.PageName},
                p.{PageColumns.Route},
                p.{PageColumns.Icon},
                p.{PageColumns.ParentPageId},
                p.{PageColumns.DisplayOrder},
                p.{PageColumns.IsMenu},
                p.{PageColumns.IsActive}
            FROM {PageTable.TableName} p
            INNER JOIN {RolePermissionTable.TableName} rp
                ON rp.{RolePermissionColumns.PageId}=p.{PageColumns.PageId}
            WHERE
                rp.{RolePermissionColumns.RoleId}=@RoleId
                AND rp.{RolePermissionColumns.CanView}=TRUE
                AND p.{PageColumns.IsMenu}=TRUE
                AND p.{PageColumns.IsActive}=TRUE
            ORDER BY p.{PageColumns.DisplayOrder};";

        await _conn.OpenAsync();

        await using var cmd = new NpgsqlCommand(query, _conn);

        cmd.Parameters.AddWithValue("@RoleId", roleId);

        await using var reader = await cmd.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            pages.Add(MapPage(reader));
        }

        return pages;
    }
    finally
    {
        if (_conn.State == ConnectionState.Open)
            await _conn.CloseAsync();
    }
}
private static PageVM MapPage(NpgsqlDataReader reader)
{
    return new PageVM
    {
        PageId = reader.GetInt32(reader.GetOrdinal(PageColumns.PageId)),
        PageName = reader.GetString(reader.GetOrdinal(PageColumns.PageName)),
        Route = reader.GetString(reader.GetOrdinal(PageColumns.Route)),
        Icon = reader.IsDBNull(reader.GetOrdinal(PageColumns.Icon))
            ? string.Empty
            : reader.GetString(reader.GetOrdinal(PageColumns.Icon)),
        ParentPageId = reader.IsDBNull(reader.GetOrdinal(PageColumns.ParentPageId))
            ? null
            : reader.GetInt32(reader.GetOrdinal(PageColumns.ParentPageId)),
        DisplayOrder = reader.GetInt32(reader.GetOrdinal(PageColumns.DisplayOrder)),
        IsMenu = reader.GetBoolean(reader.GetOrdinal(PageColumns.IsMenu)),
        IsActive = reader.GetBoolean(reader.GetOrdinal(PageColumns.IsActive))
    };
}
    }
}