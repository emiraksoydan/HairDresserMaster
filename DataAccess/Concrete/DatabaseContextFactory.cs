using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using System.IO;
using System.Text.Json;

namespace DataAccess.Concrete
{
    public class DatabaseContextFactory : IDesignTimeDbContextFactory<DatabaseContext>
    {
        public DatabaseContext CreateDbContext(string[] args)
        {
            var basePath = Directory.GetCurrentDirectory();
            var apiPath = Path.GetFullPath(Path.Combine(basePath, "..", "Api"));
            var appsettingsPath = Path.Combine(apiPath, "appsettings.json");

            var envConn = Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection");
            string? jsonConn = null;
            if (File.Exists(appsettingsPath))
            {
                var json = File.ReadAllText(appsettingsPath);
                using var doc = JsonDocument.Parse(json);
                if (doc.RootElement.TryGetProperty("ConnectionStrings", out var cs) &&
                    cs.TryGetProperty("DefaultConnection", out var dc))
                {
                    jsonConn = dc.GetString();
                }
            }

            var connectionString = !string.IsNullOrWhiteSpace(envConn) ? envConn : jsonConn
                ?? throw new InvalidOperationException("DefaultConnection not found.");

            var optionsBuilder = new DbContextOptionsBuilder<DatabaseContext>();
            optionsBuilder.UseNpgsql(connectionString);

            return new DatabaseContext(optionsBuilder.Options);
        }
    }
}
