using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

// ReSharper disable ClassNeverInstantiated.Global

namespace Napoleon.Server.SharedData;

[JsonSerializable(typeof(Item))]
[JsonSerializable(typeof(Item[]))]
[JsonSerializable(typeof(NodeStatus))]
[JsonSerializable(typeof(NodeStatus[]))]
[JsonSerializable(typeof(JsonDocument))]
[JsonSerializable(typeof(JsonObject))]
[JsonSerializable(typeof(JsonElement))]
[JsonSerializable(typeof(JsonNode))]
public partial class SerializationContext : JsonSerializerContext
{
}