using Curl.Protocol.Abstractions;

namespace Curl.Conformance;

/// <summary>
/// An <see cref="IConnector"/> on which <see cref="FtpPort"/>, upstream's <c>%FTPPORT</c>, reaches
/// the FTP control channel of upstream's <c>tests/ftpserver.pl</c> (at <c>curl-8_21_0</c>) for one
/// case, and which hands every other connection to the server it wraps. Each connection to
/// <see cref="FtpPort"/> gets its own <see cref="FtpControlChannelResponder"/> answering from the
/// case's <c>&lt;servercmd&gt;</c> <c>REPLY</c> lines. The passive-mode data connections of <c>PASV</c> and <c>EPSV</c> are reached on <see cref="PassivePort"/>; <c>PORT</c> and <c>EPRT</c> connect to
/// curl's port on <see cref="ActiveModeListener"/>, and the last <c>STOR</c> or <c>APPE</c> upload is
/// <see cref="UploadedBytes"/> (BL-1907). No socket is opened.
/// </summary>
public sealed class FtpServerConnector : IConnector
{
    /// <summary>The port of the FTP control channel, <c>%FTPPORT</c>.</summary>
    public const int FtpPort = 8993;

    /// <summary>The port each passive-mode data connection is offered on: ftpserver.pl picks a free port, and this fixed port, which no other stand-in uses (8995 is %SMTPPORT), stands for it.</summary>
    public const int PassivePort = 9005;

    private readonly LineProtocolServerConnector controlChannel;

    private readonly IConnector backend;

    private readonly FtpActiveModeListener activeModeListener = new();

    private FtpDataConnection? passiveConnection;

    private FtpDataConnection? upload;

    /// <summary>Creates the FTP server for one case.</summary>
    /// <param name="testCase">The expanded case whose <c>&lt;servercmd&gt;</c> and <c>&lt;reply&gt;</c> parts the server answers from.</param>
    /// <param name="backend">The server every connection not to <see cref="FtpPort"/> or an open passive data connection reaches.</param>
    public FtpServerConnector(UpstreamTestCase testCase, IConnector backend)
    {
        this.backend = backend;
        controlChannel = new(() => new FtpControlChannelResponder(
            LineProtocolServerCommands.Read((testCase.Find("reply", "servercmd")?.Content ?? ReadOnlyMemory<byte>.Empty).Span),
            new FtpTransferCommands(testCase, connection => passiveConnection = connection, activeModeListener.Connect, connection => upload = connection)));
    }

    /// <summary>
    /// Gets every command byte the control channel has read, across every connection, in the order
    /// the client wrote them: what ftpserver.pl logs for <c>&lt;verify&gt;&lt;protocol&gt;</c>.
    /// </summary>
    public ReadOnlyMemory<byte> ReceivedBytes => controlChannel.ReceivedBytes;

    /// <summary>Gets the listener curl's active mode (<c>-P</c>) listens on, which <c>PORT</c> and <c>EPRT</c> connect to: an in-memory port, no socket.</summary>
    public IConnectionListener ActiveModeListener => activeModeListener;

    /// <summary>Gets the bytes of the last <c>STOR</c> or <c>APPE</c> upload: what ftpserver.pl writes to <c>%LOGDIR/upload.%TESTNUMBER</c>, compared with <c>&lt;verify&gt;&lt;upload&gt;</c>.</summary>
    public ReadOnlyMemory<byte> UploadedBytes => upload?.ReceivedBytes ?? [];

    /// <summary>
    /// Opens a control-channel connection when <paramref name="target"/>'s port is <see cref="FtpPort"/>,
    /// the data connection the last <c>PASV</c> or <c>EPSV</c> opened (once) when it is
    /// <see cref="PassivePort"/>, otherwise a connection to the wrapped server.
    /// </summary>
    /// <param name="target">The host and port to connect to.</param>
    /// <param name="cancellationToken">Passed to the server the connection reaches.</param>
    /// <returns>The connected result.</returns>
    public ValueTask<ConnectResult> ConnectAsync(ConnectTarget target, CancellationToken cancellationToken)
    {
        if (target.Port == FtpPort)
        {
            return controlChannel.ConnectAsync(target, cancellationToken);
        }

        FtpDataConnection? passive = target.Port == PassivePort ? Interlocked.Exchange(ref passiveConnection, null) : null;
        return passive is null
            ? backend.ConnectAsync(target, cancellationToken)
            : ValueTask.FromResult(ConnectResult.Connected(passive));
    }
}
