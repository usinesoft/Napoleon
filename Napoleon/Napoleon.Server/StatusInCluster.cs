namespace Napoleon.Server;

public enum StatusInCluster
{
    /// <summary>
    ///     Not initialized
    /// </summary>
    None,

    /// <summary>
    ///     A single node
    /// </summary>
    HomeAlone,

    /// <summary>
    ///     A node that follows a leader in the cluster
    /// </summary>
    Follower,

    /// <summary>
    ///     Candidate leader waiting for confirmation from all nodes that are alive
    /// </summary>
    Candidate,

    /// <summary>
    ///     Confirmed leader
    /// </summary>
    Leader
}