using Npgsql;
using Repositories.Constants.Users;
using Repositories.Interfaces;
using Repositories.Models;
using System.Data;

namespace Repositories.Implementations
{
    public class UserRepository : IUserInterface
    {
        private readonly NpgsqlConnection _conn;

        public UserRepository(NpgsqlConnection conn)
        {
            _conn = conn;
        }

        public async Task<User?> GetUserByIdAsync(int userId)
        {
            try
            {
                string query = $@"
                    SELECT
                        {UserColumns.UserId},
                        {UserColumns.FirstName},
                        {UserColumns.LastName},
                        {UserColumns.Email},
                        {UserColumns.PhoneNumber},
                        {UserColumns.ProfilePictureId},
                        {UserColumns.LastActiveRoleId},
                        {UserColumns.IsActive}
                    FROM {UserTable.TableName}
                    WHERE {UserColumns.UserId} = @UserId
                    LIMIT 1;";

                await _conn.OpenAsync();

                using var cmd = new NpgsqlCommand(query, _conn);
                cmd.Parameters.AddWithValue("@UserId", userId);

                using var reader = await cmd.ExecuteReaderAsync();

                if (!await reader.ReadAsync())
                    return null;

                return new User
                {
                    UserId = reader.GetInt32(reader.GetOrdinal(UserColumns.UserId)),
                    FirstName = reader.GetString(reader.GetOrdinal(UserColumns.FirstName)),
                    LastName = reader.IsDBNull(reader.GetOrdinal(UserColumns.LastName))
                        ? string.Empty
                        : reader.GetString(reader.GetOrdinal(UserColumns.LastName)),
                    Email = reader.GetString(reader.GetOrdinal(UserColumns.Email)),
                    PhoneNumber = reader.IsDBNull(reader.GetOrdinal(UserColumns.PhoneNumber))
                        ? string.Empty
                        : reader.GetString(reader.GetOrdinal(UserColumns.PhoneNumber)),
                    ProfilePictureId = reader.IsDBNull(reader.GetOrdinal(UserColumns.ProfilePictureId))
                        ? null
                        : reader.GetString(reader.GetOrdinal(UserColumns.ProfilePictureId)),
                    LastActiveRoleId = reader.IsDBNull(reader.GetOrdinal(UserColumns.LastActiveRoleId))
                        ? null
                        : reader.GetInt32(reader.GetOrdinal(UserColumns.LastActiveRoleId)),
                    IsActive = reader.GetBoolean(reader.GetOrdinal(UserColumns.IsActive))
                };
            }
            finally
            {
                if (_conn.State == ConnectionState.Open)
                    await _conn.CloseAsync();
            }
        }

        public async Task<User?> GetUserByEmailAsync(string email)
        {
            try
            {
                string query = $@"
                    SELECT
                        {UserColumns.UserId},
                        {UserColumns.FirstName},
                        {UserColumns.LastName},
                        {UserColumns.Email},
                        {UserColumns.PhoneNumber},
                        {UserColumns.ProfilePictureId},
                        {UserColumns.LastActiveRoleId},
                        {UserColumns.IsActive}
                    FROM {UserTable.TableName}
                    WHERE {UserColumns.Email} = @Email
                    LIMIT 1;";

                await _conn.OpenAsync();

                using var cmd = new NpgsqlCommand(query, _conn);
                cmd.Parameters.AddWithValue("@Email", email);

                using var reader = await cmd.ExecuteReaderAsync();

                if (!await reader.ReadAsync())
                    return null;

                return new User
                {
                    UserId = reader.GetInt32(reader.GetOrdinal(UserColumns.UserId)),
                    FirstName = reader.GetString(reader.GetOrdinal(UserColumns.FirstName)),
                    LastName = reader.IsDBNull(reader.GetOrdinal(UserColumns.LastName))
                        ? string.Empty
                        : reader.GetString(reader.GetOrdinal(UserColumns.LastName)),
                    Email = reader.GetString(reader.GetOrdinal(UserColumns.Email)),
                    PhoneNumber = reader.IsDBNull(reader.GetOrdinal(UserColumns.PhoneNumber))
                        ? string.Empty
                        : reader.GetString(reader.GetOrdinal(UserColumns.PhoneNumber)),
                    ProfilePictureId = reader.IsDBNull(reader.GetOrdinal(UserColumns.ProfilePictureId))
                        ? null
                        : reader.GetString(reader.GetOrdinal(UserColumns.ProfilePictureId)),
                    LastActiveRoleId = reader.IsDBNull(reader.GetOrdinal(UserColumns.LastActiveRoleId))
                        ? null
                        : reader.GetInt32(reader.GetOrdinal(UserColumns.LastActiveRoleId)),
                    IsActive = reader.GetBoolean(reader.GetOrdinal(UserColumns.IsActive))
                };
            }
            finally
            {
                if (_conn.State == ConnectionState.Open)
                    await _conn.CloseAsync();
            }
        }

        public async Task<ServiceResult> UpdateProfileAsync(User user)
        {
            ServiceResult result = new();

            try
            {
                string query = $@"
                    UPDATE {UserTable.TableName}
                    SET
                        {UserColumns.FirstName} = @FirstName,
                        {UserColumns.LastName} = @LastName,
                        {UserColumns.PhoneNumber} = @PhoneNumber
                    WHERE {UserColumns.UserId} = @UserId;";

                await _conn.OpenAsync();

                using var cmd = new NpgsqlCommand(query, _conn);

                cmd.Parameters.AddWithValue("@UserId", user.UserId);
                cmd.Parameters.AddWithValue("@FirstName", user.FirstName);
                cmd.Parameters.AddWithValue("@LastName", user.LastName);
                cmd.Parameters.AddWithValue("@PhoneNumber", user.PhoneNumber);

                await cmd.ExecuteNonQueryAsync();

                result.Success = true;
                result.Message = "Profile updated successfully.";
            }
            catch
            {
                result.Success = false;
                result.Message = "Unable to update profile.";
            }
            finally
            {
                if (_conn.State == ConnectionState.Open)
                    await _conn.CloseAsync();
            }

            return result;
        }

        public async Task<ServiceResult> ChangeProfilePictureAsync(int userId, string? profilePictureId)
        {
            ServiceResult result = new();

            try
            {
                string query = $@"
                    UPDATE {UserTable.TableName}
                    SET {UserColumns.ProfilePictureId} = @ProfilePictureId
                    WHERE {UserColumns.UserId} = @UserId;";

                await _conn.OpenAsync();

                using var cmd = new NpgsqlCommand(query, _conn);

                cmd.Parameters.AddWithValue("@UserId", userId);
                cmd.Parameters.AddWithValue("@ProfilePictureId",
                    (object?)profilePictureId ?? DBNull.Value);

                await cmd.ExecuteNonQueryAsync();

                result.Success = true;
                result.Message = "Profile picture updated successfully.";
            }
            catch
            {
                result.Success = false;
                result.Message = "Unable to update profile picture.";
            }
            finally
            {
                if (_conn.State == ConnectionState.Open)
                    await _conn.CloseAsync();
            }

            return result;
        }
    }
}