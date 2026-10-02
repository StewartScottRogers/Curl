using System.Buffers.Binary;
using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Text;

using Curl.Protocol.Abstractions;

namespace Curl.Networking;

/// <summary>
/// The SOCKS4 and SOCKS4a handshake as curl 8.21.0 runs it (measured): one CONNECT request
/// of <c>04 01</c>, the port, the IPv4 address, the user ID and a zero byte, then SOCKS4a's
/// host name and a zero byte; one eight-byte reply.
/// </summary>
/// <remarks>
/// SOCKS4 resolves the host locally and sends its first IPv4 address. SOCKS4a sends
/// <c>0.0.0.1</c> and the host as written, an address literal included, for the proxy to
/// resolve. The user ID is the credential's user name; the password is never sent.
/// </remarks>
internal static class Socks4Handshake
{
    /// <summary>The most bytes a SOCKS4 user ID may hold (measured: 256 is refused).</summary>
    public const int MaximumUserIdBytes = 255;

    /// <summary>The most bytes a SOCKS4a host name may hold (measured: 255 is refused).</summary>
    public const int MaximumHostBytes = 254;

    private const byte RequestGranted = 0x5A;

    /// <summary>
    /// Runs the handshake.
    /// </summary>
    /// <param name="connection">The connection to the proxy.</param>
    /// <param name="proxy">The SOCKS4 or SOCKS4a proxy.</param>
    /// <param name="host">The host the tunnel reaches.</param>
    /// <param name="port">The port the tunnel reaches.</param>
    /// <param name="resolve">Resolves the host for SOCKS4.</param>
    /// <param name="cancellationToken">Cancels the handshake.</param>
    /// <returns><see langword="null" /> when the tunnel is open, else the failure.</returns>
    public static async ValueTask<ConnectResult?> RunAsync(
        IConnection connection,
        ProxyEndpoint proxy,
        string host,
        int port,
        Func<string, int, CancellationToken, ValueTask<DnsResolution>> resolve,
        CancellationToken cancellationToken)
    {
        var userId = Encoding.UTF8.GetBytes(proxy.Credential?.UserName ?? string.Empty);
        if (userId.Length > MaximumUserIdBytes)
        {
            return SocksProxyTunnel.Failed("Too long SOCKS proxy username");
        }

        var (destination, failure) = proxy.Kind == ProxyKind.Socks4a
            ? RemotelyResolvedDestination(host)
            : await LocallyResolvedDestinationAsync(host, port, resolve, cancellationToken).ConfigureAwait(false);
        if (failure is not null)
        {
            return failure;
        }

        await SocksProxyTunnel.SendAsync(connection, BuildRequest(port, destination, userId), cancellationToken).ConfigureAwait(false);
        var reply = new byte[8];
        return await SocksProxyTunnel.ReadExactlyAsync(connection, reply, cancellationToken).ConfigureAwait(false)
            ? CheckReply(reply)
            : SocksProxyTunnel.Failed(SocksProxyTunnel.ProxyClosedMessage);
    }

    // SOCKS4a names the host after the user ID and asks for 0.0.0.1, which tells the proxy to resolve it.
    private static (Socks4Destination Destination, ConnectResult? Failure) RemotelyResolvedDestination(string host)
    {
        var hostBytes = Encoding.UTF8.GetBytes(host);
        return hostBytes.Length > MaximumHostBytes
            ? (default, SocksProxyTunnel.Failed("SOCKS4: too long hostname"))
            : (new Socks4Destination([0, 0, 0, 1], hostBytes), null);
    }

    private static async ValueTask<(Socks4Destination Destination, ConnectResult? Failure)> LocallyResolvedDestinationAsync(
        string host,
        int port,
        Func<string, int, CancellationToken, ValueTask<DnsResolution>> resolve,
        CancellationToken cancellationToken)
    {
        var (addresses, failure) = SocksProxyTunnel.ParseAddressLiteral(host) is { } literal
            ? new DnsResolution([literal], DnsLookupFailure.None)
            : await resolve(host, port, cancellationToken).ConfigureAwait(false);
        if (addresses.Count == 0)
        {
            return (default, SocksProxyTunnel.CouldNotResolve(host, port, failure));
        }

        return addresses.FirstOrDefault(address => address.AddressFamily == AddressFamily.InterNetwork) is { } ipv4
            ? (new Socks4Destination(ipv4.GetAddressBytes(), null), null)
            : (default, SocksProxyTunnel.Failed($"SOCKS4 connection to {addresses[0]} not supported"));
    }

    private static byte[] BuildRequest(int port, Socks4Destination destination, byte[] userId)
    {
        var request = new List<byte> { 4, 1, (byte)(port >> 8), (byte)port };
        request.AddRange(destination.Address);
        request.AddRange(userId);
        request.Add(0);
        if (destination.HostName is { } hostName)
        {
            request.AddRange(hostName);
            request.Add(0);
        }

        return [.. request];
    }

    private static ConnectResult? CheckReply(byte[] reply)
    {
        if (reply[0] != 0)
        {
            return SocksProxyTunnel.Failed("SOCKS4 reply has wrong version, version should be 0.");
        }

        if (reply[1] == RequestGranted)
        {
            return null;
        }

        // The address and port are the reply's own, as curl prints them.
        var boundPort = BinaryPrimitives.ReadUInt16BigEndian(reply.AsSpan(2));
        return SocksProxyTunnel.Failed(string.Create(
            CultureInfo.InvariantCulture,
            $"[SOCKS] cannot complete SOCKS4 connection to {reply[4]}.{reply[5]}.{reply[6]}.{reply[7]}:{boundPort}. ({reply[1]}), {RejectionReason(reply[1])}"));
    }

    private static string RejectionReason(byte code) => code switch
    {
        0x5B => "request rejected or failed.",
        0x5C => "request rejected because SOCKS server cannot connect to identd on the client.",
        0x5D => "request rejected because the client program and identd report different user-ids.",
        _ => "Unknown.",
    };

    // The four address bytes of the request, and SOCKS4a's host name or null.
    private readonly record struct Socks4Destination(byte[] Address, byte[]? HostName);
}
