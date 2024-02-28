using System.Runtime.CompilerServices;

namespace Napoleon.Server.PublishSubscribe.TcpImplementation
{
    /// <summary>
    /// We assume every stream contains a sequence of objects, each object is prefixed by its size
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

        private static byte[] ReadData(this Stream stream, int dataSize)
        {
            var result = new byte[dataSize];

            if (stream.Read(result, 0, dataSize) == dataSize)
            {
                return result;
            }

            throw new NotSupportedException("Error reading data");

        }

        public static int ReadInt(this Stream stream)
        {
            byte[] buffer = new byte[sizeof(int)];

            var bytesRead = stream.Read(buffer, 0, sizeof(int));
            if (bytesRead != sizeof(int)) throw new NotSupportedException("Can not read int value from stream");

            return BitConverter.ToInt32(buffer);
        }

        public static async Task<int> ReadIntAsync(this Stream stream, CancellationToken ct)
        {
            var buffer = await stream.ReadDataAsync(sizeof(int), ct);

            return BitConverter.ToInt32(buffer);
        }

        public static async Task WriteIntAsync(this Stream stream, int value, CancellationToken ct = default)
        {
            await stream.WriteAsync(BitConverter.GetBytes(value), ct);
        }
        
        /// <summary>
        /// Read a sequence prefixed by the item count 
        /// </summary>
        /// <param name="stream"></param>
        /// <param name="ct"></param>
        /// <returns></returns>
        public static async IAsyncEnumerable<byte[]> FromStreamWithPrefixAsync(this Stream stream, [EnumeratorCancellation]CancellationToken ct = default)
        {
            
            var count = await stream.ReadIntAsync(ct);
            for (int i = 0; i < count; i++)
            {
                var size = await stream.ReadIntAsync(ct);
                var data = await stream.ReadDataAsync(size, ct);

                yield return data;
            }
            
        }

        public static IEnumerable<byte[]> FromStreamWithPrefix(this Stream stream, CancellationToken ct = default)
        {
            
            var count = stream.ReadInt();
            for (int i = 0; i < count; i++)
            {
                var size = stream.ReadInt();
                var data = stream.ReadData(size);

                yield return data;
            }
            
        }

        /// <summary>
        /// Read a sequence that ends with a zero size item
        /// </summary>
        /// <param name="stream"></param>
        /// <param name="ct"></param>
        /// <returns></returns>
        public static async IAsyncEnumerable<byte[]> FromStreamWithSuffixAsync(this Stream stream, [EnumeratorCancellation]CancellationToken ct = default)
        {
            while(true)
            {
                var size = await stream.ReadIntAsync(ct);
                if(size == 0)
                    yield break;

                var data = await stream.ReadDataAsync(size, ct);

                yield return data;
            }
            
        }

        public static async Task ToStreamAsyncWithPrefix(this Stream stream, byte[] data, CancellationToken ct = default)
        {
            // items count
            await stream.WriteAsync(BitConverter.GetBytes(1), ct);
            // item size
            await stream.WriteAsync(BitConverter.GetBytes(data.Length), ct);
            // item
            await stream.WriteAsync(data, ct);
        }

        public static async Task ToStreamAsyncWithSuffix(this Stream stream, byte[] data, CancellationToken ct = default)
        {
            // item size
            await stream.WriteAsync(BitConverter.GetBytes(data.Length), ct);
            // item
            await stream.WriteAsync(data, ct);
            // end of sequence
            await stream.WriteAsync(BitConverter.GetBytes(0), ct);
        }

        public static async Task SequenceToStreamAsync(this Stream stream, IReadOnlyCollection<byte[]> data, CancellationToken ct = default)
        {
            // items count
            await stream.WriteAsync(BitConverter.GetBytes(data.Count), ct);

            foreach (var item in data)
            {
                await stream.WriteAsync(BitConverter.GetBytes(item.Length), ct);
                await stream.WriteAsync(item, ct);
            }
            
        }
    }
}
