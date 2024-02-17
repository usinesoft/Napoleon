using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Napoleon.Server.Messages
{

    public enum MessageType
    {
        None,
        Heartbeat
    }

    /// <summary>
    /// Implemented by all message types
    /// </summary>
    public interface IAsRawBytes
    {
        byte[] ToRawBytes();
        void FromRawBytes(byte[] bytes);

    }

    /// <summary>
    /// The same header for all message types
    /// </summary>
    public class MessageHeader:IAsRawBytes
    {
        /// <summary>
        /// As unique as possible for a reasonable size. Used to avoid duplicated messages in most cases.
        /// </summary>
        public int MessageId { get; set;}
        
        public string? Cluster { get; set;}
        
        /// <summary>
        /// Unique identifier inside a cluster
        /// </summary>
        public string? SenderNode { get; set;}


        public MessageType MessageType { get; set;}

        /// <summary>
        /// Optional payload size (zero for heartbeat messages)
        /// </summary>
        public uint PayloadSize { get; set; }

        public override string ToString()
        {
            return $"{nameof(MessageId)}: {MessageId}, {nameof(Cluster)}: {Cluster}, {nameof(SenderNode)}: {SenderNode}, {nameof(MessageType)}: {MessageType}, {nameof(PayloadSize)}: {PayloadSize}";
        }

        public byte[] ToRawBytes()
        {
            var stream = new MemoryStream();
            var writer = new BinaryWriter(stream);
            
            writer.Write(MessageId);
            writer.Write(Cluster ?? string.Empty);
            writer.Write(SenderNode ?? string.Empty);
            writer.Write((int)MessageType);
            writer.Write(PayloadSize);
            writer.Flush();
            
            return stream.ToArray();
        }

        public void FromRawBytes(byte[] bytes)
        {
            var stream = new MemoryStream(bytes);
            var reader = new BinaryReader(stream);

            MessageId =  reader.ReadInt32();
            Cluster = reader.ReadString();
            SenderNode = reader.ReadString();
            MessageType =  (MessageType)reader.ReadInt32();
            PayloadSize = reader.ReadUInt32();
        }
    }
}
