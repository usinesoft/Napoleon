using System.Runtime.CompilerServices;
using System.Text;

namespace Napoleon.Server.PublishSubscribe.TcpImplementation;

/// <summary>
///     We assume every stream contains a sequence of objects, each object is prefixed by its size
/// </summary>
public static class StreamingHelper
{
    public static async Task<byte[]> ReadDataAsync(this Stream stream, int dataSize, CancellationToken ct = default)
    {
        var result = new byte[dataSize];

        var offset = 0;

        while (offset < dataSize)
        {
            var read = await stream.ReadAsync(result, offset, dataSize - offset, ct);
            offset += read;
        }

        return result;
    }


    public static async Task<int> ReadIntAsync(this Stream stream, CancellationToken ct)
    {
        var buffer = await stream.ReadDataAsync(sizeof(int), ct);

        return BitConverter.ToInt32(buffer);
    }

    public static async Task<string> ReadStringAsync(this Stream stream, CancellationToken ct = default)
    {
        var size = await stream.ReadIntAsync(ct);
        var data = await stream.ReadDataAsync(size, ct);

        return Encoding.UTF8.GetString(data);
    }

    public static async Task WriteIntAsync(this Stream stream, int value, CancellationToken ct = default)
    {
        await stream.WriteAsync(BitConverter.GetBytes(value), ct);
    }

    public static async Task WriteStringAsync(this Stream stream, string value, CancellationToken ct = default)
    {
        var data = Encoding.UTF8.GetBytes(value);
        await stream.WriteIntAsync(data.Length, ct);
        await stream.WriteAsync(data, ct);
    }

    /// <summary>
    ///     Read a sequence prefixed by the item count
    /// </summary>
    /// <param name="stream"></param>
    /// <param name="ct"></param>
    /// <returns></returns>
    public static async IAsyncEnumerable<byte[]> FromStreamWithPrefixAsync(this Stream stream,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        var count = await stream.ReadIntAsync(ct);
        for (var i = 0; i < count; i++)
        {
            var size = await stream.ReadIntAsync(ct);
            var data = await stream.ReadDataAsync(size, ct);

            yield return data;
        }
    }


    /// <summary>
    ///     Read a sequence that ends with a zero size item
    /// </summary>
    /// <param name="stream"></param>
    /// <param name="ct"></param>
    /// <returns></returns>
    public static async IAsyncEnumerable<byte[]> FromStreamWithSuffixAsync(this Stream stream,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        while (true)
        {
            var size = await stream.ReadIntAsync(ct);
            if (size == 0) yield break;


            var data = await stream.ReadDataAsync(size, ct);

            yield return data;
        }
    }
}