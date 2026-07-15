using System;
using System.Data;
using System.Threading.Tasks;
using Npgsql;
using Repositories.Constants.ResetPassword;
using Repositories.Constants.Users;
using Repositories.Interfaces;
using Repositories.Models.ViewModels.Auth;

namespace Repositories.Implementations
{
    public class PasswordResetRepository : IPasswordResetInterface
    {
        private readonly NpgsqlConnection _conn;

        public PasswordResetRepository(NpgsqlConnection conn)
        {
            _conn = conn;
        }

        public async Task SaveToken(int userId, string token, DateTime expiry)
        {
            try
            {
                if (_conn.State != ConnectionState.Open)
                    await _conn.OpenAsync();

                string expireOldTokensQry = $@"
UPDATE {PasswordResetTable.TableName}
SET {PasswordResetColumns.EXPIRY} = @currentTime
WHERE {PasswordResetColumns.USERID} = @userid
  AND {PasswordResetColumns.ISUSED} = FALSE
  AND {PasswordResetColumns.EXPIRY} > @currentTime;";

                using (var expireCmd = new NpgsqlCommand(expireOldTokensQry, _conn))
                {
                    expireCmd.Parameters.AddWithValue("@userid", userId);
                    expireCmd.Parameters.AddWithValue("@currentTime", DateTime.UtcNow);
                    await expireCmd.ExecuteNonQueryAsync();
                }

                string qry = $@"
INSERT INTO {PasswordResetTable.TableName}
(
    {PasswordResetColumns.USERID},
    {PasswordResetColumns.TOKEN},
    {PasswordResetColumns.EXPIRY}
)
VALUES
(
    @userid,
    @token,
    @expiry
);";

                using var cmd = new NpgsqlCommand(qry, _conn);

                cmd.Parameters.AddWithValue("@userid", userId);
                cmd.Parameters.AddWithValue("@token", token);
                cmd.Parameters.AddWithValue("@expiry", expiry);

                await cmd.ExecuteNonQueryAsync();
            }
            finally
            {
                if (_conn.State == ConnectionState.Open)
                    await _conn.CloseAsync();
            }
        }

        public async Task<ResetPasswordVm?> GetToken(string token)
        {
            try
            {
                if (_conn.State != ConnectionState.Open)
                    await _conn.OpenAsync();

                string qry = $@"
SELECT
    {PasswordResetColumns.ID},
    {PasswordResetColumns.USERID},
    {PasswordResetColumns.TOKEN},
    {PasswordResetColumns.EXPIRY},
    {PasswordResetColumns.ISUSED}
FROM {PasswordResetTable.TableName}
WHERE {PasswordResetColumns.TOKEN} = @token;";

                using var cmd = new NpgsqlCommand(qry, _conn);

                cmd.Parameters.AddWithValue("@token", token);

                using var reader = await cmd.ExecuteReaderAsync();

                if (!await reader.ReadAsync())
                    return null;

                return new ResetPasswordVm
                {
                    Id = reader.GetInt32(0),
                    UserId = reader.GetInt32(1),
                    Token = reader.GetString(2),
                    Expiry = reader.GetDateTime(3),
                    IsUsed = reader.GetBoolean(4)
                };
            }
            finally
            {
                if (_conn.State == ConnectionState.Open)
                    await _conn.CloseAsync();
            }
        }

        public async Task MarkUsed(int id)
        {
            try
            {
                if (_conn.State != ConnectionState.Open)
                    await _conn.OpenAsync();

                string qry = $@"
UPDATE {PasswordResetTable.TableName}
SET {PasswordResetColumns.ISUSED} = TRUE
WHERE {PasswordResetColumns.ID} = @id;";

                using var cmd = new NpgsqlCommand(qry, _conn);

                cmd.Parameters.AddWithValue("@id", id);

                await cmd.ExecuteNonQueryAsync();
            }
            finally
            {
                if (_conn.State == ConnectionState.Open)
                    await _conn.CloseAsync();
            }
        }

        public async Task UpdatePassword(int userId, string passwordHash)
        {
            try
            {
                if (_conn.State != ConnectionState.Open)
                    await _conn.OpenAsync();

                string qry = $@"
UPDATE {UserTable.TableName}
SET {UserColumns.PasswordHash} = @password
WHERE {UserColumns.UserId} = @userid;";

                using var cmd = new NpgsqlCommand(qry, _conn);

                cmd.Parameters.AddWithValue("@userid", userId);
                cmd.Parameters.AddWithValue("@password", passwordHash);

                await cmd.ExecuteNonQueryAsync();
            }
            finally
            {
                if (_conn.State == ConnectionState.Open)
                    await _conn.CloseAsync();
            }
        }
    }
}
