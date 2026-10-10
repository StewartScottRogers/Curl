using System.Net;
using Curl.Protocol.Abstractions;

namespace Curl.Conformance;

/// <summary>
/// An <see cref="IConnector"/> whose every connection is served in memory by a line protocol,
/// the shared core of the FTP, SMTP, IMAP and POP3 stand-ins for upstream's
/// <c>tests/ftpserver.pl</c> (at <c>curl-8_21_0</c>): the greeting is sent on connect, the client's
/// bytes are read as CRLF-terminated command lines however its writes split them, each line is
/// answered by the connection's <see cref="ILineProtocolResponder"/>, and every byte the server
/// reads is recorded for comparison with <c>&lt;verify&gt;&lt;protocol&gt;</c>. No socket is opened.
/// </summary>
internal sealed class LineProtocolServerConnector : IConnector
{
    private const int FirstLocalPort = 49152;

    private readonly SwsServerRecording recording = new();

    private readonly Func<ILineProtocolResponder> createResponder;

    private int connectionsOpened;

    /// <summary>Creates a server that gives each connection a responder of its own.</summary>
    /// <param name="createResponder">Creates the responder for one new connection.</param>
    public LineProtocolServerConnector(Func<ILineProtocolResponder> createResponder)
    {
        ArgumentNullException.ThrowIfNull(createResponder);
        this.createResponder = createResponder;
    }

    /// <summary>
    /// Gets the <c>&lt;server&gt;</c> names whose cases <see cref="UpstreamCaseScreening"/> lets run
    /// on a line-protocol stand-in; each protocol task adds its name here once its stand-in answers.
    /// </summary>
    public static IReadOnlyList<string> EmulatedServers { get; } = ["ftp", "smtp", "imap", "pop3"];

    /// <summary>
    /// Gets every byte the server has read, across every connection in the order the client wrote
    /// them, excluding bytes written after the server closed a connection.
    /// </summary>
    public ReadOnlyMemory<byte> ReceivedBytes => recording.Bytes;

    /// <summary>Opens a new in-memory connection, its remote end point 127.0.0.1 at the target's port.</summary>
    /// <param name="target">Gives only the remote end point's port: every host and port reaches the same server.</param>
    /// <param name="cancellationToken">Not observed; the connection opens at once.</param>
    /// <returns>A connected result.</returns>
    public ValueTask<ConnectResult> ConnectAsync(ConnectTarget target, CancellationToken cancellationToken) =>
        ValueTask.FromResult(ConnectResult.Connected(new LineProtocolServerConnection(createResponder(), recording)
        {
            LocalEndPoint = new IPEndPoint(IPAddress.Loopback, FirstLocalPort + Interlocked.Increment(ref connectionsOpened) - 1),
            RemoteEndPoint = new IPEndPoint(IPAddress.Loopback, target.Port),
        }));
}
