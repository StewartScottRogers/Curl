using System.Net;
using System.Net.Sockets;

using Curl.Networking;
using Curl.Protocol.Abstractions;

namespace Curl.Engine;

/// <summary>
/// The dialer of a transfer in a multi handle driven by <c>curl_multi_socket_action</c>: while a dial runs the
/// transfer waits writable (<see cref="TransferSocketWait.BeginConnect" />) on the socket the inner dialer publishes as it
/// creates it (<see cref="TransferSocketWait.ConnectingOn" />, BL-3890), a dial that connects ends the connect
/// (<see cref="TransferSocketWait.EndConnect" />) after the transfer takes its socket, an engine number for a connection with no
/// operating system socket (<see cref="TransferSocketWait.TakeSocket" />, ADR-0955), and each dialed connection is wrapped, beneath TLS, in a
/// <see cref="SocketActionGatedConnection" />, so its reads and writes wait for the multi handle's signal
/// (ADR-0917, BL-3859). Every other member is the inner dialer's.
/// </summary>
/// <remarks>
/// A dial that fails leaves the transfer connecting, as libcurl's does while it tries the next address; the
/// transfer's end is what ends its wait then. A pooled connection a later transfer reuses keeps the gate of the
/// transfer that dialed it, as the multi handle signals by socket.
/// </remarks>
/// <param name="inner">The dialer that opens the sockets.</param>
/// <param name="wait">What the transfer waits for on its socket.</param>
internal sealed class SocketActionGatedTcpDialer(ITcpDialer inner, TransferSocketWait wait) : ITcpDialer
{
    private readonly ITcpDialer dialer = inner.HoldingFailedSocketsUntil(wait.HoldFailedConnectAsync).PublishingSocketsTo(wait.ConnectingOn);

    /// <summary>
    /// Gives <paramref name="dialer" /> gated on <paramref name="wait" />, or <paramref name="dialer" /> itself
    /// outside socket-action mode (<paramref name="wait" /> <see langword="null" />).
    /// </summary>
    /// <param name="dialer">The dialer that opens the sockets.</param>
    /// <param name="wait">What the transfer waits for on its socket, or <see langword="null" />.</param>
    /// <returns>The dialer the transfer dials through.</returns>
    internal static ITcpDialer Over(ITcpDialer dialer, TransferSocketWait? wait) =>
        wait is null ? dialer : new SocketActionGatedTcpDialer(dialer, wait);

    /// <inheritdoc />
    public ValueTask<DialedTcpConnection> DialAsync(IPEndPoint endPoint, CancellationToken cancellationToken) =>
        GatedAsync(() => dialer.DialAsync(endPoint, cancellationToken));

    /// <inheritdoc />
    public ValueTask<DialedTcpConnection> DialFromAsync(IPEndPoint endPoint, IPEndPoint localEndPoint, int localPortCount, CancellationToken cancellationToken) =>
        GatedAsync(() => dialer.DialFromAsync(endPoint, localEndPoint, localPortCount, cancellationToken));

    /// <inheritdoc />
    public ValueTask<DialedTcpConnection> DialFromAsync(IPEndPoint endPoint, IPEndPoint localEndPoint, int localPortCount, ITransferEvents events, CancellationToken cancellationToken) =>
        GatedAsync(() => dialer.DialFromAsync(endPoint, localEndPoint, localPortCount, events, cancellationToken));

    /// <inheritdoc />
    public ValueTask<DialedTcpConnection> DialFromDeviceAsync(
        IPEndPoint endPoint,
        string deviceName,
        bool bindsAddressAfterDevice,
        Func<CancellationToken, ValueTask<IPEndPoint>> chooseLocalEndAsync,
        int localPortCount,
        ITransferEvents events,
        CancellationToken cancellationToken) =>
        GatedAsync(() => dialer.DialFromDeviceAsync(endPoint, deviceName, bindsAddressAfterDevice, chooseLocalEndAsync, localPortCount, events, cancellationToken));

    /// <inheritdoc />
    public async ValueTask<IConnection> DialUnixSocketAsync(UnixSocketAddress address, CancellationToken cancellationToken)
    {
        wait.BeginConnect();
        IConnection connection = await dialer.DialUnixSocketAsync(address, cancellationToken).ConfigureAwait(false);
        wait.TakeSocket(connection.SocketHandle);
        wait.EndConnect();
        return new SocketActionGatedConnection(connection, wait);
    }

    /// <inheritdoc />
    public SocketException? FailureToOpenSocket(AddressFamily family) => inner.FailureToOpenSocket(family);

    private async ValueTask<DialedTcpConnection> GatedAsync(Func<ValueTask<DialedTcpConnection>> dialAsync)
    {
        wait.BeginConnect();
        DialedTcpConnection dialed = await dialAsync().ConfigureAwait(false);
        wait.TakeSocket(dialed.Connection.SocketHandle);
        wait.EndConnect();
        return new DialedTcpConnection(new SocketActionGatedConnection(dialed.Connection, wait), dialed.LocalEndPoint);
    }
}
