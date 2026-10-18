using System.Net;
using Curl.Protocol.Abstractions;

namespace Curl.Conformance;

/// <summary>
/// An <see cref="IConnector"/> that emulates upstream's RTSP test server, <c>rtspd</c>
/// (<c>tests/server/rtspd.c</c> at <c>curl-8_22_0</c>), on <see cref="RtspPort"/>, and hands every other
/// connection to the server it wraps. No socket is opened.
/// </summary>
/// <remarks>
/// Each connection is an <see cref="RtspServerConnection"/>: it answers each request with the
/// <c>&lt;reply&gt;</c> part the request's path picks, then the interleaved RTP packets the case's
/// <c>&lt;servercmd&gt;</c> <c>rtp:</c> lines give that part. <see cref="ReceivedBytes"/> holds every request,
/// across connections, for comparison with <c>&lt;verify&gt;&lt;protocol&gt;</c>, as rtspd's
/// <c>server.input</c> does.
/// </remarks>
/// <param name="testCase">The test case, parsed after <see cref="UpstreamTestFileExpander"/> has expanded it.</param>
/// <param name="backend">The server every connection not to <see cref="RtspPort"/> reaches.</param>
public sealed class RtspServerConnector(UpstreamTestCase testCase, IConnector backend) : IConnector
{
    /// <summary>The port of upstream's RTSP server, <c>%RTSPPORT</c>.</summary>
    public const int RtspPort = 8996;

    /// <summary>The <c>&lt;server&gt;</c> name whose cases this stand-in lets run.</summary>
    public static IReadOnlyList<string> EmulatedServers { get; } = ["rtsp"];

    private readonly SwsServerRecording recording = new();

    /// <summary>Every request byte a client wrote, as rtspd's protocol dump holds them.</summary>
    public ReadOnlyMemory<byte> ReceivedBytes => recording.Bytes;

    /// <summary>The writes of <see cref="ReceivedBytes"/>, each numbered in the order of all servers' writes.</summary>
    internal IReadOnlyList<UpstreamProtocolDumpEntry> ReceivedDump => recording.Dump;

    /// <summary>
    /// Opens an in-memory connection to the RTSP emulation when <paramref name="target"/>'s port is
    /// <see cref="RtspPort"/>, its remote end point ::1 when the target is an IPv6 address and 127.0.0.1
    /// otherwise; otherwise a connection to the wrapped server.
    /// </summary>
    /// <param name="target">The host and port to connect to.</param>
    /// <param name="cancellationToken">Passed to the wrapped server.</param>
    /// <returns>The connection's result.</returns>
    public ValueTask<ConnectResult> ConnectAsync(ConnectTarget target, CancellationToken cancellationToken) =>
        target.Port == RtspPort
            ? ValueTask.FromResult(ConnectResult.Connected(new RtspServerConnection(testCase, recording)
            {
                RemoteEndPoint = new IPEndPoint(LoopbackAddress.For(target.Host), target.Port),
            }))
            : backend.ConnectAsync(target, cancellationToken);
}
