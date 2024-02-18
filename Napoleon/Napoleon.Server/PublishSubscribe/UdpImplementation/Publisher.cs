using System.Net;
using System.Net.Sockets;
using Napoleon.Server.Messages;

namespace Napoleon.Server.PublishSubscribe.UdpImplementation;

public sealed class Publisher : IPublisher
{
    private IPAddress GroupAddress { get; }

    private int GroupPort { get; }

    private readonly UdpClient _udpClient;

    public Publisher(string groupAddress, int groupPort)
    {
        GroupAddress = IPAddress.Parse(groupAddress);

        GroupPort = groupPort;

        _udpClient = new(0, AddressFamily.InterNetwork);

        _udpClient.JoinMulticastGroup(IPAddress.Parse(groupAddress));
    }

    public void Publish(MessageHeader message)
    {
        var data = message.ToRawBytes();

        var endpoint = new IPEndPoint(GroupAddress, GroupPort);
        _udpClient.Send(data, endpoint);
    }

    public void Dispose()
    {
        _udpClient.Dispose();
    }
}