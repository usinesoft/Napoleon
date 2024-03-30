namespace Napoleon.Server;

public interface IServer
{
    string? MyNodeId { get; }

    StatusInCluster MyStatus { get; }

    NodeStatus[] AllNodes();

    /// <summary>
    ///     Block wile the server is synchronizing data
    /// </summary>
    Task WaitSyncingEnd();

    void WakeMeUpWhenDataChanged(WakeUpCall wakeUpCall);
}

/// <summary>
///     Wraps a synchronization object that is initialized by the client as blocking and released by the server
///     when the client need to be woke up
/// </summary>
public class WakeUpCall
{
    private readonly SemaphoreSlim _blockingSemaphore = new(0);

    public Task WaitForCall()
    {
        return _blockingSemaphore.WaitAsync();
    }

    public void WakeUp()
    {
        _blockingSemaphore.Release();
    }
}