using CourseCommander.Data;
using CourseCommander.Entities;
using Microsoft.EntityFrameworkCore;

namespace CourseCommander.Integrations.Connectors;

public abstract class ConnectorBase
{
    private readonly string _sourceSystemName;
    private readonly string _sourceSystemType;

    protected ConnectorBase(
        AppDbContext context,
        IConfiguration configuration,
        string sourceSystemName,
        string sourceSystemType)
    {
        Context = context;
        Configuration = configuration;
        _sourceSystemName = sourceSystemName;
        _sourceSystemType = sourceSystemType;
    }

    protected AppDbContext Context { get; }
    protected IConfiguration Configuration { get; }

    protected async Task<int> CompletePlaceholderSyncAsync(
        string message,
        CancellationToken cancellationToken = default)
    {
        var sourceSystem = await GetOrCreateSourceSystemAsync(cancellationToken);
        var syncRun = new SyncRun
        {
            SourceSystemId = sourceSystem.Id,
            SourceSystem = sourceSystem,
            StartedAt = DateTime.UtcNow,
            Status = "InProgress",
            Message = $"{_sourceSystemName} sync started."
        };

        Context.SyncRuns.Add(syncRun);
        await Context.SaveChangesAsync(cancellationToken);

        syncRun.CompletedAt = DateTime.UtcNow;
        syncRun.Status = "Success";
        syncRun.Message = message;
        syncRun.RecordsProcessed = 0;

        await Context.SaveChangesAsync(cancellationToken);

        return syncRun.RecordsProcessed;
    }

    protected bool HasConfigValue(string key)
    {
        return !string.IsNullOrWhiteSpace(Configuration[$"VendorIntegrations:{key}"])
            || !string.IsNullOrWhiteSpace(Configuration[key]);
    }

    private async Task<SourceSystem> GetOrCreateSourceSystemAsync(CancellationToken cancellationToken)
    {
        var sourceSystem = await Context.SourceSystems
            .FirstOrDefaultAsync(
                source => source.Name == _sourceSystemName && source.Type == _sourceSystemType,
                cancellationToken);

        if (sourceSystem is not null)
        {
            return sourceSystem;
        }

        sourceSystem = new SourceSystem
        {
            Name = _sourceSystemName,
            Type = _sourceSystemType,
            IsEnabled = true,
            CreatedAt = DateTime.UtcNow
        };

        Context.SourceSystems.Add(sourceSystem);
        await Context.SaveChangesAsync(cancellationToken);

        return sourceSystem;
    }
}
