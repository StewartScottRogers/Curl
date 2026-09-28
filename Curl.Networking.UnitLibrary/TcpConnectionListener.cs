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
/// <c>bind() failed, ran out of ports</c> (measured with a held port, BL-456); any other bind
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
    /// Gets a value indicating whether curl moves on to the next port after a bind fails with
    /// <paramref name="error" />: only when the port is in use or not permitted.
    /// </summary>
    /// <param name="error">The bind's error.</param>
    /// <returns><see langword="true" /> for <c>EADDRINUSE</c> and <c>EACCES</c>.</returns>
    internal static bool MovesOnToTheNextPort(SocketError error) =>
        error == SocketError.AddressAlreadyInUse || error == SocketError.AccessDenied;

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
            return (null, MovesOnToTheNextPort(exception.SocketErrorCode)
                ? null
                : $"bind(port={endPoint.Port}) failed: {Reason(exception)}");
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
