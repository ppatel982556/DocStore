using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Npgsql;
using Repositories.Constants.Roles;
using Repositories.Constants.UserRoles;
using Repositories.Constants.Users;
using Repositories.Interfaces;
using Repositories.Models;
using Repositories.Models.ViewModels;

namespace Repositories.Implementations
{
    public class RoleRepository : IRoleInterface
    {
        private readonly NpgsqlConnection _conn;
    private readonly ILogger<RoleRepository> _logger;

    public RoleRepository(
        NpgsqlConnection conn,
        ILogger<RoleRepository> logger)
    {
        _conn = conn;
        _logger = logger;
    }
        public async Task<List<RoleVM>> GetAllRolesAsync()
{
    List<RoleVM> roles = new();

    try
    {
        string query = $@"
            SELECT
                {RoleColumns.RoleId},
                {RoleColumns.RoleName},
                {RoleColumns.Description}
            FROM {RoleTable.TableName}
            WHERE {RoleColumns.IsActive} = TRUE
            ORDER BY {RoleColumns.RoleName};";

        await _conn.OpenAsync();

        await using var cmd = new NpgsqlCommand(query, _conn);

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
    catch (Exception ex)
    {
        _logger.LogError(ex, "Error retrieving roles.");
        throw;
    }
    finally
    {
        if (_conn.State == ConnectionState.Open)
            await _conn.CloseAsync();
    }
}

public async Task<RoleVM?> GetRoleByIdAsync(int roleId)
{
    try
    {
        string query = $@"
            SELECT
                {RoleColumns.RoleId},
                {RoleColumns.RoleName},
                {RoleColumns.Description}
            FROM {RoleTable.TableName}
            WHERE {RoleColumns.RoleId} = @RoleId
            LIMIT 1;";

        await _conn.OpenAsync();

        await using var cmd = new NpgsqlCommand(query, _conn);

        cmd.Parameters.AddWithValue("@RoleId", roleId);

        await using var reader = await cmd.ExecuteReaderAsync();

        if (!await reader.ReadAsync())
            return null;

        return new RoleVM
        {
            RoleId = reader.GetInt32(reader.GetOrdinal(RoleColumns.RoleId)),
            RoleName = reader.GetString(reader.GetOrdinal(RoleColumns.RoleName)),
            Description = reader.IsDBNull(reader.GetOrdinal(RoleColumns.Description))
                ? string.Empty
                : reader.GetString(reader.GetOrdinal(RoleColumns.Description))
        };
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Error retrieving role {RoleId}", roleId);
        throw;
    }
    finally
    {
        if (_conn.State == ConnectionState.Open)
            await _conn.CloseAsync();
    }
}
public async Task<List<RoleVM>> GetUserRolesAsync(int userId)
{
    List<RoleVM> roles = new();

    try
    {
        string query = $@"
            SELECT
                r.{RoleColumns.RoleId},
                r.{RoleColumns.RoleName},
                r.{RoleColumns.Description}
            FROM {RoleTable.TableName} r
            INNER JOIN {UserRoleTable.TableName} ur
                ON ur.{UserRoleColumns.RoleId}=r.{RoleColumns.RoleId}
            WHERE
                ur.{UserRoleColumns.UserId}=@UserId
                AND r.{RoleColumns.IsActive}=TRUE
            ORDER BY r.{RoleColumns.RoleName};";

        await _conn.OpenAsync();

        await using var cmd = new NpgsqlCommand(query, _conn);

        cmd.Parameters.AddWithValue("@UserId", userId);

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
    catch (Exception ex)
    {
        _logger.LogError(ex,
            "Error retrieving roles for UserId {UserId}",
            userId);

        throw;
    }
    finally
    {
        if (_conn.State == ConnectionState.Open)
            await _conn.CloseAsync();
    }
}
public async Task<ServiceResult> SwitchActiveRoleAsync(int userId, int roleId)
{
    ServiceResult result = new();

    try
    {
        await _conn.OpenAsync();

        // Check if the role belongs to the user
        string checkQuery = $@"
            SELECT COUNT(*)
            FROM {UserRoleTable.TableName}
            WHERE
                {UserRoleColumns.UserId} = @UserId
                AND {UserRoleColumns.RoleId} = @RoleId;";

        await using (var checkCmd = new NpgsqlCommand(checkQuery, _conn))
        {
            checkCmd.Parameters.AddWithValue("@UserId", userId);
            checkCmd.Parameters.AddWithValue("@RoleId", roleId);

            int exists = Convert.ToInt32(await checkCmd.ExecuteScalarAsync());

            if (exists == 0)
            {
                result.Success = false;
                result.Message = "Role is not assigned to the user.";

                return result;
            }
        }

        string updateQuery = $@"
            UPDATE {UserTable.TableName}
            SET {UserColumns.LastActiveRoleId}=@RoleId
            WHERE {UserColumns.UserId}=@UserId;";

        await using (var updateCmd = new NpgsqlCommand(updateQuery, _conn))
        {
            updateCmd.Parameters.AddWithValue("@UserId", userId);
            updateCmd.Parameters.AddWithValue("@RoleId", roleId);

            await updateCmd.ExecuteNonQueryAsync();
        }

        result.Success = true;
        result.Message = "Active role updated successfully.";

        return result;
    }
    catch (Exception ex)
    {
        _logger.LogError(ex,
            "Error switching active role for UserId {UserId}",
            userId);

        result.Success = false;
        result.Message = "Unable to switch active role.";

        return result;
    }
    finally
    {
        if (_conn.State == ConnectionState.Open)
            await _conn.CloseAsync();
    }
}
    }
}