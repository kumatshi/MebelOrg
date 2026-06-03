using Npgsql;
using MebelOrg.Models;

namespace MebelOrg.Services;

public static class AuthService
{
    public static async Task<UserAccount?> AuthenticateAsync(string login, string password)
    {
        // Добавляем приведение к нижнему регистру для логина и сравнение без учета регистра
        const string sql = """
            SELECT u.id, u.full_name, u.login, u.password, r.name
            FROM users u
            JOIN roles r ON r.id = u.role_id
            WHERE LOWER(u.login) = LOWER(@login) AND u.password = @password
            """;

        await using var connection = DatabaseHelper.CreateConnection();
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("login", login.Trim());
        command.Parameters.AddWithValue("password", password);

        await using var reader = await command.ExecuteReaderAsync();
        if (!await reader.ReadAsync())
            return null;

        var roleTitle = reader.GetString(4);
        return new UserAccount
        {
            Id = reader.GetInt32(0),
            FullName = reader.GetString(1),
            Login = reader.GetString(2),
            Password = reader.GetString(3),
            RoleName = roleTitle,
            RoleType = UserAccount.ParseRole(roleTitle)
        };
    }
}