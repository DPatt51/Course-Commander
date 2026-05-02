using CourseCommander.Data;

namespace CourseCommander.Integrations.Connectors;

public class RangeServantConnector : ConnectorBase, IRangeDataConnector
{
    public RangeServantConnector(AppDbContext context, IConfiguration configuration)
        : base(context, configuration, "Range Servant", "Range")
    {
    }

    public Task<int> SyncAsync(CancellationToken cancellationToken = default)
    {
        var configNote = HasConfigValue("RANGE_SERVANT_API_KEY")
            ? "API key is configured."
            : "API key is not configured yet.";

        return CompletePlaceholderSyncAsync(
            $"Range Servant sync completed. {configNote} Range activity sync is ready to be connected.",
            cancellationToken);
    }
}
