using System.Globalization;
using System.Net;
using System.Net.Sockets;
using Curl.Api.Callbacks;
using Curl.Protocol.Abstractions;

namespace Curl.Engine;

/// <summary>
/// Calls a handle's <c>CURLOPT_OPENSOCKETFUNCTION</c>, <c>CURLOPT_SOCKOPTFUNCTION</c> and
/// <c>CURLOPT_CLOSESOCKETFUNCTION</c> around each TCP connection <paramref name="inner" /> opens (ADR-0610). The
/// managed engine's sockets live inside <c>Curl.Networking</c> and are never handed out, so a socket the callbacks
/// see is an opaque <see cref="CurlSocket" /> over the <see cref="IConnection" />: the open-socket callback's, or
/// the engine's next <see cref="CurlSocketHandles" /> number without one. Once <paramref name="inner" /> has
/// connected, the open-socket callback is asked for the connection's peer address, and its
/// <see cref="CurlSocket.Bad" /> closes it and fails the connect with <see cref="CurlExitCode.CouldntConnect" />;
/// the socket-option callback then gets the socket, and its <see cref="SocketOptionResult.Error" /> closes it (the
/// close-socket callback called) and fails with <see cref="CurlExitCode.AbortedByCallback" />, each with curl
/// 8.22.0's <c>Failed to connect to &lt;host&gt;:&lt;port&gt; after &lt;n&gt; ms: &lt;text&gt;</c>;
/// <see cref="SocketOptionResult.AlreadyConnected" /> carries on as <see cref="SocketOptionResult.Ok" /> does, the
/// connection being made already. Closing the connection calls the close-socket callback once. QUIC connections pass
/// through untouched.
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

        CurlSocket socket = callbacks.OpenSocket is { } open
            ? open(CurlSocketType.IpConnection, AddressOf(connection.RemoteEndPoint))
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

        return Rewrapped(connect, new SocketCallbackConnection(connection, closer));
    }

    /// <summary>Opens a QUIC connection as the inner connector does; the socket callbacks are not called for it.</summary>
    /// <param name="target">Where to connect.</param>
    /// <param name="cancellationToken">Cancels the connect.</param>
    /// <returns>The inner connector's result.</returns>
    public ValueTask<MultiplexedConnectResult> ConnectMultiplexedAsync(ConnectTarget target, CancellationToken cancellationToken) =>
        inner.ConnectMultiplexedAsync(target, cancellationToken);

    /// <summary>Opens a QUIC session as the inner connector does; the socket callbacks are not called for it.</summary>
    /// <param name="target">Where to connect.</param>
    /// <param name="openSession">Builds the session over a new QUIC connection.</param>
    /// <param name="cancellationToken">Cancels the connect.</param>
    /// <returns>The inner connector's result.</returns>
    public ValueTask<ConnectResult> ConnectMultiplexedSessionAsync(ConnectTarget target, Func<IMultiplexedConnection, IConnection> openSession, CancellationToken cancellationToken) =>
        inner.ConnectMultiplexedSessionAsync(target, openSession, cancellationToken);

    /// <summary>
    /// The <c>struct curl_sockaddr</c> the open-socket callback is given for <paramref name="endPoint" />:
    /// <c>SOCK_STREAM</c> and <c>IPPROTO_TCP</c> (0 for a Unix socket), the family as the running platform numbers it
    /// (<see cref="FamilyNumberOf" />) and the end point's <see cref="SocketAddress" /> bytes; no end point is family 0
    /// and no bytes.
    /// </summary>
    /// <param name="endPoint">The connection's peer.</param>
    /// <returns>The address.</returns>
    internal static CurlSocketAddress AddressOf(EndPoint? endPoint)
    {
        if (endPoint is null)
        {
            return new CurlSocketAddress(0, StreamSocketType, TcpProtocol, ReadOnlyMemory<byte>.Empty);
        }

        SocketAddress address = endPoint.Serialize();
        int protocol = endPoint.AddressFamily == AddressFamily.Unix ? 0 : TcpProtocol;
        return new CurlSocketAddress(
            FamilyNumberOf(endPoint.AddressFamily, OperatingSystem.IsWindows(), OperatingSystem.IsMacOS()),
            StreamSocketType,
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

    private ConnectResult Refused(ConnectTarget target, long started, CurlExitCode code, string text)
    {
        long elapsed = (long)timeProvider.GetElapsedTime(started).TotalMilliseconds;
        return ConnectResult.Failed(code, string.Create(CultureInfo.InvariantCulture, $"Failed to connect to {target.Host}:{target.Port} after {elapsed} ms: {text}"));
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
}
