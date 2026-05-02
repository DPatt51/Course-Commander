using CourseCommander.Entities;
using Microsoft.EntityFrameworkCore;

namespace CourseCommander.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<DailyOperationMetric> DailyOperationMetrics => Set<DailyOperationMetric>();
    public DbSet<MaintenanceTask> MaintenanceTasks => Set<MaintenanceTask>();
    public DbSet<EquipmentIssue> EquipmentIssues => Set<EquipmentIssue>();
    public DbSet<Insight> Insights => Set<Insight>();
    public DbSet<SourceSystem> SourceSystems => Set<SourceSystem>();
    public DbSet<SyncRun> SyncRuns => Set<SyncRun>();
    public DbSet<DailyWeatherMetric> DailyWeatherMetrics => Set<DailyWeatherMetric>();
    public DbSet<DailySalesMetric> DailySalesMetrics => Set<DailySalesMetric>();
    public DbSet<DailyPlayMetric> DailyPlayMetrics => Set<DailyPlayMetric>();
    public DbSet<AgronomyReading> AgronomyReadings => Set<AgronomyReading>();
    public DbSet<AdminReminder> AdminReminders => Set<AdminReminder>();
    public DbSet<PayrollPeriod> PayrollPeriods => Set<PayrollPeriod>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<MaintenanceTask>()
            .Property(task => task.Status)
            .HasConversion<string>();

        modelBuilder.Entity<EquipmentIssue>()
            .Property(issue => issue.Status)
            .HasConversion<string>();

        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            foreach (var property in entityType.GetProperties())
            {
                if (property.ClrType == typeof(DateTime) || property.ClrType == typeof(DateTime?))
                {
                    property.SetColumnType("timestamp without time zone");
                }
            }
        }
    }
}
