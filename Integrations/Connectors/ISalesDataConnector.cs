namespace CourseCommander.Integrations.Connectors;

public interface ISalesDataConnector
{
    Task<int> SyncAsync(CancellationToken cancellationToken = default);
}
