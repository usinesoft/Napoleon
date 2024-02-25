namespace Napoleon.Server.Configuration;

/// <summary>
/// How to allocate the unique node id 
/// </summary>
public enum NodeIdPolicy
{
    /// <summary>
    /// Automatically generate one at runtime
    /// </summary>
    Guid,
    /// <summary>
    /// Name in config file
    /// </summary>
    ExplicitName,
    /// <summary>
    /// ip_address:port
    /// </summary>
    ImplicitIpAndPort
}