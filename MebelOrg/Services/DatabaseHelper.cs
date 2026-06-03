using Npgsql;
using MebelOrg.Helpers;
using System.IO;

namespace MebelOrg.Services;

public static class DatabaseHelper
{
    public static NpgsqlConnection CreateConnection() => new(ConfigManager.DbConnectionString);

    public static async Task InitializeDatabaseAsync()
    {
        var scriptPath = Path.GetFullPath(Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory,
            "..", "..", "..", "..", "database", "init.sql"));

        if (!File.Exists(scriptPath))
        {
            scriptPath = Path.GetFullPath(Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory,
                "..", "..", "..", "database", "init.sql"));
        }

        if (!File.Exists(scriptPath))
            return;

        var sqlScript = await File.ReadAllTextAsync(scriptPath);
        await using var connection = CreateConnection();
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sqlScript, connection);
        await command.ExecuteNonQueryAsync();
    }

    public static async Task<bool> IsDatabasePopulatedAsync()
    {
        await using var connection = CreateConnection();
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand("SELECT COUNT(*) FROM products", connection);
        var count = Convert.ToInt32(await command.ExecuteScalarAsync());
        return count > 0;
    }
}