using System.Globalization;
using System.Net;
using System.Text;
using Curl.Core;
using Curl.Http2;
using Curl.Http3;
using Curl.Protocol.Abstractions;

namespace Curl.Console;

/// <summary>
/// Pins the <c>h2</c> and <c>h3</c> alternatives of <c>--alt-svc</c> through the runner and the production
/// handler set over scripted connectors, an <see cref="InMemoryFileSystem" /> and a stopped clock, on every
/// platform (BL-733, ADR-0226), against curl.se's 8.18.0 build with HTTP/2 and HTTP/3 measured on 2026-09-29
/// (BL-733 Notes): an <c>h3</c> entry naming the origin upgrades the next run to HTTP/3 and falls back to TCP
/// when QUIC fails; one naming another port connects there over HTTP/3 alone; an <c>h2</c> one connects over
/// TCP, where ALPN chooses HTTP/2; and the version option decides which entries are used.
/// </summary>
[TestClass]
public sealed class CurlCommandRunnerAltSvcVersionTests
{
    private const string CacheFile = "cache.txt";

    private const string Origin = "https://localhost:18443/";

    private const string Future = "\"20260930 11:05:35\"";

    private const string SameOriginH3Entry = $"h1 localhost 18443 h3 localhost 18443 {Future} 0 0";

    private const string OtherPortH3Entry = $"h1 localhost 18443 h3 localhost 18444 {Future} 0 0";

    private const string OtherPortH2Entry = $"h1 localhost 18443 h2 localhost 18444 {Future} 0 0";

    private const string QuicRecvError = "QUIC: recvfrom() unexpectedly returned -1 (errno=10054; Connection was reset)";

    private static readonly DateTimeOffset Now = new(2026, 9, 29, 11, 5, 35, TimeSpan.Zero);

    private static readonly MultiplexedConnectResult QuicRefused = MultiplexedConnectResult.Failed(CurlExitCode.RecvError, QuicRecvError);

    private readonly MemoryStream standardOutput = new();
    private readonly MemoryStream standardError = new();
    private readonly InMemoryFileSystem files = new();

    private string StandardOutputText => Encoding.Latin1.GetString(standardOutput.ToArray());

    private string StandardErrorText => Encoding.Latin1.GetString(standardError.ToArray());

    /// <summary>Gets standard error with every CR LF made LF, so a line reads the same on every platform.</summary>
    private string StandardErrorLines => StandardErrorText.Replace("\r\n", "\n", StringComparison.Ordinal);

    [TestMethod]
    public async Task RunAsync_SecondRunAfterAnH3AltSvcHeader_ConnectsOverHttp3()
    {
        ScriptedConnector firstServer = new([Encoding.Latin1.GetBytes("HTTP/1.1 200 OK\r\nContent-Length: 2\r\nAlt-Svc: h3=\":18443\"\r\n\r\nhi")]);
        await RunAsync(firstServer, ["-s", "--alt-svc", CacheFile, Origin]);
        Assert.AreEqual(CacheFileText("h1 localhost 18443 h3 localhost 18443 \"20260930 11:05:35\" 0 0"), SavedCacheFile());
        files.ExistingContent[CacheFile] = files.Written[CacheFile].ToArray();
        standardOutput.SetLength(0);
        ScriptedQuicConnector secondServer = Http3Server("hello");

        int exitCode = await RunAsync(secondServer, ["-sS", "-w", "|%{http_version}", "--alt-svc", CacheFile, Origin]);

        Assert.AreEqual(0, exitCode, StandardErrorText);
        Assert.AreEqual("hello|3", StandardOutputText);
        Assert.AreEqual(new ConnectTarget("localhost", 18443, true) { PoolScheme = "https" }, secondServer.QuicTargets.Single() with { Events = NoTransferEvents.Instance });
        Assert.AreEqual(0, secondServer.TcpConnectCount);
    }

    [TestMethod]
    public async Task RunAsync_H3EntryForTheOriginAndQuicFails_FallsBackToTcpAndWritesTheRenewedEntry()
    {
        CacheFileHolds(SameOriginH3Entry);
        ScriptedConnector tcp = new([Encoding.Latin1.GetBytes("HTTP/1.1 200 OK\r\nContent-Length: 2\r\nAlt-Svc: h3=\":18443\"\r\n\r\nhi")]);
        ScriptedQuicConnector connector = new(QuicRefused, tcp);

        int exitCode = await RunAsync(connector, ["-s", "-w", "|%{http_version}", "--alt-svc", CacheFile, Origin]);

        Assert.AreEqual(0, exitCode);
        Assert.AreEqual("hi|1.1", StandardOutputText);
        Assert.HasCount(1, connector.QuicTargets);
        Assert.IsNull(tcp.Targets.Single().AltSvcRoute);
        Assert.AreEqual(CacheFileText("h1 localhost 18443 h3 localhost 18443 \"20260930 11:05:35\" 0 0"), SavedCacheFile());
    }

    [TestMethod]
    public async Task RunAsync_H3EntryForAnotherPort_ConnectsThereOverHttp3AloneWithAltUsed()
    {
        CacheFileHolds(OtherPortH3Entry);
        ScriptedMultiplexedStream stream = new(0, Http3Response("hello"));
        ScriptedQuicConnector connector = new(
            MultiplexedConnectResult.Connected(new ScriptedMultiplexedConnection(stream) { RemoteEndPoint = new IPEndPoint(IPAddress.Loopback, 18444) }, null),
            new RecordingConnector(CurlExitCode.CouldntConnect, "unused"));

        int exitCode = await RunAsync(connector, ["-sS", "-w", "|%{http_version}", "--alt-svc", CacheFile, Origin]);

        Assert.AreEqual(0, exitCode, StandardErrorText);
        Assert.AreEqual("hello|3", StandardOutputText);
        Assert.AreEqual(new AltSvcRoute("h1", new AltSvcAlternative("h3", "localhost", 18444)), connector.QuicTargets.Single().AltSvcRoute);
        Assert.AreEqual(0, connector.TcpConnectCount);
        Assert.AreEqual(CacheFileText(OtherPortH3Entry), SavedCacheFile());
    }

    [TestMethod]
    public async Task RunAsync_H3EntryForAnotherPortAndQuicFails_FailsWithTheQuicErrorWithoutTryingTcp()
    {
        CacheFileHolds(OtherPortH3Entry);
        ScriptedQuicConnector connector = new(QuicRefused, new RecordingConnector(CurlExitCode.CouldntConnect, "unused"));

        int exitCode = await RunAsync(connector, ["-sS", "--alt-svc", CacheFile, Origin]);

        Assert.AreEqual((int)CurlExitCode.RecvError, exitCode);
        Assert.AreEqual($"curl: (56) {QuicRecvError}\n", StandardErrorText.Replace("\r\n", "\n", StringComparison.Ordinal));
        Assert.AreEqual(0, connector.TcpConnectCount);
        Assert.AreEqual(CacheFileText(OtherPortH3Entry), SavedCacheFile());
    }

    [TestMethod]
    public async Task RunAsync_H2EntryForAnotherPortAndAlpnAgreesH2_ConnectsThereOverHttp2()
    {
        CacheFileHolds(OtherPortH2Entry);
        ScriptedConnector server = new([Http2Response("hello")]) { ApplicationProtocol = "h2" };

        int exitCode = await RunAsync(server, ["-sS", "-w", "|%{http_version}", "--alt-svc", CacheFile, Origin]);

        Assert.AreEqual(0, exitCode, StandardErrorText);
        Assert.AreEqual("hello|2", StandardOutputText);
        Assert.AreEqual(new AltSvcRoute("h1", new AltSvcAlternative("h2", "localhost", 18444)), server.Targets.Single().AltSvcRoute);
        CollectionAssert.AreEqual(Http2Connection.ClientPreface.ToArray(), server.Written.Take(Http2Connection.ClientPreface.Length).ToArray());
    }

    [TestMethod]
    public async Task RunAsync_H2EntryForAnotherPortOnWindowsWithoutAVersionOption_OffersH2AloneAndSpeaksHttp2()
    {
        // curl.se 8.18.0, "h1 127.0.0.1 18736 h2 127.0.0.1 18735 ...": * ALPN: curl offers h2 (BL-733 Notes case 4).
        CacheFileHolds(OtherPortH2Entry);
        ScriptedConnector server = new([Http2Response("hello")]) { ApplicationProtocol = "h2", ReportsTheAlpnOffer = true };

        int exitCode = await RunAsync(server, ["-sSv", "-w", "|%{http_version}", "--alt-svc", CacheFile, Origin]);

        Assert.AreEqual(0, exitCode, StandardErrorText);
        Assert.AreEqual("hello|2", StandardOutputText);
        CollectionAssert.AreEqual(new[] { "h2" }, server.Targets.Single().ApplicationProtocols!.ToArray());
        StringAssert.Contains(StandardErrorLines, "* ALPN: curl offers h2\n");
    }

    [TestMethod]
    public async Task RunAsync_EntrySwitchingToH1_OffersHttp11Alone()
    {
        CacheFileHolds($"h2 localhost 18443 h1 localhost 18444 {Future} 0 0");
        ScriptedConnector server = new([Encoding.Latin1.GetBytes("HTTP/1.1 200 OK\r\nContent-Length: 2\r\n\r\nhi")]) { ReportsTheAlpnOffer = true };

        int exitCode = await RunAsync(server, ["-sSv", "--alt-svc", CacheFile, Origin]);

        Assert.AreEqual(0, exitCode, StandardErrorText);
        Assert.AreEqual(new AltSvcRoute("h2", new AltSvcAlternative("h1", "localhost", 18444)), server.Targets.Single().AltSvcRoute);
        CollectionAssert.AreEqual(new[] { "http/1.1" }, server.Targets.Single().ApplicationProtocols!.ToArray());
        StringAssert.Contains(StandardErrorLines, "* ALPN: curl offers http/1.1\n");
    }

    [TestMethod]
    [DataRow("h2")]
    [DataRow("h1")]
    public async Task RunAsync_Http3AndAnEntryNamingTheOriginWithATcpVersion_TriesTcpFirstBeforeHttp3(string alpn)
    {
        // curl 8.21.0's cf_hc_get_pref_alpn makes the entry's version the first attempt (BL-948).
        CacheFileHolds($"h1 localhost 18443 {alpn} localhost 18443 {Future} 0 0");
        ScriptedConnector tcp = new([Encoding.Latin1.GetBytes("HTTP/1.1 200 OK\r\nContent-Length: 2\r\n\r\nhi")]);
        ScriptedQuicConnector connector = new(MultiplexedConnectResult.Connected(new ScriptedMultiplexedConnection(new ScriptedMultiplexedStream(0, Http3Response("h3"))), null), tcp);

        int exitCode = await RunAsync(connector, ["-sS", "-w", "|%{http_version}", "--http3", "--alt-svc", CacheFile, Origin]);

        Assert.AreEqual(0, exitCode, StandardErrorText);
        Assert.AreEqual("hi|1.1", StandardOutputText);
        Assert.AreEqual(1, connector.TcpConnectCount);
        Assert.IsEmpty(connector.QuicTargets);
        Assert.IsNull(tcp.Targets.Single().AltSvcRoute);
    }

    [TestMethod]
    public async Task RunAsync_Http3AndAnH3EntryNamingTheOrigin_StillTriesHttp3First()
    {
        CacheFileHolds($"h1 localhost 18443 h3 localhost 18443 {Future} 0 0");
        ScriptedQuicConnector connector = Http3Server("hello");

        int exitCode = await RunAsync(connector, ["-sS", "-w", "|%{http_version}", "--http3", "--alt-svc", CacheFile, Origin]);

        Assert.AreEqual(0, exitCode, StandardErrorText);
        Assert.AreEqual("hello|3", StandardOutputText);
        Assert.AreEqual(0, connector.TcpConnectCount);
    }

    [TestMethod]
    public async Task RunAsync_H2EntryForAnotherPortAndNoAlpnAgreed_UsesHttp11ThereWithAltUsed()
    {
        // curl.se's build offers only h2 there and, when the server agrees on nothing, sends HTTP/1.1 (measured).
        CacheFileHolds(OtherPortH2Entry);
        ScriptedConnector server = new([Encoding.Latin1.GetBytes("HTTP/1.1 200 OK\r\nContent-Length: 2\r\n\r\nhi")]);

        int exitCode = await RunAsync(server, ["-sS", "--http3", "--alt-svc", CacheFile, Origin]);

        Assert.AreEqual(0, exitCode, StandardErrorText);
        Assert.AreEqual("hi", StandardOutputText);
        StringAssert.Contains(Encoding.Latin1.GetString(server.Written), "Alt-Used: localhost:18444\r\n");
    }

    [TestMethod]
    [DataRow("--http1.1", OtherPortH2Entry)]
    [DataRow("--http1.1", OtherPortH3Entry)]
    [DataRow("--http2", OtherPortH3Entry)]
    [DataRow("-0", $"h1 localhost 18443 h1 localhost 18444 {Future} 0 0")]
    [DataRow("--http2-prior-knowledge", OtherPortH2Entry)]
    public async Task RunAsync_EntryTheVersionOptionRulesOut_ConnectsToTheOrigin(string option, string entry)
    {
        CacheFileHolds(entry);
        ScriptedConnector server = new([Encoding.Latin1.GetBytes("HTTP/1.1 200 OK\r\nContent-Length: 2\r\n\r\nhi")]);

        await RunAsync(server, ["-s", option, "--alt-svc", CacheFile, Origin]);

        Assert.IsNull(server.Targets.Single().AltSvcRoute);
        Assert.AreEqual(CacheFileText(entry), SavedCacheFile());
    }

    [TestMethod]
    public async Task RunAsync_Http3OnlyAndAnEntryLearnedOverH1_ConnectsToTheOriginOverQuic()
    {
        CacheFileHolds(OtherPortH3Entry);
        ScriptedQuicConnector connector = Http3Server("hello");

        int exitCode = await RunAsync(connector, ["-sS", "--http3-only", "--alt-svc", CacheFile, Origin]);

        Assert.AreEqual(0, exitCode, StandardErrorText);
        Assert.IsNull(connector.QuicTargets.Single().AltSvcRoute);
    }

    [TestMethod]
    public async Task RunAsync_Http3OnlyAndAnEntryLearnedOverH3_ConnectsToItsAlternative()
    {
        string entry = $"h3 localhost 18443 h3 localhost 18444 {Future} 0 0";
        CacheFileHolds(entry);
        ScriptedQuicConnector connector = Http3Server("hello");

        int exitCode = await RunAsync(connector, ["-sS", "--http3-only", "--alt-svc", CacheFile, Origin]);

        Assert.AreEqual(0, exitCode, StandardErrorText);
        Assert.AreEqual(new AltSvcRoute("h3", new AltSvcAlternative("h3", "localhost", 18444)), connector.QuicTargets.Single().AltSvcRoute);
    }

    [TestMethod]
    public async Task RunAsync_Http3AndAnH1EntryForAnotherPort_RacesQuicThereAndFallsBackToTcpThere()
    {
        // curl.se's build keeps --http3's race when the entry names the version it was found under (measured).
        string entry = $"h1 localhost 18443 h1 localhost 18444 {Future} 0 0";
        CacheFileHolds(entry);
        ScriptedConnector tcp = new([Encoding.Latin1.GetBytes("HTTP/1.1 200 OK\r\nContent-Length: 2\r\n\r\nhi")]);
        ScriptedQuicConnector connector = new(QuicRefused, tcp);

        int exitCode = await RunAsync(connector, ["-s", "--http3", "--alt-svc", CacheFile, Origin]);

        Assert.AreEqual(0, exitCode);
        AltSvcRoute route = new("h1", new AltSvcAlternative("h1", "localhost", 18444));
        Assert.AreEqual(route, connector.QuicTargets.Single().AltSvcRoute);
        Assert.AreEqual(route, tcp.Targets.Single().AltSvcRoute);
    }

    /// <summary>
    /// curl stores a header under the version its response came over (BL-947): a run over HTTP/2 to
    /// <c>www.google.com</c> wrote <c>h2 www.google.com 443 h3 www.google.com 443 ...</c> (measured, BL-733 Notes).
    /// </summary>
    [TestMethod]
    public async Task RunAsync_AltSvcHeaderOverHttp2_WritesAnEntryLearnedUnderH2()
    {
        ScriptedConnector server = new([Http2Response("hello", ("alt-svc", "h3=\":18443\""))]) { ApplicationProtocol = "h2" };

        int exitCode = await RunAsync(server, ["-sS", "-w", "|%{http_version}", "--alt-svc", CacheFile, Origin]);

        Assert.AreEqual(0, exitCode, StandardErrorText);
        Assert.AreEqual("hello|2", StandardOutputText);
        Assert.AreEqual(CacheFileText($"h2 localhost 18443 h3 localhost 18443 {Future} 0 0"), SavedCacheFile());
    }

    /// <summary>
    /// A run over HTTP/3 to <c>www.google.com</c> added <c>h3 www.google.com 443 h3 www.google.com 443 ...</c>
    /// (measured, BL-733 Notes).
    /// </summary>
    [TestMethod]
    public async Task RunAsync_AltSvcHeaderOverHttp3_WritesAnEntryLearnedUnderH3()
    {
        ScriptedQuicConnector connector = new(
            MultiplexedConnectResult.Connected(new ScriptedMultiplexedConnection(new ScriptedMultiplexedStream(0, Http3Response("hello", ("alt-svc", "h3=\":18443\"")))), null),
            new RecordingConnector(CurlExitCode.CouldntConnect, "unused"));

        int exitCode = await RunAsync(connector, ["-sS", "-w", "|%{http_version}", "--http3-only", "--alt-svc", CacheFile, Origin]);

        Assert.AreEqual(0, exitCode, StandardErrorText);
        Assert.AreEqual("hello|3", StandardOutputText);
        Assert.AreEqual(CacheFileText($"h3 localhost 18443 h3 localhost 18443 {Future} 0 0"), SavedCacheFile());
    }

    [TestMethod]
    public async Task RunAsync_Http3OnlyAfterAnHttp3ResponseAdvertisedAnotherPort_ConnectsToThatAlternative()
    {
        ScriptedQuicConnector firstServer = new(
            MultiplexedConnectResult.Connected(new ScriptedMultiplexedConnection(new ScriptedMultiplexedStream(0, Http3Response("hi", ("alt-svc", "h3=\":18444\"")))), null),
            new RecordingConnector(CurlExitCode.CouldntConnect, "unused"));
        await RunAsync(firstServer, ["-s", "--http3-only", "--alt-svc", CacheFile, Origin]);
        files.ExistingContent[CacheFile] = files.Written[CacheFile].ToArray();
        ScriptedQuicConnector secondServer = Http3Server("hello");

        int exitCode = await RunAsync(secondServer, ["-sS", "--http3-only", "--alt-svc", CacheFile, Origin]);

        Assert.AreEqual(0, exitCode, StandardErrorText);
        Assert.AreEqual(new AltSvcRoute("h3", new AltSvcAlternative("h3", "localhost", 18444)), secondServer.QuicTargets.Single().AltSvcRoute);
    }

    [TestMethod]
    public async Task RunAsync_RedirectFromAnH3AlternativeToAnotherOrigin_ConnectsToItOverTcp()
    {
        CacheFileHolds(OtherPortH3Entry);
        ScriptedMultiplexedStream stream = new(0, Http3Response(string.Empty, ("location", "https://other.example:18445/")));
        ScriptedConnector tcp = new([Encoding.Latin1.GetBytes("HTTP/1.1 200 OK\r\nContent-Length: 2\r\n\r\nhi")]);
        ScriptedQuicConnector connector = new(MultiplexedConnectResult.Connected(new ScriptedMultiplexedConnection(stream), null), tcp);

        int exitCode = await RunAsync(connector, ["-sS", "-L", "--alt-svc", CacheFile, Origin]);

        Assert.AreEqual(0, exitCode, StandardErrorText);
        Assert.AreEqual("hi", StandardOutputText);
        Assert.HasCount(1, connector.QuicTargets);
        Assert.AreEqual("other.example", tcp.Targets.Single().Host);
        Assert.IsNull(tcp.Targets.Single().AltSvcRoute);
    }

    /// <summary>The file the run reads, its lines ending in LF.</summary>
    private void CacheFileHolds(params string[] entries) =>
        files.ExistingContent[CacheFile] = Encoding.Latin1.GetBytes(string.Join("\n", [.. AltSvcCacheHeader(), .. entries]) + "\n");

    private string SavedCacheFile() => Encoding.Latin1.GetString(files.Written[CacheFile].ToArray());

    /// <summary>The file as curl writes it: the two comments, then the entries, each line ending in the platform's newline.</summary>
    private static string CacheFileText(params string[] entries) =>
        string.Concat(((string[])[.. AltSvcCacheHeader(), .. entries]).Select(line => line + Environment.NewLine));

    private static string[] AltSvcCacheHeader() =>
    [
        "# Your alt-svc cache. https://curl.se/docs/alt-svc.html",
        "# This file was generated by libcurl! Edit at your own risk.",
    ];

    /// <summary>A connector whose QUIC connects all succeed and answer <paramref name="body" /> over HTTP/3; its TCP connects fail.</summary>
    private static ScriptedQuicConnector Http3Server(string body) =>
        new(
            MultiplexedConnectResult.Connected(new ScriptedMultiplexedConnection(new ScriptedMultiplexedStream(0, Http3Response(body))), null),
            new RecordingConnector(CurlExitCode.CouldntConnect, "unused"));

    /// <summary>
    /// A response on the HTTP/3 request stream: a 200 head, or a 302 head when <paramref name="extra" />
    /// names a <c>location</c>, with the body's length and the extra fields, then the body.
    /// </summary>
    private static byte[] Http3Response(string body, params (string Name, string Value)[] extra)
    {
        HeaderField[] fields =
        [
            new(":status", extra.Any(field => field.Name == "location") ? "302" : "200"),
            new("content-length", body.Length.ToString(CultureInfo.InvariantCulture)),
            .. extra.Select(field => new HeaderField(field.Name, field.Value)),
        ];
        byte[] head = new QpackEncoder(0, 0).EncodeFieldSection(0, fields);
        return [.. new Http3HeadersFrame(head).ToBytes(), .. new Http3DataFrame(Encoding.Latin1.GetBytes(body)).ToBytes()];
    }

    /// <summary>
    /// An HTTP/2 server's bytes: its settings and their acknowledgement, then a 200 on stream 1 with the
    /// body's length and the extra fields, then the body.
    /// </summary>
    private static byte[] Http2Response(string body, params (string Name, string Value)[] extra) =>
    [
        .. Http2FrameCodec.Serialize(Http2FrameFactory.CreateSettings([])),
        .. Http2FrameCodec.Serialize(Http2FrameFactory.CreateSettingsAcknowledgement()),
        .. Http2FrameCodec.Serialize(Http2FrameFactory.CreateHeaders(
            1,
            new HpackEncoder().Encode(
            [
                new(":status", "200"),
                new("content-length", body.Length.ToString(CultureInfo.InvariantCulture)),
                .. extra.Select(field => new HeaderField(field.Name, field.Value)),
            ]),
            isEndStream: false,
            isEndHeaders: true)),
        .. Http2FrameCodec.Serialize(Http2FrameFactory.CreateData(1, Encoding.Latin1.GetBytes(body), isEndStream: true)),
    ];

    private Task<int> RunAsync(IConnector connector, string[] arguments) =>
        new CurlCommandRunner(
                options => CreateTransferDispatch(connector),
                files,
                files,
                standardOutput,
                standardError,
                new MemoryStream(),
                runsOnWindows: true,
                timeProvider: new FixedUtcClock(Now))
            .RunAsync(arguments);

    private static TransferDispatch CreateTransferDispatch(IConnector connector) =>
        new(new ProtocolDispatcher(CurlComposition.CreateProtocolHandlers(
            connector,
            new RecordingDatagramConnector(CurlExitCode.CouldntConnect, "unused"),
            new PassThroughTlsProvider(),
            new LoopbackDnsResolver())));

    /// <summary>A clock stopped at one instant.</summary>
    private sealed class FixedUtcClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
