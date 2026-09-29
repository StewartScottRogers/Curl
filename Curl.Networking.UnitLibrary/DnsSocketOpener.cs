using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Net.Sockets;

using Curl.Protocol.Abstractions;

namespace Curl.Networking;

/// <summary>
/// The production <see cref="IDnsSocketOpener" />: a <see cref="UdpDatagramChannel" /> for a UDP
/// query, and a bound, connected TCP <see cref="Socket" /> wrapped in a <see cref="NetworkStream" />
/// for a query retried over TCP.
/// </summary>
public sealed class DnsSocketOpener : IDnsSocketOpener
{
    /// <inheritdoc />
    public IDatagramChannel OpenDatagramChannel(IPEndPoint server, IPAddress localAddress) =>
        new UdpDatagramChannel(server, localAddress);

    /// <inheritdoc />
    /// <remarks>
    /// Excluded from coverage per ADR-0083: connecting needs a listening server, so the loopback
    /// round trip in the Integration run measures it.
    /// </remarks>
    [ExcludeFromCodeCoverage(Justification = "ADR-0083: a thin socket adapter, measured by the Integration run.")]
    public async ValueTask<Stream> ConnectStreamAsync(IPEndPoint server, IPAddress localAddress, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(server);

        var socket = new Socket(server.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
        try
        {
            socket.Bind(new IPEndPoint(localAddress, 0));
            await socket.ConnectAsync(server, cancellationToken).ConfigureAwait(false);
            return new NetworkStream(socket, ownsSocket: true);
        }
        catch
        {
            socket.Dispose();
            throw;
        }
    }
}
