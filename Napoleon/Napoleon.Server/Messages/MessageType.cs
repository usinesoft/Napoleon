using System.Runtime.Serialization.Json;
using System.Text.Json.Serialization;

namespace Napoleon.Server.Messages;

public enum MessageType
{
    None,
    Heartbeat,
    DataSync
}