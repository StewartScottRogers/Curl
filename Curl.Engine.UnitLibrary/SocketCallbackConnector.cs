using System.Globalization;
using System.Net;
using System.Net.Sockets;
using Curl.Api.Callbacks;
using Curl.Protocol.Abstractions;

namespace Curl.Engine;

/// <summary>
/// Calls a handle's <c>CURLOPT_OPENSOCKETFUNCTION</c>, <c>CURLOPT_SOCKOPTFUNCTION</c> and
/// <c>CURLOPT_CLOSESOCKETFUNCTION</c> around each TCP and QUIC connection <paramref name="inner" /> opens (ADR-0610). The
/// managed engine's sockets live inside <c>Curl.Networking</c> and are never handed out, so a socket the callbacks
/// see is an opaque <see cref="CurlSocket" /> over the <see cref="IConnection" />: the open-socket callback's, or
/// the engine's next <see cref="CurlSocketHandles" /> number without one. Once <paramref name="inner" /> has
/// connected, the open-socket callback is asked for the connection's peer address, and its
/// <see cref="CurlSocket.Bad" /> closes it and fails the connect with <see cref="CurlExitCode.CouldntConnect" />;
/// the socket-option callback then gets the socket, and its <see cref="SocketOptionResult.Error" /> closes it (the
/// close-socket callback called) and fails with <see cref="CurlExitCode.AbortedByCallback" />, each with curl
/// 8.22.0's <c>Failed to connect to &lt;host&gt;:&lt;port&gt; after &lt;n&gt; ms: &lt;text&gt;</c>;
/// <see cref="SocketOptionResult.AlreadyConnected" /> carries on as <see cref="SocketOptionResult.Ok" /> does, the
/// connection being made already. Closing the connection calls the close-socket callback once. A QUIC connection's
/// UDP socket goes through the same steps, the open-socket callback seeing <c>SOCK_DGRAM</c> and <c>IPPROTO_UDP</c>
/// (BL-2474).
/// </summary>
/// <param name="inner">Opens the connections.</param>
/// <param name="callbacks">The handle's socket callbacks.</param>
/// <param name="handles">Numbers the sockets the open-socket callback did not make.</param>
/// <param name="timeProvider">Measures the milliseconds a refused connect reports.</param>
internal sealed class SocketCallbackConnector(IConnector inner, EasyCallbackOptions callbacks, CurlSocketHandles handles, TimeProvider timeProvider) : IConnector
{
    /// <summary><c>curl_easy_strerror(CURLE_COULDNT_CONNECT)</c>, the end of an open-socket refusal's message.</summary>
    internal const string CouldntConnectText = "Could not connect to server";

    /// <summary><c>curl_easy_strerror(CURLE_ABORTED_BY_CALLBACK)</c>, the end of a socket-option refusal's message.</summary>
    internal const string AbortedByCallbackText = "Operation was aborted by an application callback";

    /// <summary><c>SOCK_STREAM</c>, 1 on every platform curl builds on.</summary>
    internal const int StreamSocketType = 1;

    /// <summary><c>IPPROTO_TCP</c>, 6 on every platform.</summary>
    internal const int TcpProtocol = 6;

    /// <summary><c>SOCK_DGRAM</c>, 2 on every platform curl builds on.</summary>
    internal const int DatagramSocketType = 2;

    /// <summary><c>IPPROTO_UDP</c>, 17 on every platform.</summary>
    internal const int UdpProtocol = 17;

    /// <summary>
    /// Gives <paramref name="connector" /> wrapped in a <see cref="SocketCallbackConnector" /> when
    /// <paramref name="callbacks" /> sets any socket callback, else <paramref name="connector" /> itself.
    /// </summary>
    /// <param name="connector">Opens the connections.</param>
    /// <param name="callbacks">The handle's callbacks.</param>
    /// <param name="shared">The engine's shared state, for its socket numbers and clock.</param>
    /// <returns>The connector the transfer connects through.</returns>
    internal static IConnector Over(IConnector connector, EasyCallbackOptions callbacks, ManagedEngineSharedState shared) =>
        callbacks is { OpenSocket: null, SocketOption: null, CloseSocket: null }
            ? connector
            : new SocketCallbackConnector(connector, callbacks, shared.SocketHandles, shared.TimeProvider);

    /// <summary>Connects through the inner connector, then runs the open-socket and socket-option callbacks.</summary>
    /// <param name="target">Where to connect.</param>
    /// <param name="cancellationToken">Cancels the connect.</param>
    /// <returns>The connection wrapped as a <see cref="SocketCallbackConnection" />, or the failure.</returns>
    public async ValueTask<ConnectResult> ConnectAsync(ConnectTarget target, CancellationToken cancellationToken)
    {
        long started = timeProvider.GetTimestamp();
        ConnectResult connect = await inner.ConnectAsync(target, cancellationToken).ConfigureAwait(false);
        if (connect.Connection is not { } connection)
        {
            return connect;
        }

        SocketOpening opening = await OpenSocketAsync(target, started, connection, AddressOf(connection.RemoteEndPoint)).ConfigureAwait(false);
        return opening.Closer is { } closer
            ? Rewrapped(connect, new SocketCallbackConnection(connection, closer))
            : ConnectResult.Failed(opening.ExitCode, opening.ErrorMessage);
    }

    /// <summary>
    /// Opens a QUIC connection through the inner connector, then runs the open-socket and socket-option callbacks for
    /// its UDP socket as <see cref="ConnectAsync" /> does for a TCP one, the open-socket callback seeing
    /// <c>SOCK_DGRAM</c> and <c>IPPROTO_UDP</c> (<see cref="DatagramAddressOf" />), as curl 8.22.0's
    /// <c>cf_socket_open</c> does for <c>cf-ngtcp2</c> (BL-2474).
    /// </summary>
    /// <param name="target">Where to connect.</param>
    /// <param name="cancellationToken">Cancels the connect.</param>
    /// <returns>The connection wrapped as a <see cref="SocketCallbackMultiplexedConnection" />, or the failure.</returns>
    public async ValueTask<MultiplexedConnectResult> ConnectMultiplexedAsync(ConnectTarget target, CancellationToken cancellationToken)
    {
        long started = timeProvider.GetTimestamp();
        MultiplexedConnectResult connect = await inner.ConnectMultiplexedAsync(target, cancellationToken).ConfigureAwait(false);
        if (connect.Connection is not { } connection)
        {
            return connect;
        }

        SocketOpening opening = await OpenSocketAsync(target, started, connection, DatagramAddressOf(connection.RemoteEndPoint)).ConfigureAwait(false);
        return opening.Closer is { } closer
            ? MultiplexedConnectResult.Connected(new SocketCallbackMultiplexedConnection(connection, closer), connect.Timings)
            : MultiplexedConnectResult.Failed(opening.ExitCode, opening.ErrorMessage);
    }

    /// <summary>
    /// Opens a QUIC session over <see cref="ConnectMultiplexedAsync" />'s connection, so the socket callbacks run once
    /// per QUIC connection; a <c>PoolingConnector</c> above this connector still shares the one session it gives.
    /// </summary>
    /// <param name="target">Where to connect.</param>
    /// <param name="openSession">Builds the session over a new QUIC connection.</param>
    /// <param name="cancellationToken">Cancels the connect.</param>
    /// <returns>The session as the connect result's connection, or the failure.</returns>
    public async ValueTask<ConnectResult> ConnectMultiplexedSessionAsync(ConnectTarget target, Func<IMultiplexedConnection, IConnection> openSession, CancellationToken cancellationToken) =>
        (await ConnectMultiplexedAsync(target, cancellationToken).ConfigureAwait(false)).ToConnectResult(openSession);

    /// <summary>
    /// The <c>struct curl_sockaddr</c> the open-socket callback is given for <paramref name="endPoint" />:
    /// <c>SOCK_STREAM</c> and <c>IPPROTO_TCP</c> (0 for a Unix socket), the family as the running platform numbers it
    /// (<see cref="FamilyNumberOf" />) and the end point's <see cref="SocketAddress" /> bytes; no end point is family 0
    /// and no bytes.
    /// </summary>
    /// <param name="endPoint">The connection's peer.</param>
    /// <returns>The address.</returns>
    internal static CurlSocketAddress AddressOf(EndPoint? endPoint) => AddressOf(endPoint, StreamSocketType, TcpProtocol);

    /// <summary>
    /// The <c>struct curl_sockaddr</c> the open-socket callback is given for a QUIC connection's UDP socket to
    /// <paramref name="endPoint" />: as <see cref="AddressOf(EndPoint?)" />, with <c>SOCK_DGRAM</c> and
    /// <c>IPPROTO_UDP</c>.
    /// </summary>
    /// <param name="endPoint">The connection's peer.</param>
    /// <returns>The address.</returns>
    internal static CurlSocketAddress DatagramAddressOf(EndPoint? endPoint) => AddressOf(endPoint, DatagramSocketType, UdpProtocol);

    private static CurlSocketAddress AddressOf(EndPoint? endPoint, int socketType, int ipProtocol)
    {
        if (endPoint is null)
        {
            return new CurlSocketAddress(0, socketType, ipProtocol, ReadOnlyMemory<byte>.Empty);
        }

        SocketAddress address = endPoint.Serialize();
        int protocol = endPoint.AddressFamily == AddressFamily.Unix ? 0 : ipProtocol;
        return new CurlSocketAddress(
            FamilyNumberOf(endPoint.AddressFamily, OperatingSystem.IsWindows(), OperatingSystem.IsMacOS()),
            socketType,
            protocol,
            address.Buffer[..address.Size].ToArray());
    }

    /// <summary>
    /// The platform's number for <paramref name="family" />: <c>AF_UNIX</c> 1 and <c>AF_INET</c> 2 everywhere, and
    /// <c>AF_INET6</c> 23 on Windows, 30 on macOS and 10 on Linux; any other family keeps .NET's number.
    /// </summary>
    /// <param name="family">The address family.</param>
    /// <param name="isWindows">Whether the platform is Windows.</param>
    /// <param name="isMacOs">Whether the platform is macOS.</param>
    /// <returns>The platform's number.</returns>
    internal static int FamilyNumberOf(AddressFamily family, bool isWindows, bool isMacOs) => family switch
    {
        AddressFamily.InterNetworkV6 when isWindows => 23,
        AddressFamily.InterNetworkV6 when isMacOs => 30,
        AddressFamily.InterNetworkV6 => 10,
        _ => (int)family,
    };

    // Runs the open-socket and socket-option callbacks for a connection inner has just made, closing it on a refusal.
    private async ValueTask<SocketOpening> OpenSocketAsync(ConnectTarget target, long started, IAsyncDisposable connection, CurlSocketAddress address)
    {
        CurlSocket socket = callbacks.OpenSocket is { } open
            ? open(CurlSocketType.IpConnection, address)
            : handles.Next();
        if (socket == CurlSocket.Bad)
        {
            await connection.DisposeAsync().ConfigureAwait(false);
            return Refused(target, started, CurlExitCode.CouldntConnect, CouldntConnectText);
        }

        CurlSocketCloser closer = new(socket, callbacks.CloseSocket);
        if (callbacks.SocketOption?.Invoke(socket, CurlSocketType.IpConnection) == SocketOptionResult.Error)
        {
            await connection.DisposeAsync().ConfigureAwait(false);
            closer.Close();
            return Refused(target, started, CurlExitCode.AbortedByCallback, AbortedByCallbackText);
        }

        return new SocketOpening(closer, CurlExitCode.Ok, string.Empty);
    }

    private SocketOpening Refused(ConnectTarget target, long started, CurlExitCode code, string text)
    {
        long elapsed = (long)timeProvider.GetElapsedTime(started).TotalMilliseconds;
        return new SocketOpening(null, code, string.Create(CultureInfo.InvariantCulture, $"Failed to connect to {target.Host}:{target.Port} after {elapsed} ms: {text}"));
    }

    private static ConnectResult Rewrapped(ConnectResult connect, IConnection connection) =>
        ConnectResult.Connected(
            connection,
            connect.Timings,
            connect.LocalEndPoint,
            connect.ProxyConnectResponseCode,
            connect.PeerCertificates,
            connect.IsReused,
            connect.ConnectionNumber,
            connect.ApplicationProtocol,
            connect.UnixSocketPath,
            connect.MappedHost,
            connect.MappedPort,
            connect.ConnectReplyHeadersStored,
            connect.ProxyConnectHeaderBytes);

    /// <summary>What the callbacks made of a new connection: its socket's closer, or the failure that refused it.</summary>
    /// <param name="Closer">Closes the socket the callbacks saw, or <see langword="null" /> when they refused it.</param>
    /// <param name="ExitCode">The refusal's exit code, <see cref="CurlExitCode.Ok" /> when accepted.</param>
    /// <param name="ErrorMessage">The refusal's message, empty when accepted.</param>
    private readonly record struct SocketOpening(CurlSocketCloser? Closer, CurlExitCode ExitCode, string ErrorMessage);
}
