using System.Net;
using System.Net.Sockets;

namespace Curl.Core;

/// <summary>
/// The host a connection goes to, as libcurl's <c>Curl_peer_create</c> (<c>lib/peer.c</c>, curl 8.22.0) makes a
/// <c>struct Curl_peer</c> of a host string: the string as given, the host name to resolve, the port, whether the
/// host is an IPv6 address, and its zone id. An IPv6 address may be written with or without brackets and with a
/// <c>%zone</c> suffix; the brackets and the zone come off the host name. Any other host keeps its text whole,
/// a trailing dot included.
/// </summary>
/// <param name="Scheme">The URL scheme the connection is for.</param>
/// <param name="UserHostname">The host string as given (<c>user_hostname</c>).</param>
/// <param name="Hostname">The host name to resolve: an IPv6 address without brackets or zone.</param>
/// <param name="Port">The port.</param>
/// <param name="IsIpv6">Whether the host is an IPv6 address.</param>
/// <param name="ZoneId">The IPv6 zone id after <c>%</c>, or <see langword="null" /> when there is none.</param>
internal sealed record ConnectionPeer(string Scheme, string UserHostname, string Hostname, int Port, bool IsIpv6, string? ZoneId)
{
    /// <summary>Makes the peer of <paramref name="userHostname" /> as <c>Curl_peer_create</c> does.</summary>
    /// <param name="scheme">The URL scheme the connection is for.</param>
    /// <param name="userHostname">The host string, an IPv6 address bracketed or not.</param>
    /// <param name="port">The port.</param>
    /// <returns>The peer.</returns>
    internal static ConnectionPeer Create(string scheme, string userHostname, int port)
    {
        string unbracketed = userHostname.StartsWith('[') && userHostname.EndsWith(']') ? userHostname[1..^1] : userHostname;
        int percent = unbracketed.IndexOf('%');
        string address = percent < 0 ? unbracketed : unbracketed[..percent];
        return IPAddress.TryParse(address, out IPAddress? parsed) && parsed.AddressFamily == AddressFamily.InterNetworkV6
            ? new ConnectionPeer(scheme, userHostname, address, port, true, percent < 0 ? null : unbracketed[(percent + 1)..])
            : new ConnectionPeer(scheme, userHostname, userHostname, port, false, null);
    }
}
