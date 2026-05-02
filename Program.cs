using CourseCommander.Data;
using CourseCommander.Integrations.Connectors;
using CourseCommander.Services;
using Microsoft.EntityFrameworkCore;
using System.Data;
using System.Text.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);
var port = Environment.GetEnvironmentVariable("PORT");

if (!string.IsNullOrWhiteSpace(port))
{
    builder.WebHost.UseUrls($"http://0.0.0.0:{port}");
}

builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    });
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddHttpClient();
builder.Services.AddCors(options =>
{
    options.AddPolicy("Frontend", policy =>
    {
        policy.WithOrigins("http://localhost:5173", "http://127.0.0.1:5173")
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

var databaseUrl = Environment.GetEnvironmentVariable("DATABASE_URL");
var usePostgres = !string.IsNullOrWhiteSpace(databaseUrl);

builder.Services.AddDbContext<AppDbContext>(options =>
{
    if (usePostgres)
    {
        options.UseNpgsql(DatabaseUrlHelper.ConvertDatabaseUrlToConnectionString(databaseUrl!));
        return;
    }

    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")
        ?? "Data Source=course-commander.db");
});

builder.Services.Configure<DailyBriefingNotificationOptions>(
    builder.Configuration.GetSection("DailyBriefingNotifications"));

builder.Services.AddScoped<DashboardMetricService>();
builder.Services.AddScoped<GrowingDegreeDayService>();
builder.Services.AddScoped<TrendService>();
builder.Services.AddScoped<ForecastService>();
builder.Services.AddScoped<FandBAnalyticsService>();
builder.Services.AddScoped<AgronomyService>();
builder.Services.AddScoped<DailyBriefingService>();
builder.Services.AddScoped<AlertService>();
builder.Services.AddScoped<PriorityService>();
builder.Services.AddScoped<InsightService>();
builder.Services.AddScoped<DemoDataService>();
builder.Services.AddScoped<NotificationService>();
builder.Services.AddScoped<AdminService>();
builder.Services.AddScoped<TenForeConnector>();
builder.Services.AddScoped<ToastConnector>();
builder.Services.AddScoped<AsbTaskTrackerConnector>();
builder.Services.AddScoped<RangeServantConnector>();
builder.Services.AddScoped<FieldScoutConnector>();
builder.Services.AddScoped<IPlayDataConnector, TenForeConnector>();
builder.Services.AddScoped<ISalesDataConnector, TenForeConnector>();
builder.Services.AddScoped<ISalesDataConnector, ToastConnector>();
builder.Services.AddScoped<ITaskDataConnector, AsbTaskTrackerConnector>();
builder.Services.AddScoped<IRangeDataConnector, RangeServantConnector>();
builder.Services.AddScoped<IAgronomyDataConnector, FieldScoutConnector>();
builder.Services.AddHostedService<DailyBriefingBackgroundService>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

    if (usePostgres)
    {
        dbContext.Database.Migrate();
    }
    else
    {
        dbContext.Database.EnsureCreated();
        EnsureDailySalesMetricColumns(dbContext);
        EnsureMaintenanceTaskColumns(dbContext);
        EnsureEquipmentIssueColumns(dbContext);
        EnsureAgronomyReadingTable(dbContext);
        EnsureAdminTables(dbContext);
    }
}

app.UseCors("Frontend");

app.MapControllers();

app.Run();

static void EnsureDailySalesMetricColumns(AppDbContext dbContext)
{
    var connection = dbContext.Database.GetDbConnection();
    var shouldCloseConnection = connection.State != ConnectionState.Open;

    if (shouldCloseConnection)
    {
        connection.Open();
    }

    try
    {
        var columns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        using (var command = connection.CreateCommand())
        {
            command.CommandText = "PRAGMA table_info(DailySalesMetrics);";

            using var reader = command.ExecuteReader();

            while (reader.Read())
            {
                columns.Add(reader.GetString(1));
            }
        }

        if (!columns.Contains("AlcoholRevenue"))
        {
            dbContext.Database.ExecuteSqlRaw("ALTER TABLE DailySalesMetrics ADD COLUMN AlcoholRevenue TEXT NOT NULL DEFAULT '0';");
        }

        if (!columns.Contains("RangeBallRevenue"))
        {
            dbContext.Database.ExecuteSqlRaw("ALTER TABLE DailySalesMetrics ADD COLUMN RangeBallRevenue TEXT NOT NULL DEFAULT '0';");
        }
    }
    finally
    {
        if (shouldCloseConnection)
        {
            connection.Close();
        }
    }
}

static void EnsureMaintenanceTaskColumns(AppDbContext dbContext)
{
    var connection = dbContext.Database.GetDbConnection();
    var shouldCloseConnection = connection.State != ConnectionState.Open;

    if (shouldCloseConnection)
    {
        connection.Open();
    }

    try
    {
        var columns = GetTableColumns(connection, "MaintenanceTasks");

        EnsureColumn(dbContext, columns, "MaintenanceTasks", "AssignedTo", "TEXT NULL");
        EnsureColumn(dbContext, columns, "MaintenanceTasks", "StartedAt", "TEXT NULL");
        EnsureColumn(dbContext, columns, "MaintenanceTasks", "UpdatedAt", "TEXT NOT NULL DEFAULT '0001-01-01T00:00:00'");
        EnsureColumn(dbContext, columns, "MaintenanceTasks", "ExternalSourceName", "TEXT NULL");
        EnsureColumn(dbContext, columns, "MaintenanceTasks", "ExternalTaskId", "TEXT NULL");
        EnsureColumn(dbContext, columns, "MaintenanceTasks", "ExternalStatus", "TEXT NULL");
        EnsureColumn(dbContext, columns, "MaintenanceTasks", "LastSyncedAt", "TEXT NULL");
        EnsureColumn(dbContext, columns, "MaintenanceTasks", "IsExternal", "INTEGER NOT NULL DEFAULT 0");

        dbContext.Database.ExecuteSqlRaw("UPDATE MaintenanceTasks SET Status = 'InProgress' WHERE Status = 'In Progress';");
        dbContext.Database.ExecuteSqlRaw("UPDATE MaintenanceTasks SET UpdatedAt = CreatedAt WHERE UpdatedAt = '0001-01-01T00:00:00';");
    }
    finally
    {
        if (shouldCloseConnection)
        {
            connection.Close();
        }
    }
}

static void EnsureEquipmentIssueColumns(AppDbContext dbContext)
{
    var connection = dbContext.Database.GetDbConnection();
    var shouldCloseConnection = connection.State != ConnectionState.Open;

    if (shouldCloseConnection)
    {
        connection.Open();
    }

    try
    {
        var columns = GetTableColumns(connection, "EquipmentIssues");

        EnsureColumn(dbContext, columns, "EquipmentIssues", "AssignedTo", "TEXT NULL");
        EnsureColumn(dbContext, columns, "EquipmentIssues", "StartedAt", "TEXT NULL");
        EnsureColumn(dbContext, columns, "EquipmentIssues", "CompletedAt", "TEXT NULL");
        EnsureColumn(dbContext, columns, "EquipmentIssues", "UpdatedAt", "TEXT NOT NULL DEFAULT '0001-01-01T00:00:00'");
        EnsureColumn(dbContext, columns, "EquipmentIssues", "Notes", "TEXT NULL");
        EnsureColumn(dbContext, columns, "EquipmentIssues", "PartName", "TEXT NULL");
        EnsureColumn(dbContext, columns, "EquipmentIssues", "PartOrderedDate", "TEXT NULL");
        EnsureColumn(dbContext, columns, "EquipmentIssues", "ExpectedArrivalDate", "TEXT NULL");
        EnsureColumn(dbContext, columns, "EquipmentIssues", "ExternalSourceName", "TEXT NULL");
        EnsureColumn(dbContext, columns, "EquipmentIssues", "ExternalIssueId", "TEXT NULL");
        EnsureColumn(dbContext, columns, "EquipmentIssues", "ExternalStatus", "TEXT NULL");
        EnsureColumn(dbContext, columns, "EquipmentIssues", "LastSyncedAt", "TEXT NULL");
        EnsureColumn(dbContext, columns, "EquipmentIssues", "IsExternal", "INTEGER NOT NULL DEFAULT 0");

        dbContext.Database.ExecuteSqlRaw("UPDATE EquipmentIssues SET Status = 'InProgress' WHERE Status = 'In Progress';");
        dbContext.Database.ExecuteSqlRaw("UPDATE EquipmentIssues SET Status = 'WaitingOnParts' WHERE Status = 'Waiting on Parts';");

        if (columns.Contains("ResolvedAt"))
        {
            dbContext.Database.ExecuteSqlRaw("UPDATE EquipmentIssues SET CompletedAt = COALESCE(CompletedAt, ResolvedAt) WHERE Status = 'Resolved' AND CompletedAt IS NULL;");
        }

        if (columns.Contains("RepairedAt"))
        {
            dbContext.Database.ExecuteSqlRaw("UPDATE EquipmentIssues SET CompletedAt = COALESCE(CompletedAt, RepairedAt) WHERE Status = 'Repaired' AND CompletedAt IS NULL;");
        }

        dbContext.Database.ExecuteSqlRaw("UPDATE EquipmentIssues SET Status = 'Resolved' WHERE Status = 'Repaired';");
        dbContext.Database.ExecuteSqlRaw("UPDATE EquipmentIssues SET UpdatedAt = ReportedAt WHERE UpdatedAt = '0001-01-01T00:00:00';");
    }
    finally
    {
        if (shouldCloseConnection)
        {
            connection.Close();
        }
    }
}

static void EnsureAgronomyReadingTable(AppDbContext dbContext)
{
    dbContext.Database.ExecuteSqlRaw("""
        CREATE TABLE IF NOT EXISTS AgronomyReadings (
            Id INTEGER NOT NULL CONSTRAINT PK_AgronomyReadings PRIMARY KEY AUTOINCREMENT,
            DateTime TEXT NOT NULL,
            MeasurementType TEXT NOT NULL,
            Location TEXT NOT NULL,
            Zone TEXT NOT NULL DEFAULT '',
            Value TEXT NOT NULL,
            Unit TEXT NOT NULL DEFAULT '',
            SourceSystemName TEXT NOT NULL DEFAULT 'Manual Entry',
            ExternalReadingId TEXT NULL,
            Notes TEXT NULL,
            CreatedAt TEXT NOT NULL,
            SyncedAt TEXT NULL
        );
        """);
}

static void EnsureAdminTables(AppDbContext dbContext)
{
    dbContext.Database.ExecuteSqlRaw("""
        CREATE TABLE IF NOT EXISTS AdminReminders (
            Id INTEGER NOT NULL CONSTRAINT PK_AdminReminders PRIMARY KEY AUTOINCREMENT,
            Title TEXT NOT NULL,
            Description TEXT NOT NULL DEFAULT '',
            Category TEXT NOT NULL DEFAULT 'Other',
            DueDate TEXT NOT NULL,
            IsRecurring INTEGER NOT NULL DEFAULT 0,
            RecurrenceType TEXT NOT NULL DEFAULT 'Custom',
            IsCompleted INTEGER NOT NULL DEFAULT 0,
            CompletedAt TEXT NULL,
            CreatedAt TEXT NOT NULL
        );
        """);

    dbContext.Database.ExecuteSqlRaw("""
        CREATE TABLE IF NOT EXISTS PayrollPeriods (
            Id INTEGER NOT NULL CONSTRAINT PK_PayrollPeriods PRIMARY KEY AUTOINCREMENT,
            PeriodStartDate TEXT NOT NULL,
            PeriodEndDate TEXT NOT NULL,
            PayrollDueDate TEXT NOT NULL,
            Status TEXT NOT NULL DEFAULT 'Open',
            Notes TEXT NOT NULL DEFAULT '',
            CreatedAt TEXT NOT NULL,
            SubmittedAt TEXT NULL
        );
        """);
}

static HashSet<string> GetTableColumns(System.Data.Common.DbConnection connection, string tableName)
{
    var columns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    using var command = connection.CreateCommand();
    command.CommandText = $"PRAGMA table_info({tableName});";

    using var reader = command.ExecuteReader();

    while (reader.Read())
    {
        columns.Add(reader.GetString(1));
    }

    return columns;
}

static void EnsureColumn(
    AppDbContext dbContext,
    HashSet<string> columns,
    string tableName,
    string columnName,
    string columnDefinition)
{
    if (columns.Contains(columnName))
    {
        return;
    }

    var alterTableSql = $"ALTER TABLE {tableName} ADD COLUMN {columnName} {columnDefinition};";
    dbContext.Database.ExecuteSqlRaw(alterTableSql);
    columns.Add(columnName);
}
