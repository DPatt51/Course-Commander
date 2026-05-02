namespace CourseCommander.Integrations.Connectors;

public interface IRangeDataConnector
{
    Task<int> SyncAsync(CancellationToken cancellationToken = default);
}
