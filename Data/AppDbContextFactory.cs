using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace CourseCommander.Data;

public class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: true)
            .AddJsonFile("appsettings.Development.json", optional: true)
            .AddEnvironmentVariables()
            .Build();

        var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();
        var databaseUrl = Environment.GetEnvironmentVariable("DATABASE_URL");

        if (!string.IsNullOrWhiteSpace(databaseUrl))
        {
            optionsBuilder.UseNpgsql(DatabaseUrlHelper.ConvertDatabaseUrlToConnectionString(databaseUrl));
        }
        else
        {
            optionsBuilder.UseSqlite(configuration.GetConnectionString("DefaultConnection")
                ?? "Data Source=course-commander.db");
        }

        return new AppDbContext(optionsBuilder.Options);
    }
}
