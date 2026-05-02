using CourseCommander.Data;

namespace CourseCommander.Integrations.Connectors;

public class FieldScoutConnector : ConnectorBase, IAgronomyDataConnector
{
    public FieldScoutConnector(AppDbContext context, IConfiguration configuration)
        : base(context, configuration, "FieldScout", "Agronomy")
    {
    }

    public Task<int> SyncAsync(CancellationToken cancellationToken = default)
    {
        var configNote = HasConfigValue("FIELDSCOUT_API_KEY")
            ? "API key is configured."
            : "API key is not configured yet.";

        return CompletePlaceholderSyncAsync(
            $"FieldScout sync completed. {configNote} Moisture and soil reading sync is ready to be connected.",
            cancellationToken);
    }
}
