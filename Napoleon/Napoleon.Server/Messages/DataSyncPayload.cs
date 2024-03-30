using System.Text.Json;
using Napoleon.Server.SharedData;

namespace Napoleon.Server.Messages;

public class DataSyncPayload : IAsRawBytes
{
    public IList<Item> Items { get; init; } = new List<Item>();

    public byte[] ToRawBytes()
    {
        var stream = new MemoryStream();
        var writer = new BinaryWriter(stream);
        writer.Write(Items.Count);
        foreach (var item in Items)
        {
            item.CheckValid();
            writer.Write(item.Collection!);
            writer.Write(item.Key!);
            writer.Write(item.Version);
            writer.Write(item.IsDeleted);
            if (!item.IsDeleted)
            {
                var json = JsonSerializer.Serialize(item.Value, SerializationContext.Default.JsonElement);
                writer.Write(json);
            }
        }

        return stream.ToArray();
    }

    public void FromRawBytes(byte[] bytes)
    {
        var stream = new MemoryStream(bytes);
        var reader = new BinaryReader(stream);
        var count = reader.ReadInt32();

        for (var i = 0; i < count; i++)
        {
            var item = new Item
            {
                Collection = reader.ReadString(),
                Key = reader.ReadString(),
                Version = reader.ReadInt32(),
                IsDeleted = reader.ReadBoolean()
            };

            if (!item.IsDeleted)
            {
                var json = reader.ReadString();
                item.Value = JsonSerializer.Deserialize(json, SerializationContext.Default.JsonElement);
            }
            else // put null on deleted items as 'Undefined' is not serializable
            {
                item.Value = JsonSerializer.Deserialize("null", SerializationContext.Default.JsonElement);
            }

            Items.Add(item);
        }
    }
}