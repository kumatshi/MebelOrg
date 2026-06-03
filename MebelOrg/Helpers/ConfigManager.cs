using Microsoft.Extensions.Configuration;
using System.IO;

namespace MebelOrg.Helpers;

public static class ConfigManager
{
    private static IConfiguration? _configuration;

    public static void LoadConfiguration()
    {
        var builder = new ConfigurationBuilder()
            .SetBasePath(AppDomain.CurrentDomain.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);
        _configuration = builder.Build();
    }

    public static string DbConnectionString =>
        _configuration?["ConnectionStrings:Postgres"]
        ?? "Host=localhost;Port=5432;Database=mebelorg;Username=postgres;Password=password";

    public static string DataImportFolder
    {
        get
        {
            var configured = _configuration?["ImportPath"] ?? "..\\импорт";
            var path = Path.IsPathRooted(configured)
                ? configured
                : Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, configured));

            if (!Directory.Exists(path))
            {
                var alternative = Path.GetFullPath(Path.Combine(
                    AppDomain.CurrentDomain.BaseDirectory,
                    "..", "..", "..", "..", "импорт"));
                if (Directory.Exists(alternative))
                    return alternative;
            }
            return path;
        }
    }
}