namespace CourseCommander.Integrations.Connectors;

public interface ITaskDataConnector
{
    Task<int> SyncAsync(CancellationToken cancellationToken = default);
}
