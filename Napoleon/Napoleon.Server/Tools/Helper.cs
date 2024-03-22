using System.Net;
using System.Net.Sockets;

namespace Napoleon.Server.Tools;

public static class Helper
{
    private static readonly Random Rand = new();

    /// <summary>
    ///     Pick a random item from the subset of a collection that matches a condition (by default all)
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="all"></param>
    /// <param name="condition"></param>
    /// <returns></returns>
    public static T? SelectRandom<T>(this IEnumerable<T> all, Predicate<T>? condition = null) where T : class
    {
        condition ??= _ => true;

        var matches = all.Where(x => condition(x)).ToArray();

        if (matches.Length == 0)
            return null;

        if (matches.Length == 1)
            return matches[0];

        return matches[Rand.Next(matches.Length)];
    }
}

public record NormalizedAddress(string Host, int Port)
{
    /// <summary>
    ///     Create from host:port string host will be converted to public ip address
    /// </summary>
    /// <param name="hostAndPort"></param>
    /// <returns></returns>
    public static NormalizedAddress FromString(string hostAndPort)
    {
        var parts = hostAndPort.Split(':', StringSplitOptions.RemoveEmptyEntries);

        if (parts.Length != 2) throw new ArgumentException($"Invalid server {hostAndPort}");

        var host = parts[0];

        var isIpAddress = IPAddress.TryParse(host, out var ip) && ip.AddressFamily == AddressFamily.InterNetwork;

        if (!isIpAddress)
        {
            var entry = Dns.GetHostEntry(host);
            if (entry.AddressList.Length == 0)
                throw new ArgumentException($"Can not resolve host specification:{host}");
            ip = entry.AddressList[0].MapToIPv4();
        }


        if (!int.TryParse(parts[1], out var port))
            throw new ArgumentException($"Port must be an integer in {hostAndPort}");


        return new(ip!.ToString(), port);
    }
}