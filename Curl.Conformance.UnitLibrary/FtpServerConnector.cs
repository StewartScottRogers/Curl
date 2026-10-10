using Curl.Protocol.Abstractions;

namespace Curl.Conformance;

/// <summary>
/// An <see cref="IConnector"/> on which <see cref="FtpPort"/>, upstream's <c>%FTPPORT</c>, reaches
/// the FTP control channel of upstream's <c>tests/ftpserver.pl</c> (at <c>curl-8_21_0</c>) for one
/// case, and which hands every other connection to the server it wraps. Each connection to
/// <see cref="FtpPort"/> gets its own <see cref="FtpControlChannelResponder"/> answering from the
/// case's <c>&lt;servercmd&gt;</c> <c>REPLY</c> lines. No data connection is served yet (BL-1906
/// to BL-1908). No socket is opened.
/// </summary>
/// <param name="testCase">The expanded case whose <c>&lt;servercmd&gt;</c> the control channel answers from.</param>
/// <param name="backend">The server every connection not to <see cref="FtpPort"/> reaches.</param>
public sealed class FtpServerConnector(UpstreamTestCase testCase, IConnector backend) : IConnector
{
    /// <summary>The port of the FTP control channel, <c>%FTPPORT</c>.</summary>
    public const int FtpPort = 8993;

    private readonly LineProtocolServerConnector controlChannel = new(() =>
        new FtpControlChannelResponder(LineProtocolServerCommands.Read((testCase.Find("reply", "servercmd")?.Content ?? ReadOnlyMemory<byte>.Empty).Span)));

    /// <summary>
    /// Gets every command byte the control channel has read, across every connection, in the order
    /// the client wrote them: what ftpserver.pl logs for <c>&lt;verify&gt;&lt;protocol&gt;</c>.
    /// </summary>
    public ReadOnlyMemory<byte> ReceivedBytes => controlChannel.ReceivedBytes;

    /// <summary>Opens a control-channel connection when <paramref name="target"/>'s port is <see cref="FtpPort"/>, otherwise a connection to the wrapped server.</summary>
    /// <param name="target">The host and port to connect to.</param>
    /// <param name="cancellationToken">Passed to the server the connection reaches.</param>
    /// <returns>The connected result.</returns>
    public ValueTask<ConnectResult> ConnectAsync(ConnectTarget target, CancellationToken cancellationToken) =>
        target.Port == FtpPort
            ? controlChannel.ConnectAsync(target, cancellationToken)
            : backend.ConnectAsync(target, cancellationToken);
}
