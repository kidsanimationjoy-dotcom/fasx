using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace Fasx.Agent.Windows.Networking;

public static class LocalNetworkAddressProvider
{
    public static IReadOnlyList<IPAddress> GetPrivateIpv4Addresses()
    {
        var candidates = new List<(int Priority, IPAddress Address)>();

        foreach (var network in NetworkInterface.GetAllNetworkInterfaces())
        {
            if (network.OperationalStatus != OperationalStatus.Up ||
                network.NetworkInterfaceType is NetworkInterfaceType.Loopback or NetworkInterfaceType.Tunnel)
            {
                continue;
            }

            var priority = network.NetworkInterfaceType == NetworkInterfaceType.Wireless80211 ? 0 : 1;

            foreach (var unicast in network.GetIPProperties().UnicastAddresses)
            {
                var address = unicast.Address;
                if (address.AddressFamily == AddressFamily.InterNetwork && IsPrivateIpv4(address))
                {
                    candidates.Add((priority, address));
                }
            }
        }

        return candidates
            .OrderBy(candidate => candidate.Priority)
            .ThenBy(candidate => candidate.Address.ToString(), StringComparer.Ordinal)
            .Select(candidate => candidate.Address)
            .Distinct()
            .ToArray();
    }

    private static bool IsPrivateIpv4(IPAddress address)
    {
        var bytes = address.GetAddressBytes();

        if (bytes.Length != 4 || IPAddress.IsLoopback(address))
        {
            return false;
        }

        return bytes[0] == 10 ||
               (bytes[0] == 172 && bytes[1] is >= 16 and <= 31) ||
               (bytes[0] == 192 && bytes[1] == 168);
    }
}
