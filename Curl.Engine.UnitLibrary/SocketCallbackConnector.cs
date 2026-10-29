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
/// the engine's next <see cref="CurlSocketHandles" /> number without one. A TCP connect asks the callbacks before each
/// attempt dials, through the <see cref="AttemptSocketOpener" /> it puts on the target (CGR-036, ADR-0903): the
/// open-socket callback's <see cref="CurlSocket.Bad" /> opens no connection and fails the connect with
/// <see cref="CurlExitCode.CouldntConnect" />, the socket it returns is the one connected (as it is, under the
/// socket-option callback's <see cref="SocketOptionResult.AlreadyConnected" />), and the socket-option callback's
/// <see cref="SocketOptionResult.Error" /> calls the close-socket callback and fails with
/// <see cref="CurlExitCode.AbortedByCallback" />, each with curl 8.22.0's
/// <c>Failed to connect to &lt;host&gt;:&lt;port&gt; after &lt;n&gt; ms: &lt;text&gt;</c>; a failed attempt's
/// socket goes to the close-socket callback. A connector that never asks the opener gets the same steps once it has
/// connected, for the connection's peer, a refusal closing the connection. Closing the connection calls the
/// close-socket callback once. A QUIC connection's UDP socket goes through the after-connect steps, the open-socket
/// callback seeing <c>SOCK_DGRAM</c> and <c>IPPROTO_UDP</c> (BL-2474).
/// </summary>
/// <param name="inner">Opens the connections.</param>
/// <param name="callbacks">The handle's socket callbacks.</param>
/// <param name="handles">Numbers the sockets the open-socket callback did not make.</param>
/// <param name="timeProvider">Measures the milliseconds a refused connect reports.</param>
/// <param name="suppliesSockets">
/// Whether the open-socket callback's socket is an operating system socket handed to the dialer; <see langword="false" />
/// when the engine dials through an <see cref="EngineDialing" /> (the conformance harness's in-memory servers), whose
/// libtests return socket numbers no operating system made, so the dialer opens its own.
/// </param>
internal sealed class SocketCallbackConnector(IConnector inner, EasyCallbackOptions callbacks, CurlSocketHandles handles, TimeProvider timeProvider, bool suppliesSockets = true) : IConnector
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
            : new SocketCallbackConnector(connector, callbacks, shared.SocketHandles, shared.TimeProvider, suppliesSockets: shared.Dialing is null);

    /// <summary>
    /// Connects through the inner connector with an <see cref="AttemptSocketOpener" /> on the target, so the
    /// open-socket and socket-option callbacks run before each TCP attempt dials, as curl 8.22.0's
    /// <c>cf_socket_open</c> calls them (CGR-036, ADR-0903): a refusal opens no connection, a socket the open-socket
    /// callback returns is the one connected (used as it is under <see cref="SocketOptionResult.AlreadyConnected" />),
    /// and a failed attempt's socket goes to the close-socket callback. A connector that never asks the opener (a Unix
    /// socket, a test fake) gets the callbacks once it has connected, for the connection's peer.
    /// </summary>
    /// <param name="target">Where to connect.</param>
    /// <param name="cancellationToken">Cancels the connect.</param>
    /// <returns>The connection wrapped as a <see cref="SocketCallbackConnection" />, or the failure.</returns>
    public async ValueTask<ConnectResult> ConnectAsync(ConnectTarget target, CancellationToken cancellationToken)
    {
        long started = timeProvider.GetTimestamp();
        AttemptSocketOpener opener = new(callbacks, handles, suppliesSockets);
        ConnectResult connect = await inner.ConnectAsync(target with { SocketOpener = opener }, cancellationToken).ConfigureAwait(false);
        return opener.WasAsked
            ? Opened(target, started, connect, opener)
            : await OpenedAfterConnectAsync(target, started, connect).ConfigureAwait(false);
    }

    // The result of a connect whose attempts the opener saw: the connection over the winning attempt's socket, or the
    // failure, curl's open-socket or socket-option refusal when one refused an attempt. A connection whose attempt the
    // opener no longer holds gets a socket number no callback saw, so closing it calls none.
    private ConnectResult Opened(ConnectTarget target, long started, ConnectResult connect, AttemptSocketOpener opener)
    {
        CurlSocketCloser? closer = opener.TakeOpened();
        if (connect.Connection is { } connection)
        {
            return Rewrapped(connect, new SocketCallbackConnection(connection, CloserOf(connection, closer)));
        }

        closer?.Close();
        if (opener.Refusal == CurlExitCode.Ok)
        {
            return connect;
        }

        SocketOpening refused = Refused(target, started, opener.Refusal, opener.Refusal == CurlExitCode.AbortedByCallback ? AbortedByCallbackText : CouldntConnectText);
        return ConnectResult.Failed(refused.ExitCode, refused.ErrorMessage);
    }

    // The winning attempt's closer, now under the connection's operating system socket when the dialer opened its
    // own (CGR-129), or a closer calling nothing when the opener no longer holds the attempt.
    private CurlSocketCloser CloserOf(IConnection connection, CurlSocketCloser? attempt) =>
        (attempt, callbacks.OpenSocket, connection.SocketHandle) switch
        {
            (null, _, _) => new CurlSocketCloser(handles.Next(), close: null),
            ({ IsTheConnectionsOwnSocket: true }, _, _) => attempt,
            (_, null, { } handle) => new CurlSocketCloser(new CurlSocket(handle), callbacks.CloseSocket, isTheConnectionsOwnSocket: true),
            _ => attempt,
        };

    // Runs the callbacks for a connection the inner connector made without asking the opener.
    private async ValueTask<ConnectResult> OpenedAfterConnectAsync(ConnectTarget target, long started, ConnectResult connect)
    {
        if (connect.Connection is not { } connection)
        {
            return connect;
        }

        SocketOpening opening = await OpenSocketAsync(target, started, connection, AddressOf(connection.RemoteEndPoint), connection.SocketHandle).ConfigureAwait(false);
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

        SocketOpening opening = await OpenSocketAsync(target, started, connection, DatagramAddressOf(connection.RemoteEndPoint), socketHandle: null).ConfigureAwait(false);
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

    // Runs the open-socket and socket-option callbacks for a connection inner has just made, closing it on a refusal;
    // without an open-socket callback the socket is the connection's own operating system socket when it has one.
    private async ValueTask<SocketOpening> OpenSocketAsync(ConnectTarget target, long started, IAsyncDisposable connection, CurlSocketAddress address, nint? socketHandle)
    {
        (CurlSocket socket, bool ownSocket) = SocketFor(address, socketHandle);
        if (socket == CurlSocket.Bad)
        {
            await connection.DisposeAsync().ConfigureAwait(false);
            return Refused(target, started, CurlExitCode.CouldntConnect, CouldntConnectText);
        }

        CurlSocketCloser closer = new(socket, callbacks.CloseSocket, ownSocket);
        if (callbacks.SocketOption?.Invoke(socket, CurlSocketType.IpConnection) == SocketOptionResult.Error)
        {
            await connection.DisposeAsync().ConfigureAwait(false);
            closer.Close();
            return Refused(target, started, CurlExitCode.AbortedByCallback, AbortedByCallbackText);
        }

        return new SocketOpening(closer, CurlExitCode.Ok, string.Empty);
    }

    // The open-socket callback's socket, else the connection's own operating system socket (marked as the engine's),
    // else the engine's next number.
    private (CurlSocket Socket, bool IsTheConnectionsOwnSocket) SocketFor(CurlSocketAddress address, nint? socketHandle) =>
        (callbacks.OpenSocket, socketHandle) switch
        {
            ({ } open, _) => (open(CurlSocketType.IpConnection, address), false),
            (null, { } handle) => (new CurlSocket(handle), true),
            _ => (handles.Next(), false),
        };

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

    /// <summary>
    /// The <see cref="ITcpSocketOpener" /> one <see cref="ConnectAsync" /> puts on its target: for each TCP attempt it
    /// calls the open-socket callback (or numbers the socket without one) and the socket-option callback, and keeps
    /// the attempt's <see cref="CurlSocketCloser" /> until the attempt fails or the connect takes it.
    /// </summary>
    /// <param name="callbacks">The handle's socket callbacks.</param>
    /// <param name="handles">Numbers the sockets the open-socket callback did not make.</param>
    /// <param name="suppliesSockets">Whether the open-socket callback's socket is handed to the dialer.</param>
    internal sealed class AttemptSocketOpener(EasyCallbackOptions callbacks, CurlSocketHandles handles, bool suppliesSockets) : ITcpSocketOpener
    {
        private readonly Lock gate = new();
        private readonly List<KeyValuePair<IPEndPoint, CurlSocketCloser>> opened = [];
        private volatile bool asked;
        private volatile int refusal;

        /// <summary>Gets a value indicating whether the connector asked about any attempt.</summary>
        internal bool WasAsked => asked;

        /// <summary>Gets the exit code of the last attempt a callback refused, <see cref="CurlExitCode.Ok" /> for none.</summary>
        internal CurlExitCode Refusal => (CurlExitCode)refusal;

        public TcpSocketOpenDecision Open(IPEndPoint remoteEndPoint)
        {
            asked = true;
            (CurlSocket socket, Socket? engineSocket) = SocketFor(remoteEndPoint);
            if (socket == CurlSocket.Bad)
            {
                refusal = (int)CurlExitCode.CouldntConnect;
                return TcpSocketOpenDecision.Refuse;
            }

            CurlSocketCloser closer = new(socket, callbacks.CloseSocket, isTheConnectionsOwnSocket: engineSocket is not null, engineSocket);
            SocketOptionResult? option = callbacks.SocketOption?.Invoke(socket, CurlSocketType.IpConnection);
            if (option == SocketOptionResult.Error)
            {
                closer.Close();
                refusal = (int)CurlExitCode.AbortedByCallback;
                return TcpSocketOpenDecision.Refuse;
            }

            lock (gate)
            {
                opened.Add(new(remoteEndPoint, closer));
            }

            return DecisionFor(socket, option, engineSocket);
        }

        // The open-socket callback's socket for the attempt; else, for a socket-option callback over a dialer that
        // adopts sockets, an operating system socket the engine opens now, before the connect, as curl 8.22.0's
        // cf_socket_open does (CGR-145); else the engine's next number.
        private (CurlSocket Socket, Socket? EngineSocket) SocketFor(IPEndPoint remoteEndPoint)
        {
            if (callbacks.OpenSocket is { } open)
            {
                return (open(CurlSocketType.IpConnection, AddressOf(remoteEndPoint)), null);
            }

            if (callbacks.SocketOption is null || !suppliesSockets)
            {
                return (handles.Next(), null);
            }

            Socket engineSocket = new(remoteEndPoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp) { NoDelay = true };
            return (new CurlSocket(engineSocket.Handle), engineSocket);
        }

        // The dialer's own socket without a socket the callbacks saw (or one it cannot adopt), else that socket,
        // used as it is under CURL_SOCKOPT_ALREADY_CONNECTED, connected or not.
        private TcpSocketOpenDecision DecisionFor(CurlSocket socket, SocketOptionResult? option, Socket? engineSocket) =>
            (callbacks.OpenSocket is null && engineSocket is null) || !suppliesSockets
                ? TcpSocketOpenDecision.Proceed
                : TcpSocketOpenDecision.Supplied(socket.Handle, option == SocketOptionResult.AlreadyConnected);

        public void SocketClosed(IPEndPoint remoteEndPoint)
        {
            CurlSocketCloser? closer = null;
            lock (gate)
            {
                int index = opened.FindIndex(attempt => attempt.Key.Equals(remoteEndPoint));
                if (index >= 0)
                {
                    closer = opened[index].Value;
                    opened.RemoveAt(index);
                }
            }

            closer?.Close();
        }

        /// <summary>
        /// Takes the socket of the attempt still open, the connection's, closing any other one left; <see langword="null" />
        /// when none is.
        /// </summary>
        /// <returns>The open attempt's closer, or <see langword="null" />.</returns>
        internal CurlSocketCloser? TakeOpened()
        {
            CurlSocketCloser[] left;
            lock (gate)
            {
                left = [.. opened.Select(attempt => attempt.Value)];
                opened.Clear();
            }

            foreach (CurlSocketCloser abandoned in left.SkipLast(1))
            {
                abandoned.Close();
            }

            return left.LastOrDefault();
        }
    }

    /// <summary>What the callbacks made of a new connection: its socket's closer, or the failure that refused it.</summary>
    /// <param name="Closer">Closes the socket the callbacks saw, or <see langword="null" /> when they refused it.</param>
    /// <param name="ExitCode">The refusal's exit code, <see cref="CurlExitCode.Ok" /> when accepted.</param>
    /// <param name="ErrorMessage">The refusal's message, empty when accepted.</param>
    private readonly record struct SocketOpening(CurlSocketCloser? Closer, CurlExitCode ExitCode, string ErrorMessage);
}
