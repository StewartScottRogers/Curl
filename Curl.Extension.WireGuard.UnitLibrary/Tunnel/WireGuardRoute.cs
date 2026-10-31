using System.Net;
using Curl.Extension.WireGuard.Configuration;

namespace Curl.Extension.WireGuard.Tunnel;

/// <summary>Cryptokey routing (whitepaper section 2): which peer a destination goes to, and from which interface address.</summary>
internal static class WireGuardRoute
{
    /// <summary>Gives the peer whose <c>AllowedIPs</c> hold <paramref name="destination" /> most specifically, among those with an <c>Endpoint</c>.</summary>
    /// <param name="peers">The configuration's peers.</param>
    /// <param name="destination">Where the packet goes.</param>
    /// <returns>The peer, or <see langword="null" /> when none routes it.</returns>
    public static WireGuardPeer? PeerFor(IReadOnlyList<WireGuardPeer> peers, IPAddress destination)
    {
        WireGuardPeer? best = null;
        int bestPrefixLength = -1;
        foreach (WireGuardPeer peer in peers.Where(peer => peer.Endpoint is not null))
        {
            foreach (WireGuardCidr range in peer.AllowedIps.Where(range => range.PrefixLength > bestPrefixLength && Contains(range, destination)))
            {
                best = peer;
                bestPrefixLength = range.PrefixLength;
            }
        }

        return best;
    }

    /// <summary>Gives the first interface <c>Address</c> of <paramref name="destination" />'s family.</summary>
    /// <param name="addresses">The interface's addresses.</param>
    /// <param name="destination">Where the packet goes.</param>
    /// <returns>The source address, or <see langword="null" /> when the interface has none of that family.</returns>
    public static IPAddress? SourceFor(IReadOnlyList<WireGuardCidr> addresses, IPAddress destination) =>
        addresses.FirstOrDefault(address => address.Address.AddressFamily == destination.AddressFamily)?.Address;

    /// <summary>Gives whether <paramref name="address" /> is inside <paramref name="range" />.</summary>
    /// <param name="range">The range.</param>
    /// <param name="address">The address.</param>
    /// <returns>Whether its first <see cref="WireGuardCidr.PrefixLength" /> bits match.</returns>
    public static bool Contains(WireGuardCidr range, IPAddress address)
    {
        if (range.Address.AddressFamily != address.AddressFamily)
        {
            return false;
        }

        byte[] prefix = range.Address.GetAddressBytes();
        byte[] candidate = address.GetAddressBytes();
        int index = 0;
        for (int bits = range.PrefixLength; bits > 0; bits -= 8, index++)
        {
            int mask = bits >= 8 ? 0xFF : (0xFF00 >> bits) & 0xFF;
            if (((prefix[index] ^ candidate[index]) & mask) != 0)
            {
                return false;
            }
        }

        return true;
    }
}
