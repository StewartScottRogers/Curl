using System.Net;
using System.Net.NetworkInformation;

namespace Curl.Networking;

/// <summary>
/// The system's DNS servers, which <see cref="DnsServerResolver" /> queries when
/// <c>--dns-interface</c>, <c>--dns-ipv4-addr</c> or <c>--dns-ipv6-addr</c> is given without
/// <c>--dns-servers</c>, as c-ares reads them from the system configuration.
/// </summary>
public static class SystemDnsServers
{
    /// <summary>
    /// Lists the DNS servers of every interface that is up, once each, on port 53, leaving out the
    /// deprecated site-local <c>fec0:0:0:ffff::1</c>-style defaults Windows lists on adapters that
    /// have no IPv6 DNS server, which never answer.
    /// </summary>
    /// <returns>The servers, in the system's interface order.</returns>
    public static IReadOnlyList<IPEndPoint> List() =>
        NetworkInterface.GetAllNetworkInterfaces()
            .Where(networkInterface => networkInterface.OperationalStatus == OperationalStatus.Up)
            .SelectMany(networkInterface => networkInterface.GetIPProperties().DnsAddresses)
            .Where(address => !address.IsIPv6SiteLocal)
            .Distinct()
            .Select(address => new IPEndPoint(address, DnsServerList.DefaultPort))
            .ToArray();
}
