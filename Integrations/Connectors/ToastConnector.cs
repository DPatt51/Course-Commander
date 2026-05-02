using CourseCommander.Data;

namespace CourseCommander.Integrations.Connectors;

public class ToastConnector : ConnectorBase, ISalesDataConnector
{
    public ToastConnector(AppDbContext context, IConfiguration configuration)
        : base(context, configuration, "Toast", "Sales")
    {
    }

    public Task<int> SyncAsync(CancellationToken cancellationToken = default)
    {
        var configNote = HasConfigValue("TOAST_CLIENT_ID") && HasConfigValue("TOAST_CLIENT_SECRET")
            ? "Client credentials are configured."
            : "Client credentials are not configured yet.";

        return CompletePlaceholderSyncAsync(
            $"Toast sync completed. {configNote} Food and beverage sales sync is ready to be connected.",
            cancellationToken);
    }
}
