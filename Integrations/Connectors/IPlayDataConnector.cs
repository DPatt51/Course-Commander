namespace CourseCommander.Integrations.Connectors;

public interface IPlayDataConnector
{
    Task<int> SyncAsync(CancellationToken cancellationToken = default);
}
