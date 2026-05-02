namespace CourseCommander.Integrations.Connectors;

public interface IAgronomyDataConnector
{
    Task<int> SyncAsync(CancellationToken cancellationToken = default);
}
