using Microsoft.Extensions.Logging;
using Npgsql;
using Repositories.Constants.Roles;
using Repositories.Constants.UserRoles;
using Repositories.Constants.Users;
using Repositories.Interfaces;
using Repositories.Models;
using Repositories.Models.ViewModels.Auth;
using Repositories.Services.CloudinaryService;
using System.Data;

namespace Repositories.Implementations
{
    public class AuthRepository : IAuthInterface
    {
        private readonly NpgsqlConnection _conn;
        private readonly ILogger<AuthRepository> _logger;


        public AuthRepository(
    NpgsqlConnection conn,
    ILogger<AuthRepository> logger)
{
    _conn = conn;
    _logger = logger;
}

        public async Task<User?> GetUserByEmailAsync(string email)
        {
            try
            {
                _logger.LogInformation("Login attempt for email: {Email}", email);

                string query = $@"
                    SELECT
                        {UserColumns.UserId},
                        {UserColumns.FirstName},
                        {UserColumns.LastName},
                        {UserColumns.Email},
                        {UserColumns.PasswordHash},
                        {UserColumns.PhoneNumber},
                        {UserColumns.IsActive}
                    FROM {UserTable.TableName}
                    WHERE {UserColumns.Email} = @Email
                    LIMIT 1;";

                _logger.LogDebug("Opening PostgreSQL connection.");

                await _conn.OpenAsync();

                _logger.LogDebug("Executing query.");

                await using var cmd = new NpgsqlCommand(query, _conn);

                cmd.Parameters.AddWithValue("@Email", email);

                await using var reader = await cmd.ExecuteReaderAsync();

                if (!await reader.ReadAsync())
                {
                    _logger.LogWarning("User not found for email: {Email}", email);
                    return null;
                }

                User user = new()
                {
                    UserId = reader.GetInt32(reader.GetOrdinal(UserColumns.UserId)),

                    FirstName = reader.GetString(reader.GetOrdinal(UserColumns.FirstName)),

                    LastName = reader.IsDBNull(reader.GetOrdinal(UserColumns.LastName))
                        ? string.Empty
                        : reader.GetString(reader.GetOrdinal(UserColumns.LastName)),

                    Email = reader.GetString(reader.GetOrdinal(UserColumns.Email)),

                    PasswordHash = reader.GetString(reader.GetOrdinal(UserColumns.PasswordHash)),

                    PhoneNumber = reader.IsDBNull(reader.GetOrdinal(UserColumns.PhoneNumber))
                        ? string.Empty
                        : reader.GetString(reader.GetOrdinal(UserColumns.PhoneNumber)),

                    IsActive = reader.GetBoolean(reader.GetOrdinal(UserColumns.IsActive))
                };

                _logger.LogInformation(
                    "User {UserId} retrieved successfully.",
                    user.UserId);

                return user;
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Error while retrieving user with email: {Email}",
                    email);

                throw;
            }
            finally
            {
                if (_conn.State == ConnectionState.Open)
                {
                    _logger.LogDebug("Closing PostgreSQL connection.");

                    await _conn.CloseAsync();
                }
            }
        }
        public async Task<List<string>> GetUserRolesAsync(int userId)
        {
            List<string> roles = new();

            try
            {
                string query = $@"
                    SELECT
                        r.{RoleColumns.RoleName}
                    FROM {RoleTable.TableName} r
                    INNER JOIN {UserRoleTable.TableName} ur
                        ON ur.{UserRoleColumns.RoleId} = r.{RoleColumns.RoleId}
                    WHERE ur.{UserRoleColumns.UserId} = @UserId
                    AND r.{RoleColumns.IsActive} = TRUE;";

                await _conn.OpenAsync();

                await using var cmd = new NpgsqlCommand(query, _conn);

                cmd.Parameters.AddWithValue("@UserId", userId);

                await using var reader = await cmd.ExecuteReaderAsync();

                while (await reader.ReadAsync())
                {
                    roles.Add(reader.GetString(0));
                }

                _logger.LogInformation(
                    "Retrieved {Count} role(s) for UserId {UserId}",
                    roles.Count,
                    userId);

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
                if (_conn.State == System.Data.ConnectionState.Open)
                    await _conn.CloseAsync();
            }
        }

        public async Task<ServiceResult> Register(RegisterVM model, string? profilePictureId)
{
    ServiceResult result = new();

    try
    {
        await _conn.OpenAsync();

        await using var transaction = await _conn.BeginTransactionAsync();

        string checkQuery =
            "SELECT COUNT(*) FROM " + UserTable.TableName +
            " WHERE " + UserColumns.Email + " = @Email;";

        await using (var checkCmd = new NpgsqlCommand(checkQuery, _conn, transaction))
        {
            checkCmd.Parameters.AddWithValue("@Email", model.Email);

            int count = Convert.ToInt32(await checkCmd.ExecuteScalarAsync());

            if (count > 0)
            {
                result.Success = false;
                result.Message = "Email already exists.";

                return result;
            }
        }

        string passwordHash = BCrypt.Net.BCrypt.HashPassword(model.Password);

        string insertUserQuery =
            "INSERT INTO " + UserTable.TableName +
            " (" +
            UserColumns.FirstName + ", " +
            UserColumns.LastName + ", " +
            UserColumns.Email + ", " +
            UserColumns.PasswordHash + ", " +
            UserColumns.PhoneNumber + ", " +
            UserColumns.ProfilePictureId + ", " +
            UserColumns.IsActive + ", " +
            UserColumns.CreatedAt +
            ") VALUES (" +
            "@FirstName,@LastName,@Email,@PasswordHash,@PhoneNumber,@ProfilePictureId,@IsActive,@CreatedAt) " +
            "RETURNING " + UserColumns.UserId + ";";

        int userId;

        await using (var cmd = new NpgsqlCommand(insertUserQuery, _conn, transaction))
        {
            cmd.Parameters.AddWithValue("@FirstName", model.FirstName);
            cmd.Parameters.AddWithValue("@LastName", model.LastName);
            cmd.Parameters.AddWithValue("@Email", model.Email);
            cmd.Parameters.AddWithValue("@PasswordHash", passwordHash);
            cmd.Parameters.AddWithValue("@PhoneNumber", model.PhoneNumber);
            cmd.Parameters.AddWithValue("@ProfilePictureId",
                (object?)profilePictureId ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@IsActive", false);
            cmd.Parameters.AddWithValue("@CreatedAt", DateTime.UtcNow);

            userId = Convert.ToInt32(await cmd.ExecuteScalarAsync());
        }

        string insertRoleQuery =
            "INSERT INTO " + UserRoleTable.TableName +
            " (" +
            UserRoleColumns.UserId + ", " +
            UserRoleColumns.RoleId + ", " +
            UserRoleColumns.AssignedAt +
            ") VALUES (" +
            "@UserId,@RoleId,@AssignedAt);";

        await using (var cmd = new NpgsqlCommand(insertRoleQuery, _conn, transaction))
        {
            cmd.Parameters.AddWithValue("@UserId", userId);
            cmd.Parameters.AddWithValue("@RoleId", 2);
            cmd.Parameters.AddWithValue("@AssignedAt", DateTime.UtcNow);

            await cmd.ExecuteNonQueryAsync();
        }

        await transaction.CommitAsync();

        result.Success = true;
        result.Message = "Registration successful.";

        return result;
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Error while registering user.");

        result.Success = false;
        result.Message = "Registration failed.";

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
    }
}