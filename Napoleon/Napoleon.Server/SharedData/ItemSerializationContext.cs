using System.Text.Json.Serialization;
// ReSharper disable ClassNeverInstantiated.Global

namespace Napoleon.Server.SharedData;

[JsonSerializable(typeof(Item))]
[JsonSerializable(typeof(NodeStatus))]
[JsonSerializable(typeof(NodeStatus[]))]
public partial class ItemSerializationContext : JsonSerializerContext
{

}