using ArkhivRobit.Data;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace ArkhivRobit.Services;

/// <summary>Підключення до бази даних: SQLite для локальної розробки, PostgreSQL для хмари.</summary>
public static class DatabaseSetup
{
    public static void AddArchiveDatabase(this IServiceCollection services, IConfiguration config, string contentRoot)
    {
        var provider = config["Database:Provider"] ?? "Sqlite";
        var connection = config.GetConnectionString("Default");

        services.AddDbContext<AppDbContext>(options =>
        {
            if (provider.Equals("Postgres", StringComparison.OrdinalIgnoreCase))
            {
                if (string.IsNullOrWhiteSpace(connection))
                    throw new InvalidOperationException("Для PostgreSQL задайте ConnectionStrings__Default.");
                options.UseNpgsql(ToNpgsqlConnectionString(connection));
            }
            else
            {
                if (string.IsNullOrWhiteSpace(connection))
                {
                    var dataDir = Path.Combine(contentRoot, "App_Data");
                    Directory.CreateDirectory(dataDir);
                    connection = $"Data Source={Path.Combine(dataDir, "arkhiv.db")}";
                }
                options.UseSqlite(connection);
            }
        });
    }

    /// <summary>
    /// Хмарні сервіси (Neon, Supabase, Render) дають адресу бази у вигляді
    /// «postgresql://user:password@host:5432/db?sslmode=require». Npgsql її не розуміє,
    /// тому перетворюємо на формат «Host=…;Username=…;Password=…».
    /// </summary>
    public static string ToNpgsqlConnectionString(string value)
    {
        if (!value.StartsWith("postgres://", StringComparison.OrdinalIgnoreCase) &&
            !value.StartsWith("postgresql://", StringComparison.OrdinalIgnoreCase))
            return value;

        var uri = new Uri(value);
        var userInfo = uri.UserInfo.Split(':', 2);
        var builder = new NpgsqlConnectionStringBuilder
        {
            Host = uri.Host,
            Port = uri.IsDefaultPort || uri.Port <= 0 ? 5432 : uri.Port,
            Database = Uri.UnescapeDataString(uri.AbsolutePath.TrimStart('/')),
            Username = Uri.UnescapeDataString(userInfo[0]),
            Password = userInfo.Length > 1 ? Uri.UnescapeDataString(userInfo[1]) : null,
            SslMode = SslMode.Require,
        };

        foreach (var pair in uri.Query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var parts = pair.Split('=', 2);
            if (parts[0].Equals("sslmode", StringComparison.OrdinalIgnoreCase) && parts.Length == 2 &&
                Enum.TryParse<SslMode>(parts[1], ignoreCase: true, out var mode))
                builder.SslMode = mode;
        }

        return builder.ConnectionString;
    }
}
