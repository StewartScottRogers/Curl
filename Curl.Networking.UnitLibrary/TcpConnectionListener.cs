using System.Net;
using System.Net.Sockets;

using Curl.Protocol.Abstractions;

namespace Curl.Networking;

/// <summary>
/// The production <see cref="IConnectionListener" /> (ADR-0102): binds a TCP
/// <see cref="Socket" /> on the target's address, trying each port of its range in turn,
/// and returns it listening as a <see cref="TcpPendingConnection" />.
/// </summary>
/// <remarks>
/// Every failure is curl 8.21.0's exit 30 with the message its <c>lib/ftp.c</c> prints: a port
/// that is in use or not permitted moves on to the next one, and when the range runs out it is
/// <c>bind() failed, ran out of ports</c> (measured with a held port, BL-456); an address that
/// is not local is <c>bind(port=&lt;port&gt;) on non-local address failed: &lt;reason&gt;</c>,
/// which the FTP handler retries on the control connection's address (BL-464); any other bind
/// error is <c>bind(port=&lt;port&gt;) failed: &lt;reason&gt;</c>, and a socket that cannot be
/// opened or put to listening is <c>socket failure: &lt;reason&gt;</c>. The reason is worded by
/// <see cref="ConnectFailureReason" />, as curl's <c>curlx_strerror</c> words it.
/// </remarks>
public sealed class TcpConnectionListener : IConnectionListener
{
    /// <summary>
    /// Gets the step that opens an unbound TCP socket; a seam so the tests reach a socket that
    /// cannot be opened, which no loopback address provokes.
    /// </summary>
    internal Func<AddressFamily, Socket> OpenSocket { get; init; } =
        static family => new Socket(family, SocketType.Stream, ProtocolType.Tcp);

    /// <summary>
    /// Gets the step that puts a bound socket to listening, with curl's backlog of one; a seam
    /// so the tests reach a failed <c>listen</c>, which no bound socket provokes.
    /// </summary>
    internal Action<Socket> StartListening { get; init; } = static socket => socket.Listen(1);

    /// <inheritdoc />
    public ValueTask<ListenResult> ListenAsync(ListenTarget target, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(target);
        cancellationToken.ThrowIfCancellationRequested();

        return ValueTask.FromResult(ListenWithinRange(target));
    }

    /// <summary>
    /// Gives curl's message for a bind that failed with <paramref name="exception" />: none when
    /// the next port is tried; for an address that is not local (<c>EADDRNOTAVAIL</c>) the line
    /// curl 8.21.0 prints with <c>-v</c> before it binds on the control connection's address,
    /// <c>bind(port=N) on non-local address failed: reason</c>, which the FTP handler tells
    /// apart and retries on (ADR-0107); otherwise <c>bind(port=N) failed: reason</c>.
    /// </summary>
    /// <param name="exception">The failed bind.</param>
    /// <param name="port">The port it tried.</param>
    /// <returns>The message, or <see langword="null" /> to move on to the next port.</returns>
    internal static string? BindFailureMessage(SocketException exception, int port) =>
        exception.SocketErrorCode switch
        {
            SocketError.AddressAlreadyInUse or SocketError.AccessDenied => null,
            SocketError.AddressNotAvailable => $"bind(port={port}) on non-local address failed: {Reason(exception)}",
            _ => $"bind(port={port}) failed: {Reason(exception)}",
        };

    private static string Reason(SocketException exception) =>
        ConnectFailureReason.Describe(exception, OperatingSystem.IsWindows());

    private ListenResult ListenWithinRange(ListenTarget target)
    {
        for (var port = target.LowPort; ; port++)
        {
            var (socket, failureMessage) = TryListen(new IPEndPoint(target.Address, port));
            if (socket is not null)
            {
                return ListenResult.Listening(new TcpPendingConnection(socket));
            }

            if (failureMessage is not null)
            {
                return ListenResult.Failed(CurlExitCode.FtpPortFailed, failureMessage);
            }

            if (port == target.HighPort)
            {
                return ListenResult.Failed(CurlExitCode.FtpPortFailed, "bind() failed, ran out of ports");
            }
        }
    }

    /// <summary>
    /// Opens, binds and listens on <paramref name="endPoint" />. Returns the listening socket;
    /// or no socket and no message when the port is taken, so the next one is tried; or curl's
    /// message for any other failure.
    /// </summary>
    private (Socket? Socket, string? FailureMessage) TryListen(IPEndPoint endPoint)
    {
        Socket socket;
        try
        {
            socket = OpenSocket(endPoint.AddressFamily);
        }
        catch (SocketException exception)
        {
            return (null, $"socket failure: {Reason(exception)}");
        }

        try
        {
            socket.Bind(endPoint);
        }
        catch (SocketException exception)
        {
            socket.Dispose();
            return (null, BindFailureMessage(exception, endPoint.Port));
        }

        try
        {
            StartListening(socket);
            return (socket, null);
        }
        catch (SocketException exception)
        {
            socket.Dispose();
            return (null, $"socket failure: {Reason(exception)}");
        }
    }
}
