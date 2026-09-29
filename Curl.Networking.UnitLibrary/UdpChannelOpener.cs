using System.Net;

using Curl.Protocol.Abstractions;

namespace Curl.Networking;

/// <summary>
/// The production <see cref="IUdpChannelOpener" />: opens a <see cref="UdpDatagramChannel" />
/// bound to the local address and port <c>--interface</c> and <c>--local-port</c> give, or to
/// any address on an ephemeral port when they are not given.
/// </summary>
/// <param name="localAddress">The local address to bind, or <see langword="null" /> for any address of the server's family.</param>
/// <param name="localPort">The local port to bind, or <c>0</c> for an ephemeral one.</param>
public sealed class UdpChannelOpener(IPAddress? localAddress = null, int localPort = 0) : IUdpChannelOpener
{
    /// <inheritdoc />
    public IDatagramChannel Open(IPEndPoint serverEndPoint) => new UdpDatagramChannel(serverEndPoint, localAddress, localPort);
}
