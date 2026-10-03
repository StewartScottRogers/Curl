using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Net.Sockets;

using Curl.Protocol.Abstractions;

namespace Curl.Networking;

/// <summary>
/// The <see cref="IPendingConnection" /> <see cref="TcpConnectionListener" /> returns: a
/// listening TCP socket, closed on dispose, whose accepted connection is a
/// <see cref="StreamConnection" /> that owns its own socket (ADR-0102).
/// </summary>
/// <remarks>
/// A failed accept is curl 8.21.0's exit 10, <c>Error accept()ing server connect: &lt;reason&gt;</c>,
/// as <c>cf_tcp_accept_connect</c> in its <c>lib/cf-socket.c</c> reports it, the reason worded
/// by <see cref="ConnectFailureReason" /> as curl's <c>curlx_strerror</c> words it; the case
/// cannot be provoked from the command line.
/// </remarks>
internal sealed class TcpPendingConnection : IPendingConnection
{
    private readonly Socket _listeningSocket;

    /// <summary>
    /// Initializes a new instance of the <see cref="TcpPendingConnection" /> class.
    /// </summary>
    /// <param name="listeningSocket">The bound, listening socket, owned from here on.</param>
    internal TcpPendingConnection(Socket listeningSocket)
    {
        _listeningSocket = listeningSocket;
        LocalEndPoint = listeningSocket.LocalEndPoint!;
    }

    /// <inheritdoc />
    public EndPoint LocalEndPoint { get; }

    /// <summary>
    /// Gets the step that accepts one connection on the listening socket; a seam so the fast
    /// tests reach both outcomes without a peer, as ADR-0083 keeps the socket work itself in
    /// <see cref="AcceptStreamConnectionAsync(Socket, CancellationToken)" />.
    /// </summary>
    internal Func<Socket, CancellationToken, ValueTask<StreamConnection>> AcceptConnectionAsync { get; init; } =
        AcceptStreamConnectionAsync;

    /// <inheritdoc />
    public async ValueTask<ConnectResult> AcceptAsync(CancellationToken cancellationToken)
    {
        StreamConnection connection;
        try
        {
            connection = await AcceptConnectionAsync(_listeningSocket, cancellationToken).ConfigureAwait(false);
        }
        catch (SocketException exception)
        {
            var reason = ConnectFailureReason.Describe(exception, OperatingSystem.IsWindows());
            return ConnectResult.Failed(CurlExitCode.FtpAcceptFailed, $"Error accept()ing server connect: {reason}");
        }

        return ConnectResult.Connected(connection, null, connection.LocalEndPoint as IPEndPoint);
    }

    /// <summary>
    /// Stops listening. A connection already accepted stays open.
    /// </summary>
    /// <returns>A completed task.</returns>
    public ValueTask DisposeAsync()
    {
        _listeningSocket.Dispose();
        return ValueTask.CompletedTask;
    }

    /// <summary>
    /// Sets TCP_NODELAY on an accepted socket, as curl sets it on every connection it makes
    /// (<c>tcp_nodelay</c> defaults on), so small writes leave without Nagle's delay.
    /// </summary>
    /// <param name="accepted">The socket the listening socket accepted.</param>
    internal static void TurnOffNagle(Socket accepted) => accepted.NoDelay = true;

    /// <summary>
    /// Accepts one socket and returns it as a <see cref="StreamConnection" /> that owns it, with
    /// its local and remote end points; a socket that fails before it is handed over is closed.
    /// </summary>
    /// <remarks>
    /// Excluded from coverage per ADR-0083: every line needs a peer connecting over TCP, so the
    /// loopback test in the Integration run measures it.
    /// </remarks>
    [ExcludeFromCodeCoverage(Justification = "ADR-0083: a thin socket adapter, measured by the Integration run.")]
    private static async ValueTask<StreamConnection> AcceptStreamConnectionAsync(Socket listeningSocket, CancellationToken cancellationToken)
    {
        var accepted = await listeningSocket.AcceptAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            TurnOffNagle(accepted);
            return new StreamConnection(
                new NetworkStream(accepted, ownsSocket: true),
                accepted.RemoteEndPoint,
                accepted.LocalEndPoint);
        }
        catch
        {
            accepted.Dispose();
            throw;
        }
    }
}
