using CourseCommander.Data;

namespace CourseCommander.Integrations.Connectors;

public class AsbTaskTrackerConnector : ConnectorBase, ITaskDataConnector
{
    public AsbTaskTrackerConnector(AppDbContext context, IConfiguration configuration)
        : base(context, configuration, "ASB Task Tracker", "Tasks")
    {
    }

    public Task<int> SyncAsync(CancellationToken cancellationToken = default)
    {
        var configNote = HasConfigValue("ASB_API_KEY")
            ? "API key is configured."
            : "API key is not configured yet.";

        return CompletePlaceholderSyncAsync(
            $"ASB Task Tracker sync completed. {configNote} Maintenance task sync is ready to be connected.",
            cancellationToken);
    }
}
