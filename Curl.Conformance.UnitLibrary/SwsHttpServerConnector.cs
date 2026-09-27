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
/// <c>no-expect</c> and <c>skip: N</c> change it, and answered with <c>&lt;data&gt;</c>, or with <c>&lt;dataN&gt;</c>
/// when the path's last segment is a number over 10000 whose last four digits are N (so
/// <c>/10002</c> gets <c>&lt;data2&gt;</c>). The connection stays open for the next request
/// until a reply containing <c>swsclose</c>, an empty reply, or <c>swsclose</c> in
/// <c>&lt;servercmd&gt;</c> closes it.
/// </para>
/// <para>
/// The other <c>&lt;servercmd&gt;</c> commands sws knows (<c>idle</c>, <c>stream</c>,
/// <c>connection-monitor</c>, <c>upgrade</c>, <c>delay</c> and <c>writedelay</c>) are not carried out; they are listed
/// in <see cref="UnsupportedServerCommands"/> so the caller can skip the case with a reason.
/// </para>
/// </remarks>
public sealed class SwsHttpServerConnector : IConnector
{
    private readonly List<byte> recording = [];

    private readonly SwsHttpReplySelector replySelector;

    private readonly SwsServerCommands serverCommands;

    /// <summary>Creates a server that answers from <paramref name="testCase"/>'s <c>&lt;reply&gt;</c> section.</summary>
    /// <param name="testCase">The test case, parsed after <see cref="UpstreamTestFileExpander"/> has expanded it.</param>
    public SwsHttpServerConnector(UpstreamTestCase testCase)
    {
        ArgumentNullException.ThrowIfNull(testCase);
        serverCommands = SwsServerCommands.Read((testCase.Find("reply", "servercmd")?.Content ?? ReadOnlyMemory<byte>.Empty).Span);
        UnsupportedServerCommands = serverCommands.UnsupportedCommands;
        replySelector = new SwsHttpReplySelector(testCase, serverCommands.ClosesAfterEveryReply);
    }

    /// <summary>
    /// The name of every <c>&lt;servercmd&gt;</c> command sws carries out that this emulation does
    /// not, which are <c>idle</c>, <c>stream</c>, <c>connection-monitor</c>, <c>upgrade</c>,
    /// <c>delay</c> and <c>writedelay</c>, in file
    /// order; empty when every command is carried out. Lines sws does not recognise are ignored,
    /// as sws ignores them.
    /// </summary>
    public IReadOnlyList<string> UnsupportedServerCommands { get; }

    /// <summary>
    /// Every byte the server has received, across every connection in the order the client
    /// wrote them, excluding bytes written after the server closed a connection.
    /// </summary>
    public ReadOnlyMemory<byte> ReceivedBytes => recording.ToArray();

    /// <summary>Opens a new in-memory connection to the server; it never fails.</summary>
    /// <param name="target">Ignored: every host and port reaches the same server.</param>
    /// <param name="cancellationToken">Not observed; the connection opens at once.</param>
    /// <returns>A connected result.</returns>
    public ValueTask<ConnectResult> ConnectAsync(ConnectTarget target, CancellationToken cancellationToken) =>
        ValueTask.FromResult(ConnectResult.Connected(new SwsHttpServerConnection(replySelector, serverCommands, recording)));
}
