namespace Napoleon.Server.Messages;

/// <summary>
///     Implemented by all message types
/// </summary>
public interface IAsRawBytes
{
    byte[] ToRawBytes();
    void FromRawBytes(byte[] bytes);
}