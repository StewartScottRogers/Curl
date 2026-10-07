using System.Text;

using Curl.Http2;
using Curl.Networking.Fakes;
using Curl.Protocol.Abstractions;

namespace Curl.Networking;

/// <summary>
/// Drives <see cref="TcpConnector" /> through an HTTPS proxy under <c>--proxy-http2</c>
/// (ADR-0408): the proxy is offered <c>h2,http/1.1</c>, an <c>h2</c> proxy is tunnelled through
/// an HTTP/2 <c>CONNECT</c> stream, and an <c>http/1.1</c> one through the HTTP/1.1 CONNECT.
/// The <c>-v</c> lines are curl 8.18.0's (OpenSSL, nghttp2), measured in BL-1413's Notes with
/// <c>curl -v --proxy-insecure --proxy-http2 -p -x https://172.26.96.1:18443 http://example.test/</c>.
/// </summary>
public sealed partial class TcpConnectorTests
{
    private static string[] Http2TunnelPrefaceLines =>
        [.. ProxyResolvedAndTriedLines, "CONNECT: 'h2' negotiated", "Establish HTTP/2 proxy tunnel to example.test:443"];

    [TestMethod]
    public async Task ConnectAsync_ThroughAnHttpsProxyWithoutProxyHttp2_OffersHttp11Alone()
    {
        var tlsProvider = new AlpnRecordingTlsProvider(new ScriptedConnection(Encoding.Latin1.GetBytes(EstablishedReply)), "http/1.1");
        var connector = CreateHttp2ProxyConnector(tlsProvider, proxyHttp2: false);

        var result = await ConnectLoggedAsync(connector, new ConnectTarget("example.test", 443, UseTls: false) { Proxy = HttpsProxy });

        Diagnostics.Assert("exit code", CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        CollectionAssert.AreEqual(new[] { "http/1.1" }, tlsProvider.ReceivedApplicationProtocols.ToArray());
    }

    [TestMethod]
    public async Task ConnectAsync_WhenAnH2ProxyAnswers200_SendsAConnectStreamAndCarriesTheTargetsBytesInItsDataFrames()
    {
        var proxyTls = new ScriptedConnection(ProxyFrames(StatusHeaders(1, "200"), Http2FrameFactory.CreateData(1, "hello"u8.ToArray(), isEndStream: false)));
        var tlsProvider = new AlpnRecordingTlsProvider(proxyTls, "h2");
        var events = new RecordingTransferEvents();
        var connector = CreateHttp2ProxyConnector(tlsProvider, proxyHttp2: true);

        var result = await ConnectLoggedAsync(connector, new ConnectTarget("example.test", 443, UseTls: false) { Events = events, Proxy = HttpsProxy });

        Diagnostics.Assert("exit code", CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        CollectionAssert.AreEqual(new[] { "h2", "http/1.1" }, tlsProvider.ReceivedApplicationProtocols.ToArray());
        CollectionAssert.AreEqual(
            Http2TunnelPrefaceLines.Concat(["CONNECT tunnel established, response 200", "CONNECT phase completed"]).ToArray(),
            events.Info);
        var sentFrames = await ReadClientFramesAsync(proxyTls.Written);
        var headers = sentFrames.Single(frame => frame.Type == Http2FrameType.Headers);
        Assert.AreEqual(1, headers.StreamId);
        Assert.IsFalse(headers.HasFlag(Http2FrameFlags.EndStream));
        CollectionAssert.AreEqual(
            new[] { ":method: CONNECT", ":authority: example.test:443", "user-agent: curl/8.21.0" },
            new HpackDecoder().Decode(Http2FramePayloadParser.ParseHeaders(headers).Fragment.Span).Select(field => $"{field.Name}: {field.Value}").ToArray());

        var connection = result.Connection!;
        var buffer = new byte[16];
        var read = await connection.ReadAsync(buffer, CancellationToken.None);
        Assert.AreEqual("hello", Encoding.Latin1.GetString(buffer, 0, read));
        var writtenBefore = proxyTls.Written.Count;
        await connection.WriteAsync("ping"u8.ToArray(), CancellationToken.None);
        var data = (await ReadFramesAsync(proxyTls.Written.Skip(writtenBefore).ToArray())).Single();
        Assert.AreEqual(Http2FrameType.Data, data.Type);
        Assert.AreEqual(1, data.StreamId);
        Assert.AreEqual("ping", Encoding.Latin1.GetString(data.Payload.Span));
    }

    [TestMethod]
    [DataRow("407", DisplayName = "407")]
    [DataRow("403", DisplayName = "403")]
    public async Task ConnectAsync_WhenAnH2ProxyRefusesTheConnect_FailsWithExit7AndNoReplyLine(string status)
    {
        // curl 8.18.0 against an h2 proxy answering :status 407 printed no "<" line for it,
        // then "curl: (7) Could not connect to server" (measured, BL-1413 Notes).
        var proxyTls = new ScriptedConnection(ProxyFrames(StatusHeaders(1, status, isEndStream: true)));
        var events = new RecordingTransferEvents();
        var connector = CreateHttp2ProxyConnector(new AlpnRecordingTlsProvider(proxyTls, "h2"), proxyHttp2: true);

        var result = await ConnectLoggedAsync(connector, new ConnectTarget("example.test", 443, UseTls: false) { Events = events, Proxy = HttpsProxy });

        Diagnostics.Assert("exit code", CurlExitCode.CouldntConnect, result.ExitCode);
        Assert.AreEqual(CurlExitCode.CouldntConnect, result.ExitCode);
        Assert.AreEqual("Could not connect to server", result.ErrorMessage);
        Assert.IsNull(result.Connection);
        Assert.IsTrue(proxyTls.IsDisposed);
        CollectionAssert.AreEqual(Http2TunnelPrefaceLines, events.Info);
    }

    [TestMethod]
    public async Task ConnectAsync_WhenAnH2ProxyBreaksOffMidFrame_DisposesItsConnectionAndRethrows()
    {
        var truncated = ProxyFrames(StatusHeaders(1, "200"))[..^1];
        var proxyTls = new ScriptedConnection(truncated);
        var connector = CreateHttp2ProxyConnector(new AlpnRecordingTlsProvider(proxyTls, "h2"), proxyHttp2: true);

        Diagnostics.Arrange("proxy", "h2 proxy whose reply breaks off mid frame");
        var thrown = await Assert.ThrowsExactlyAsync<EndOfStreamException>(async () =>
            await ConnectLoggedAsync(connector, new ConnectTarget("example.test", 443, UseTls: false) { Proxy = HttpsProxy }));

        Diagnostics.Act("exception", thrown.GetType().Name);
        Diagnostics.Assert("proxy connection disposed", true, proxyTls.IsDisposed);
        Assert.IsTrue(proxyTls.IsDisposed);
    }

    [TestMethod]
    public async Task ConnectAsync_WhenTheProxyPicksHttp11UnderProxyHttp2_FallsBackToTheHttp11Connect()
    {
        var proxyTls = new ScriptedConnection(Encoding.Latin1.GetBytes(EstablishedReply));
        var tlsProvider = new AlpnRecordingTlsProvider(proxyTls, "http/1.1");
        var events = new RecordingTransferEvents();
        var connector = CreateHttp2ProxyConnector(tlsProvider, proxyHttp2: true);

        var result = await ConnectLoggedAsync(connector, new ConnectTarget("example.test", 80, UseTls: false) { Events = events, Proxy = HttpsProxy });

        Diagnostics.Assert("exit code", CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        CollectionAssert.AreEqual(new[] { "h2", "http/1.1" }, tlsProvider.ReceivedApplicationProtocols.ToArray());
        CollectionAssert.AreEqual(
            ProxyResolvedAndTriedLines
                .Concat(["CONNECT: 'http/1.1' negotiated", "allocate connect buffer", "Establishing HTTP proxy tunnel to example.test:80"])
                .Concat(["CONNECT phase completed for HTTP proxy", "CONNECT tunnel established, response 200"])
                .ToArray(),
            events.Info);
        StringAssert.StartsWith(Encoding.Latin1.GetString(proxyTls.Written.ToArray()), "CONNECT example.test:80 HTTP/1.1\r\n");
    }

    /// <summary>Builds the bytes an h2 proxy sends: its empty SETTINGS, then <paramref name="frames" />.</summary>
    internal static byte[] ProxyFrames(params Http2Frame[] frames) =>
        [.. Http2FrameCodec.Serialize(Http2FrameFactory.CreateSettings([])), .. frames.SelectMany(Http2FrameCodec.Serialize)];

    /// <summary>Builds a HEADERS frame on <paramref name="streamId" /> holding <c>:status</c> <paramref name="status" />.</summary>
    internal static Http2Frame StatusHeaders(int streamId, string status, bool isEndStream = false) =>
        Http2FrameFactory.CreateHeaders(streamId, new HpackEncoder().Encode([new HeaderField(":status", status)]), isEndStream, isEndHeaders: true);

    /// <summary>Reads the frames a client wrote after its connection preface.</summary>
    internal static Task<List<Http2Frame>> ReadClientFramesAsync(List<byte> written) =>
        ReadFramesAsync(written.Skip(Http2Connection.ClientPreface.Length).ToArray());

    private static async Task<List<Http2Frame>> ReadFramesAsync(byte[] bytes)
    {
        using var stream = new MemoryStream(bytes);
        List<Http2Frame> frames = [];
        while (await Http2FrameCodec.ReadAsync(stream, 1 << 24, CancellationToken.None) is { } frame)
        {
            frames.Add(frame);
        }

        return frames;
    }

    private static TcpConnector CreateHttp2ProxyConnector(AlpnRecordingTlsProvider tlsProvider, bool proxyHttp2) =>
        new(
            new FakeDnsResolver(ProxyAddress),
            new FakeTcpDialer { DialOutcome = _ => new ScriptedConnection([]) },
            tlsProvider,
            new ManualTimeProvider(),
            HttpProxyTunnelOptions.Default with { MatchesSchannelBuild = false, ProxyHttp2 = proxyHttp2 });

    /// <summary>A TLS provider whose one handshake returns <paramref name="secured" /> agreeing on <paramref name="agreedProtocol" />, recording the offer.</summary>
    private sealed class AlpnRecordingTlsProvider(IConnection secured, string agreedProtocol) : IHandshakeReportingTlsProvider
    {
        public List<string> ReceivedApplicationProtocols { get; } = [];

        public TlsClientRoute Route => TlsClientRoute.SslStream;

        public string? RouteReason => null;

        public ValueTask<ConnectResult> AuthenticateAsClientAsync(
            IConnection plaintext,
            string targetHost,
            ITransferEvents events,
            bool isProxy,
            IReadOnlyList<string> applicationProtocols,
            CancellationToken cancellationToken)
        {
            ReceivedApplicationProtocols.AddRange(applicationProtocols);
            return AuthenticateAsClientAsync(plaintext, targetHost, cancellationToken);
        }

        public ValueTask<ConnectResult> AuthenticateAsClientAsync(IConnection plaintext, string targetHost, CancellationToken cancellationToken) =>
            ValueTask.FromResult(ConnectResult.Connected(secured, null, applicationProtocol: agreedProtocol));
    }
}
