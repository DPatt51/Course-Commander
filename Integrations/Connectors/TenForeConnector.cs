using CourseCommander.Data;

namespace CourseCommander.Integrations.Connectors;

public class TenForeConnector : ConnectorBase, IPlayDataConnector, ISalesDataConnector
{
    public TenForeConnector(AppDbContext context, IConfiguration configuration)
        : base(context, configuration, "TenFore", "Play/Sales")
    {
    }

    public Task<int> SyncAsync(CancellationToken cancellationToken = default)
    {
        var configNote = HasConfigValue("TENFORE_API_KEY")
            ? "API key is configured."
            : "API key is not configured yet.";

        return CompletePlaceholderSyncAsync(
            $"TenFore sync completed. {configNote} Play and sales API sync is ready to be connected.",
            cancellationToken);
    }
}
