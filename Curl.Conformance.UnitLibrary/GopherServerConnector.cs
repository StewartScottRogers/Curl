using System.Net;
using Curl.Protocol.Abstractions;

namespace Curl.Conformance;

/// <summary>
/// An <see cref="IConnector"/> that emulates upstream's Gopher test server, <c>sws --gopher</c>
/// (<c>tests/server/sws.c</c>), on <see cref="GopherPort"/>, and hands every other connection to the
/// server it wraps. No socket is opened.
/// </summary>
/// <remarks>
/// Each connection is a <see cref="GopherServerConnection"/>: it reads one selector line, answers it
/// with the <c>&lt;reply&gt;</c> part the selector's test number picks, and closes.
/// <see cref="ReceivedBytes"/> holds the selector lines, across connections, for comparison with
/// <c>&lt;verify&gt;&lt;protocol&gt;</c>.
/// </remarks>
/// <param name="testCase">The test case, parsed after <see cref="UpstreamTestFileExpander"/> has expanded it.</param>
/// <param name="backend">The server every connection not to <see cref="GopherPort"/> reaches.</param>
public sealed class GopherServerConnector(UpstreamTestCase testCase, IConnector backend) : IConnector
{
    /// <summary>The port of upstream's Gopher server, <c>%GOPHERPORT</c>.</summary>
    public const int GopherPort = 9008;

    private readonly SwsServerRecording recording = new();

    /// <summary>Every selector byte a client wrote, as sws's protocol dump holds them.</summary>
    public ReadOnlyMemory<byte> ReceivedBytes => recording.Bytes;

    /// <summary>The writes of <see cref="ReceivedBytes"/>, each numbered in the order of all servers' writes.</summary>
    internal IReadOnlyList<UpstreamProtocolDumpEntry> ReceivedDump => recording.Dump;

    /// <summary>Opens an in-memory connection to the Gopher emulation when <paramref name="target"/>'s port is <see cref="GopherPort"/>, otherwise a connection to the wrapped server.</summary>
    /// <param name="target">The host and port to connect to.</param>
    /// <param name="cancellationToken">Passed to the wrapped server.</param>
    /// <returns>The connection's result.</returns>
    public ValueTask<ConnectResult> ConnectAsync(ConnectTarget target, CancellationToken cancellationToken) =>
        target.Port == GopherPort
            ? ValueTask.FromResult(ConnectResult.Connected(new GopherServerConnection(testCase, recording)
            {
                RemoteEndPoint = new IPEndPoint(IPAddress.Loopback, GopherPort),
            }))
            : backend.ConnectAsync(target, cancellationToken);
}
