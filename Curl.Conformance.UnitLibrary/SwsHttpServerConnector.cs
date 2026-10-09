using System.Net;
using Curl.Protocol.Abstractions;

namespace Curl.Conformance;

/// <summary>
/// An <see cref="IConnector"/> whose every connection is served by an in-memory emulation of
/// upstream's <c>sws</c> HTTP test server (<c>tests/server/sws.c</c> at <c>curl-8_21_0</c>),
/// answering from one test case's <c>&lt;reply&gt;</c> and recording every byte it receives.
/// No socket is opened.
/// </summary>
/// <remarks>
/// <para>
/// Each request is read by its headers, then a body by <c>Transfer-Encoding: chunked</c> or
/// <c>Content-Length</c>, as the <c>&lt;servercmd&gt;</c> commands <c>auth_required</c>,
/// <c>no-expect</c>, <c>skip: N</c> and <c>upgrade</c> change it, and answered with <c>&lt;data&gt;</c>, or with <c>&lt;dataN&gt;</c>
/// when the path's last segment is a number over 10000 whose last four digits are N (so
/// <c>/10002</c> gets <c>&lt;data2&gt;</c>). An <c>Authorization:</c> header moves the part
/// number as sws's does (Digest to <c>&lt;data1000&gt;</c>, NTLM type 1 and 3 to
/// <c>&lt;data1001&gt;</c> and <c>&lt;data1002&gt;</c>, Basic from 1000 up by one, Negotiate
/// counting up), a reply containing <c>swsbounce</c> gives the next request the part after it,
/// and a <c>CONNECT host:port</c> request is answered from <c>&lt;connect&gt;</c> or
/// <c>&lt;connectN&gt;</c>. The connection stays open for the next request
/// until a reply containing <c>swsclose</c>, an empty reply, or <c>swsclose</c> in
/// <c>&lt;servercmd&gt;</c> closes it.
/// </para>
/// <para>
/// The timing and stream commands are carried out too: <c>idle</c> answers nothing and keeps the
/// connection open, <c>stream</c> answers with an endless stream of text, <c>writedelay: N</c>
/// and <c>&lt;postcmd&gt;</c> <c>wait N</c> hold replies and closes back on the injected
/// <see cref="TimeProvider"/>, <c>connection-monitor</c> records <c>[DISCONNECT]</c> when a
/// connection closes, and <c>upgrade</c> answers a request with <c>Upgrade:</c> in it and then
/// records the connection's traffic until the client has been quiet for a second.
/// <c>delay: N</c> is not carried out; it is listed in <see cref="UnsupportedServerCommands"/> so
/// the caller can skip the case with a reason.
/// </para>
/// </remarks>
public sealed class SwsHttpServerConnector : IConnector
{
    // Each connection gets the next port above this one as its local end point, so %{local_port} is a number.
    private const int FirstLocalPort = 49151;

    private readonly SwsServerRecording recording = new();

    private readonly SwsServerAbandonment abandonment = new();

    private readonly SwsHttpReplySelector replySelector;

    private readonly SwsServerCommands serverCommands;

    private readonly TimeSpan waitAfterReply;

    private readonly TimeProvider timeProvider;

    private int connectionsOpened;

    /// <summary>Creates a server that answers from <paramref name="testCase"/>'s <c>&lt;reply&gt;</c> section on the system clock.</summary>
    /// <param name="testCase">The test case, parsed after <see cref="UpstreamTestFileExpander"/> has expanded it.</param>
    public SwsHttpServerConnector(UpstreamTestCase testCase)
        : this(testCase, TimeProvider.System)
    {
    }

    /// <summary>Creates a server that answers from <paramref name="testCase"/>'s <c>&lt;reply&gt;</c> section.</summary>
    /// <param name="testCase">The test case, parsed after <see cref="UpstreamTestFileExpander"/> has expanded it.</param>
    /// <param name="timeProvider">The clock that write delays, post-reply waits and upgraded-connection closes are timed on.</param>
    public SwsHttpServerConnector(UpstreamTestCase testCase, TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(testCase);
        ArgumentNullException.ThrowIfNull(timeProvider);
        serverCommands = SwsServerCommands.Read(ReplyPart(testCase, "servercmd"));
        waitAfterReply = SwsPostReplyCommands.ReadWaitAfterReply(ReplyPart(testCase, "postcmd"));
        UnsupportedServerCommands = serverCommands.UnsupportedCommands;
        replySelector = new SwsHttpReplySelector(testCase, serverCommands);
        this.timeProvider = timeProvider;
    }

    /// <summary>
    /// The name of every <c>&lt;servercmd&gt;</c> command sws carries out that this emulation does
    /// not, which is <c>delay</c>, once per line that gives it, in file order; empty when every
    /// command is carried out. Lines sws does not recognise are ignored, as sws ignores them.
    /// </summary>
    public IReadOnlyList<string> UnsupportedServerCommands { get; }

    /// <summary>
    /// Every byte the server has received, across every connection in the order the client
    /// wrote them, excluding bytes written after the server closed a connection or stopped
    /// reading it, with <c>[DISCONNECT]</c> and a line feed where <c>connection-monitor</c> saw a
    /// connection close.
    /// </summary>
    public ReadOnlyMemory<byte> ReceivedBytes => recording.Bytes;

    /// <summary>
    /// Gives up on the server once the harness no longer waits for the run using it: from then
    /// on connecting, and reading or writing on any connection it opened, throws
    /// <see cref="IOException"/>, so a run that outlived its time limit stops at its next exchange.
    /// </summary>
    public void Abandon() => abandonment.Abandon();

    /// <summary>Opens a new in-memory connection to the server, on the next local port from 49152 on; it fails only after <see cref="Abandon"/>.</summary>
    /// <param name="target">Ignored: every host and port reaches the same server.</param>
    /// <param name="cancellationToken">Not observed; the connection opens at once.</param>
    /// <returns>A connected result.</returns>
    /// <exception cref="IOException">The server has been abandoned.</exception>
    public ValueTask<ConnectResult> ConnectAsync(ConnectTarget target, CancellationToken cancellationToken)
    {
        abandonment.ThrowIfAbandoned();
        return ValueTask.FromResult(ConnectResult.Connected(new SwsHttpServerConnection(replySelector, serverCommands, waitAfterReply, recording, timeProvider, abandonment)
        {
            LocalEndPoint = new IPEndPoint(IPAddress.Loopback, FirstLocalPort + Interlocked.Increment(ref connectionsOpened)),
        }));
    }

    private static ReadOnlySpan<byte> ReplyPart(UpstreamTestCase testCase, string name) =>
        (testCase.Find("reply", name)?.Content ?? ReadOnlyMemory<byte>.Empty).Span;
}
