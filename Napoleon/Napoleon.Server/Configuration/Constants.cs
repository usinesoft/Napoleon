namespace Napoleon.Server.Configuration;

public static class Constants
{
    public static readonly string Tcp = "TCP";
    public static readonly string Udp = "UDP";

    public static readonly string DefaultMulticastAddress = "224.101.102.103";

    public static readonly int DefaultMulticastPort = 50501;

    public static readonly int DefaultHeartbeatPeriodInMilliseconds = 500;

    public static readonly double HearbeatsLostBeforeDeath = 2.2;
}