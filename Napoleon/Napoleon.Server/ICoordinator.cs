namespace Napoleon.Server;


/// <summary>
/// Interface between the <see cref="ClusterCoordinator"/> and the data server
/// </summary>
public interface ICoordinator
{
    /// <summary>
    ///     Role of the current node (Leader, Follower,...)
    /// </summary>
    StatusInCluster MyStatus { get; }

    /// <summary>
    ///     Status of each known node in the cluster
    /// </summary>
    /// <returns></returns>
    NodeStatus[] AllNodes();

    /// <summary>
    ///     Block wile the server is synchronizing data
    /// </summary>
    Task WaitSyncingEnd();

    /// <summary>
    ///     Ask for a wake-up call when data changed. This allows awaiting for
    ///     data changes thus liberating the current thread
    /// </summary>
    /// <param name="wakeUpCall"></param>
    void WakeMeUpWhenDataChanged(WakeUpCall wakeUpCall);
}

/// <summary>
///     Wraps a synchronization object that is initialized by the client as blocking and released by the server
///     when the client need to be woke up
/// </summary>
public class WakeUpCall
{
    private readonly SemaphoreSlim _blockingSemaphore = new(0);

    /// <summary>
    /// This is called by a client of the coordinator 
    /// </summary>
    /// <returns></returns>
    public Task WaitForCall()
    {
        return _blockingSemaphore.WaitAsync();
    }

    /// <summary>
    /// This is called by the <see cref="ClusterCoordinator"/>
    /// </summary>
    public void WakeUp()
    {
        _blockingSemaphore.Release();
    }
}