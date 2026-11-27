using System.Net;
using System.Net.Sockets;
using System.Text;
using Curl.Api.Codes;
using Curl.Api.EngineContracts;
using Curl.Api.EngineContracts.Multi;
using Curl.Api.EngineContracts.Share;
using Curl.Api.Options;
using Curl.Api.Options.Network;
using Curl.Api.Options.Tls;
using Curl.Cli;
using Curl.Console;
using Curl.Core;
using Curl.Networking;
using Curl.Protocol.Abstractions;
using Curl.Testing;

namespace Curl.Engine;

/// <summary>
/// Pins how the composition resolves through <c>--doh-url</c> (BL-642, ADR-0152), each case as curl
/// 8.21.0 (Schannel) was measured with <c>Record-CurlExchange.ps1 -Tls</c> standing in for the DoH
/// server on 2026-09-29 (BL-642 Notes): the DoH server's A answer <c>127.0.0.1</c> is dialled, a
/// <c>--resolve</c> entry for the host wins without asking the DoH server, and <c>--doh-url bogus</c>
/// and an <c>ftp://</c> DoH URL fail the transfer with exit 6. The DoH server and the transfer's
/// server are both <see cref="ScriptedConnector" />s, so no socket is opened.
/// </summary>
[TestClass]
public sealed class EngineCompositionDohTests
{
    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    private const string DohUrl = "https://127.0.0.1:48711/dns-query";

    // The measured A answer: ID 0, flags 0x8180, one question for example.test A IN, one answer
    // (a pointer to the question's name) A IN TTL 60 127.0.0.1. Every DoH connection got it.
    private static readonly byte[] AAnswer =
    [
        .. Encoding.Latin1.GetBytes("HTTP/1.1 200 OK\r\nContent-Type: application/dns-message\r\nContent-Length: 46\r\n\r\n"),
        0x00, 0x00, 0x81, 0x80, 0x00, 0x01, 0x00, 0x01, 0x00, 0x00, 0x00, 0x00,
        0x07, .. "example"u8, 0x04, .. "test"u8, 0x00, 0x00, 0x01, 0x00, 0x01,
        0xC0, 0x0C, 0x00, 0x01, 0x00, 0x01, 0x00, 0x00, 0x00, 0x3C, 0x00, 0x04, 0x7F, 0x00, 0x00, 0x01,
    ];

    [TestMethod]
    public void CreateDnsResolver_WithDohUrl_IsTheDohResolver()
    {
        IDnsResolver resolver = CreateDnsResolver("--doh-url", DohUrl);

        Diagnostics.Assert("resolver type", nameof(DohDnsResolver), resolver.GetType().Name);
        Assert.IsInstanceOfType<DohDnsResolver>(resolver);
    }

    [TestMethod]
    public void CreateDnsResolver_WithDohUrlAndDnsServers_AsksTheDohServer()
    {
        IDnsResolver resolver = CreateDnsResolver("--dns-servers", "192.0.2.1", "--doh-url", DohUrl);

        Diagnostics.Assert("resolver type", nameof(DohDnsResolver), resolver.GetType().Name);
        Assert.IsInstanceOfType<DohDnsResolver>(resolver);
    }

    [TestMethod]
    public void CreateDnsResolver_WithAnEmptyDohUrlLast_IsTheSystemResolver()
    {
        IDnsResolver resolver = CreateDnsResolver("--doh-url", DohUrl, "--doh-url", "");

        Diagnostics.Assert("resolver type", nameof(SystemDnsResolver), resolver.GetType().Name);
        Assert.IsInstanceOfType<SystemDnsResolver>(resolver);
    }

    [TestMethod]
    public void CreateTransports_WithDohUrl_SharesTheDohResolverBetweenBothConnectors()
    {
        CurlTransports transports = EngineComposition.CreateTransports(EasyOptionsFromCommandLine.ToolOptionsOf(Parse("--doh-url", DohUrl)), EasyOptionsFromCommandLine.NetworkOf(Parse("--doh-url", DohUrl)), EasyOptionsFromCommandLine.TlsOf(Parse("--doh-url", DohUrl)), EasyOptionsFromCommandLine.HttpOf(Parse("--doh-url", DohUrl)), EasyOptionsFromCommandLine.ProxyOf(Parse("--doh-url", DohUrl)), EasyOptionsFromCommandLine.AuthenticationOf(Parse("--doh-url", DohUrl)));
        Diagnostics.Act("resolver type", transports.DnsResolver.GetType().Name);

        Diagnostics.Assert("resolver type", nameof(DohDnsResolver), transports.DnsResolver.GetType().Name);
        Assert.IsInstanceOfType<DohDnsResolver>(transports.DnsResolver);
    }

    [TestMethod]
    public async Task Connect_WithTheMeasuredAAnswer_DialsTheAnsweredAddressAndReportsItResolved()
    {
        // curl -sS -v --doh-url https://127.0.0.1:48711/dns-query --doh-insecure http://example.test:48712/
        // -> two POSTs to the DoH server, then "* Host example.test:48712 was resolved." / "* IPv6: (none)" /
        // "* IPv4: 127.0.0.1" / "*   Trying 127.0.0.1:48712...", the GET to the second server, exit 0.
        // The DoH server leaves the connection open, so the second POST goes on it (ADR-1441).
        ScriptedConnector dohServer = new([AAnswer, AAnswer]);
        ScriptedConnector webServer = new([]);
        RecordingEvents events = new();

        ConnectResult result = await ConnectAsync(DohUrl, dohServer, webServer, events, "--doh-insecure");

        Diagnostics.Assert("result.Connection is not null", true, result.Connection is not null);
        Assert.IsNotNull(result.Connection);
        Diagnostics.Assert("dohServer.Targets count", 1, dohServer.Targets.Count);
        Assert.HasCount(1, dohServer.Targets);
        Assert.AreEqual(new ConnectTarget("127.0.0.1", 48711, true), dohServer.Targets[0] with { PoolScheme = null, ApplicationProtocols = null });
        CollectionAssert.AreEqual(HttpApplicationProtocols.H2ThenHttp11.ToArray(), dohServer.Targets[0].ApplicationProtocols!.ToArray());
        Diagnostics.Assert("dohServer.Targets[0].PoolScheme", "https", dohServer.Targets[0].PoolScheme);
        Assert.AreEqual("https", dohServer.Targets[0].PoolScheme);
        Diagnostics.Diff("DoH requests", MeasuredPost(0x01) + MeasuredPost(0x1C), Encoding.Latin1.GetString(dohServer.Written));
        Assert.AreEqual(MeasuredPost(0x01) + MeasuredPost(0x1C), Encoding.Latin1.GetString(dohServer.Written));
        Diagnostics.Assert("webServer.Targets count", 1, webServer.Targets.Count);
        Assert.HasCount(1, webServer.Targets);
        Diagnostics.Assert("webServer.Targets[0].Host", "127.0.0.1", webServer.Targets[0].Host);
        Assert.AreEqual("127.0.0.1", webServer.Targets[0].Host);
        Diagnostics.Assert("webServer.Targets[0].Port", 48712, webServer.Targets[0].Port);
        Assert.AreEqual(48712, webServer.Targets[0].Port);
        CollectionAssert.AreEqual(
            new[] { "Host example.test:48712 was resolved.", "IPv6: (none)", "IPv4: 127.0.0.1", "  Trying 127.0.0.1:48712..." },
            events.Info.ToArray());
    }

    [TestMethod]
    [DataRow("doh")]
    [DataRow("dns")]
    [DataRow("network")]
    public async Task Connect_UnderTraceConfigDnsOrDoh_WritesTheDnsFilterAndDohLinesToTheTransfer(string component)
    {
        // curl -v --trace-config doh (or dns, which writes the same) --doh-url ... http://example.test:P/:
        // the filter's lines, each query's completed line or, for the AAAA answer that does not decode,
        // its error line, then the resolve and Trying (measured, BL-1102 Notes; the error line as
        // BL-2970 measured it with curl 8.22.0), each DoH sub-transfer's
        // [DoH] lines before them, AAAA's first (BL-1180, BL-2973; the scripted DoH server reports no connect lines); network's [HAPPY-EYEBALLS], [TCP] and [TIMER] lines (BL-1161, BL-1186) aside.
        ScriptedConnector dohServer = new([AAnswer, AAnswer]);
        RecordingEvents events = new();

        ConnectResult result = await ConnectAsync(DohUrl, dohServer, new ScriptedConnector([]), events, "--doh-insecure", "-v", "--trace-config", component);

        Diagnostics.Assert("result.Connection is not null", true, result.Connection is not null);
        Assert.IsNotNull(result.Connection);
        CollectionAssert.AreEqual(
            new[]
            {
                "[DNS] created DNS filter for example.test:48712, transport=3, queries=3",
                "[DNS] added",
                "[DNS] cf_dns_start host example.test:48712",
                "[DoH] using HTTP/1.x",
                "[DoH] upload completely sent off: 30 bytes",
                "[DoH] Connection #1 to host 127.0.0.1:48711 left intact",
                "[DoH] [DoH] [AAAA] error decoding response: Unexpected TYPE",
                "[DoH] Reusing existing https: connection with host 127.0.0.1",
                "[DoH] upload completely sent off: 30 bytes",
                "[DoH] Connection #1 to host 127.0.0.1:48711 left intact",
                "[DoH] DoH request complete, 0 to go",
                "[DNS] resolve complete for example.test:48712",
                "Host example.test:48712 was resolved.",
                "IPv6: (none)",
                "IPv4: 127.0.0.1",
                "  Trying 127.0.0.1:48712...",
                "[DNS] Curl_conn_connect(block=0) -> 0, done=0",
                "[DNS] connected filter chain below",
                "[DNS] Curl_conn_connect(block=0) -> 0, done=1",
                "[DNS] removing connected setup filter",
                "[DNS] destroy",
            },
            events.Info.Where(line => !line.StartsWith("[HAPPY-EYEBALLS]", StringComparison.Ordinal) && !line.StartsWith("[TCP]", StringComparison.Ordinal) && !line.StartsWith("[TIMER]", StringComparison.Ordinal)).ToArray());
    }

    [TestMethod]
    public async Task Connect_UnderAnotherTraceComponent_WritesNoDnsLine()
    {
        ScriptedConnector dohServer = new([AAnswer, AAnswer]);
        RecordingEvents events = new();

        await ConnectAsync(DohUrl, dohServer, new ScriptedConnector([]), events, "--doh-insecure", "-v", "--trace-config", "tls,http/1");

        Diagnostics.Assert("[DNS] or [DoH] line present", false, events.Info.Any(line => line.StartsWith("[DNS]", StringComparison.Ordinal) || line.StartsWith("[DoH]", StringComparison.Ordinal)));
        Assert.IsFalse(events.Info.Any(line => line.StartsWith("[DNS]", StringComparison.Ordinal) || line.StartsWith("[DoH]", StringComparison.Ordinal)));
    }

    [TestMethod]
    public async Task Connect_WithIpv4Only_PostsOnlyTheAQuery()
    {
        // curl -sS -4 --doh-url https://127.0.0.1:P1/dns-query --doh-insecure http://example.test:P2/
        // -> one POST to the DoH server, QTYPE A (measured, BL-642).
        ScriptedConnector dohServer = new([AAnswer]);
        ScriptedConnector webServer = new([]);

        ConnectResult result = await ConnectAsync(DohUrl, dohServer, webServer, new RecordingEvents(), "--doh-insecure", "-4");

        Diagnostics.Assert("result.Connection is not null", true, result.Connection is not null);
        Assert.IsNotNull(result.Connection);
        Diagnostics.Assert("dohServer.Targets count", 1, dohServer.Targets.Count);
        Assert.HasCount(1, dohServer.Targets);
        Diagnostics.Diff("DoH requests", MeasuredPost(0x01), Encoding.Latin1.GetString(dohServer.Written));
        Assert.AreEqual(MeasuredPost(0x01), Encoding.Latin1.GetString(dohServer.Written));
        Diagnostics.Assert("webServer.Targets[0].Host", "127.0.0.1", webServer.Targets[0].Host);
        Assert.AreEqual("127.0.0.1", webServer.Targets[0].Host);
    }

    [TestMethod]
    public async Task Connect_WithResolveForTheSameHost_DialsTheEntryWithoutAskingTheDohServer()
    {
        // curl -v --resolve example.test:48712:127.0.0.1 --doh-url ... --doh-insecure http://example.test:48712/
        // -> "[DNS] added ... to cache", "Hostname example.test was found in DNS cache", no byte to the DoH server.
        ScriptedConnector dohServer = new([AAnswer, AAnswer]);
        ScriptedConnector webServer = new([]);

        ConnectResult result = await ConnectAsync(
            DohUrl, dohServer, webServer, new RecordingEvents(), "--doh-insecure", "--resolve", "example.test:48712:192.0.2.7");

        Diagnostics.Assert("result.Connection is not null", true, result.Connection is not null);
        Assert.IsNotNull(result.Connection);
        Diagnostics.Assert("dohServer.Targets count", 0, dohServer.Targets.Count);
        Assert.IsEmpty(dohServer.Targets);
        Diagnostics.Assert("webServer.Targets[0].Host", "192.0.2.7", webServer.Targets[0].Host);
        Assert.AreEqual("192.0.2.7", webServer.Targets[0].Host);
    }

    [TestMethod]
    public async Task Connect_WithBogusDohUrl_AsksHostBogusOverHttpsAndFailsWithExit6()
    {
        // curl -sS -v --doh-url bogus http://example.test:48712/ -> "* Could not resolve host: example.test",
        // "curl: (6) Could not resolve host: example.test"; nothing reached either server.
        ScriptedConnector dohServer = new([]);
        ScriptedConnector webServer = new([]);

        ConnectResult result = await ConnectAsync("bogus", dohServer, webServer, new RecordingEvents());

        Diagnostics.Assert("result.ExitCode", CurlExitCode.CouldntResolveHost, result.ExitCode);
        Assert.AreEqual(CurlExitCode.CouldntResolveHost, result.ExitCode);
        Diagnostics.Assert("result.ErrorMessage", "Could not resolve host: example.test", result.ErrorMessage);
        Assert.AreEqual("Could not resolve host: example.test", result.ErrorMessage);
        Assert.AreEqual(new ConnectTarget("bogus", 443, true), dohServer.Targets[0] with { PoolScheme = null, ApplicationProtocols = null });
        Diagnostics.Assert("webServer.Targets count", 0, webServer.Targets.Count);
        Assert.IsEmpty(webServer.Targets);
    }

    [TestMethod]
    public async Task Connect_WithFtpDohUrl_FailsWithExit6WithoutAskingAnyone()
    {
        // curl -sS -v --doh-url ftp://127.0.0.1:48711/ http://example.test:48712/ -> exit 6, nothing sent.
        ScriptedConnector dohServer = new([AAnswer, AAnswer]);
        ScriptedConnector webServer = new([]);

        ConnectResult result = await ConnectAsync("ftp://127.0.0.1:48711/", dohServer, webServer, new RecordingEvents());

        Diagnostics.Assert("result.ExitCode", CurlExitCode.CouldntResolveHost, result.ExitCode);
        Assert.AreEqual(CurlExitCode.CouldntResolveHost, result.ExitCode);
        Diagnostics.Assert("dohServer.Targets count", 0, dohServer.Targets.Count);
        Assert.IsEmpty(dohServer.Targets);
        Diagnostics.Assert("webServer.Targets count", 0, webServer.Targets.Count);
        Assert.IsEmpty(webServer.Targets);
    }

    [TestMethod]
    public void CreateDohResolver_WithAUrlThatDoesNotParse_ResolvesNothing()
    {
        Diagnostics.Arrange("DoH URL", "http://[bad/");
        IDnsResolver resolver = EngineComposition.CreateDohResolver("http://[bad/", new ScriptedConnector([]), AddressFamily.Unspecified);
        Diagnostics.Act("resolver type", resolver.GetType().Name);

        Diagnostics.Assert("resolver type", nameof(UnusableDohUrlResolver), resolver.GetType().Name);
        Assert.IsInstanceOfType<UnusableDohUrlResolver>(resolver);
    }

    [TestMethod]
    public async Task UnusableDohUrlResolver_ResolvesANameToNoAddress()
    {
        Diagnostics.Arrange("name", "example.test");

        var addresses = await new UnusableDohUrlResolver().ResolveAsync("example.test", CancellationToken.None);
        Diagnostics.Act("addresses", string.Join(", ", addresses));

        Diagnostics.Assert("address count", 0, addresses.Count);
        Assert.IsEmpty(addresses);
    }

    [TestMethod]
    [DataRow("https://127.0.0.1:48711/dns-query", "https://127.0.0.1:48711/dns-query")]
    [DataRow("HTTPS://dns.example/q", "https://dns.example/q")]
    [DataRow("http://dns.example/", "http://dns.example/")]
    [DataRow("127.0.0.1:48711/dns-query", "https://127.0.0.1:48711/dns-query")]
    [DataRow("bogus", "https://bogus/")]
    public void DohUrlOf_MakesTheUrlCurlAsks(string value, string expected)
    {
        Diagnostics.Arrange("value", value);
        Uri? url = EngineComposition.DohUrlOf(value);
        Diagnostics.Act("DoH URL", url);

        Diagnostics.Assert("DoH URL", new Uri(expected), url);
        Assert.AreEqual(new Uri(expected), EngineComposition.DohUrlOf(value));
    }

    [TestMethod]
    [DataRow("ftp://127.0.0.1:48711/")]
    [DataRow("http://[bad/")]
    [DataRow("://")]
    public void DohUrlOf_ANonHttpOrUnparsableValue_IsNone(string value)
    {
        Diagnostics.Arrange("value", value);
        Uri? url = EngineComposition.DohUrlOf(value);
        Diagnostics.Act("DoH URL", url?.ToString() ?? "(none)");

        Diagnostics.Assert("DoH URL is none", true, url is null);
        Assert.IsNull(EngineComposition.DohUrlOf(value));
    }

    [TestMethod]
    public void DohFromCommandLine_TakesTheDohFlagsAndNotTheTransfersInsecureOrCertStatus()
    {
        TlsClientOptions dohOnly = TlsClientOptionsMapping.DohOf(TlsOf("--doh-insecure", "--doh-cert-status"));
        TlsClientOptions transferOnly = TlsClientOptionsMapping.DohOf(TlsOf("-k", "--cert-status"));
        Diagnostics.Act("DoH-only options", dohOnly);
        Diagnostics.Act("transfer-only options", transferOnly);

        Diagnostics.Assert("DoH-only insecure", true, dohOnly.Insecure);
        Assert.IsTrue(dohOnly.Insecure);
        // The Schannel build ignores --doh-cert-status (CGR-154).
        Diagnostics.Assert("DoH-only requires certificate status", !OperatingSystem.IsWindows(), dohOnly.RequireCertificateStatus);
        Assert.AreEqual(!OperatingSystem.IsWindows(), dohOnly.RequireCertificateStatus);
        Diagnostics.Assert("transferOnly", new TlsClientOptions(), transferOnly);
        Assert.AreEqual(new TlsClientOptions(), transferOnly);
    }

    [TestMethod]
    public void DohFromCommandLine_CopiesTheTransfersTrustOptionsAsCurlsDohCopiesThem()
    {
        // curl --ssl-no-revoke --cacert root.pem --doh-url ... (no --doh-insecure) verified the DoH server's
        // certificate against root.pem and resolved; -k alone did not reach it (measured).
        TlsClientOptions options = TlsClientOptionsMapping.DohOf(TlsOf(
            "--cacert", "root.pem",
            "--capath", "certs",
            "--crlfile", "list.crl",
            "--curves", "X25519",
            "--ssl-no-revoke",
            "--ssl-revoke-best-effort",
            "--ssl-auto-client-cert",
            "--cert", "client.pem",
            "--tlsv1.3"));
        Diagnostics.Act("DoH options", options);

        Diagnostics.Assert("DoH CA file", "root.pem", options.CaCertificateFile);
        Assert.AreEqual(
            new TlsClientOptions(
                CaCertificateFile: "root.pem",
                CaCertificateDirectory: "certs",
                SkipRevocationCheck: true,
                RevocationCheckBestEffort: true,
                Curves: "X25519",
                AutoClientCertificate: true,
                CertificateRevocationListFile: "list.crl"),
            options);
    }

    [TestMethod]
    public void CreateDohConnector_OffersOnlyHttp11AndTakesNoResolveEntries()
    {
        TcpConnector connector = EngineComposition.CreateDohConnector(
            EngineTraceComponents.None, new EasyTlsOptions(), new TcpDialer(), TimeProvider.System);
        Diagnostics.Act("ALPN offer", string.Join(", ", connector.HttpOverTlsApplicationProtocols.ToArray()));
        Diagnostics.Act("Unix socket", connector.UnixSocket?.ToString() ?? "(none)");

        Diagnostics.Assert("ALPN offer", string.Join(", ", HttpApplicationProtocols.Http11Only.ToArray()), string.Join(", ", connector.HttpOverTlsApplicationProtocols.ToArray()));
        Diagnostics.Assert("Unix socket is none", true, connector.UnixSocket is null);
        CollectionAssert.AreEqual(HttpApplicationProtocols.Http11Only.ToArray(), connector.HttpOverTlsApplicationProtocols.ToArray());
        Assert.IsNull(connector.UnixSocket);
    }

    private static string MeasuredPost(byte queryType) =>
        "POST /dns-query HTTP/1.1\r\n"
        + "Host: 127.0.0.1:48711\r\n"
        + "Accept: */*\r\n"
        + "Content-Type: application/dns-message\r\n"
        + "Content-Length: 30\r\n"
        + "\r\n"
        + "\0\0\u0001\0\0\u0001\0\0\0\0\0\0\u0007example\u0004test\0\0" + (char)queryType + "\0\u0001";

    private IDnsResolver CreateDnsResolver(params string[] arguments)
    {
        IDnsResolver resolver = EngineComposition.CreateDnsResolver(EasyOptionsFromCommandLine.NetworkOf(Parse(arguments)), EasyOptionsFromCommandLine.TlsOf(Parse(arguments)), TimeProvider.System, new TcpDialer());
        Diagnostics.Act("resolver type", resolver.GetType().Name);
        return resolver;
    }

    /// <summary>
    /// Builds the DoH resolver the composition makes of <paramref name="dohUrl" /> over
    /// <paramref name="dohServer" />, and the production TCP connector for the parsed
    /// <paramref name="arguments" /> over that resolver and <paramref name="webServer" />, then connects to
    /// <c>example.test:48712</c> as the measured transfer does.
    /// </summary>
    private async Task<ConnectResult> ConnectAsync(
        string dohUrl,
        ScriptedConnector dohServer,
        ScriptedConnector webServer,
        RecordingEvents events,
        params string[] arguments)
    {
        CommandLineOptions options = Parse([.. arguments, "--doh-url", dohUrl]);
        FlowScopedTransferEvents? resolverEvents = EngineComposition.TracesDns(EasyOptionsFromCommandLine.TraceComponentsOf(options)) ? new() : null;
        TcpConnector connector = EngineComposition.CreateTcpConnector(
            EasyOptionsFromCommandLine.ToolOptionsOf(options), EasyOptionsFromCommandLine.TraceComponentsOf(options), EasyOptionsFromCommandLine.NetworkOf(options), EasyOptionsFromCommandLine.HttpOf(options), EasyOptionsFromCommandLine.ProxyOf(options), EasyOptionsFromCommandLine.AuthenticationOf(options),
            EngineComposition.CreateDohResolver(dohUrl, dohServer, EngineComposition.AddressFamilyOf(EasyOptionsFromCommandLine.NetworkOf(options)), resolverEvents),
            new ScriptedTcpDialer(webServer),
            new PassThroughTlsProvider(),
            TimeProvider.System,
            HttpProxyTunnelOptions.Default,
            resolverEvents: resolverEvents);

        ConnectResult result;
        using (Diagnostics.Phase("connect"))
        {
            result = await connector.ConnectAsync(new ConnectTarget("example.test", 48712, false) { Events = events }, CancellationToken.None);
        }

        Diagnostics.Act("exit code", result.ExitCode);
        Diagnostics.Act("error message", result.ErrorMessage);
        Diagnostics.Act("info lines", string.Join(" | ", events.Info));
        Diagnostics.Bytes("DoH requests", dohServer.Written);
        Diagnostics.Act("web targets", string.Join(", ", webServer.Targets.Select(target => $"{target.Host}:{target.Port}")));
        return result;
    }

    private const string PlainDohUrl = "http://127.0.0.1:48711/dns-query";

    [TestMethod]
    public async Task DisposeAsync_OfTransports_ClosesTheConnectionsTheDohResolverKept()
    {
        DohRecordingDialer dialer = new(AAnswer);
        CurlTransports transports = DialingTransports(PlainDohUrl, dialer, new LoopbackDnsResolver());
        await transports.DnsResolver.ResolveAsync("example.test", CancellationToken.None);
        int closedBefore = dialer.Dialled.Count(connection => connection.IsDisposed);

        await transports.DisposeAsync();

        Diagnostics.Assert("closed before the dispose", 0, closedBefore);
        Assert.AreEqual(0, closedBefore);
        Diagnostics.Assert("DoH connections opened", 2, dialer.Dialled.Count);
        Assert.HasCount(2, dialer.Dialled);
        Assert.IsTrue(dialer.Dialled.All(connection => connection.IsDisposed));
    }

    [TestMethod]
    public async Task DisposeAsync_OfTransportsWithoutADohUrl_LeavesTheCallersResolverAlone()
    {
        DisposalRecordingResolver resolver = new();
        CurlTransports transports = DialingTransports(string.Empty, new DohRecordingDialer(AAnswer), resolver);

        await transports.DisposeAsync();

        Diagnostics.Assert("caller's resolver disposed", false, resolver.IsDisposed);
        Assert.AreSame(resolver, transports.DnsResolver);
        Assert.IsFalse(resolver.IsDisposed);
    }

    [TestMethod]
    public async Task DisposeAsync_OfADispatchHoldingTheTransports_ClosesTheConnectionsTheDohResolverKept()
    {
        DohRecordingDialer dialer = new(AAnswer);
        CurlTransports transports = DialingTransports(PlainDohUrl, dialer, new LoopbackDnsResolver());
        await transports.DnsResolver.ResolveAsync("example.test", CancellationToken.None);
        TransferDispatch dispatch = new(new ProtocolDispatcher([]), [], connectionPool: transports.PoolingConnector)
        {
            KeptDohConnections = transports,
        };

        await dispatch.DisposeAsync();

        Diagnostics.Assert("DoH connections opened", 2, dialer.Dialled.Count);
        Assert.HasCount(2, dialer.Dialled);
        Assert.IsTrue(dialer.Dialled.All(connection => connection.IsDisposed));
    }

    [TestMethod]
    public async Task PerformAsync_TwiceWithTheSameDohUrl_OpensNoNewDohConnectionForTheSecondHost()
    {
        DohRecordingDialer dialer = new(AAnswer) { OtherPortReply = EmptyWebResponse };
        await using IEasyTransfer easy = DialingEngine(dialer).CreateEasy();
        await PerformThroughDohAsync(easy, "http://example.test:48712/", new EasyNetworkOptions { DohUrl = PlainDohUrl });
        List<DisposalRecordingConnection> openedByTheFirst = dialer.DohConnections;

        await PerformThroughDohAsync(easy, "http://other.test:48712/", easy.Options.Network);

        Diagnostics.Assert("DoH connections after the second perform", 2, dialer.DohConnections.Count);
        Assert.HasCount(2, openedByTheFirst);
        CollectionAssert.AreEqual(openedByTheFirst, dialer.DohConnections);
        // Each connection carried one query of each name, and stays open for a later perform.
        Assert.IsTrue(openedByTheFirst.All(connection => connection.Requests == 2));
        Assert.IsFalse(openedByTheFirst.Any(connection => connection.IsDisposed));
    }

    [TestMethod]
    public async Task DisposeAsync_OfAHandle_ClosesTheKeptDohConnections()
    {
        DohRecordingDialer dialer = new(AAnswer) { OtherPortReply = EmptyWebResponse };
        IEasyTransfer easy = DialingEngine(dialer).CreateEasy();
        await PerformThroughDohAsync(easy, "http://example.test:48712/", new EasyNetworkOptions { DohUrl = PlainDohUrl });
        easy.Reset();
        int closedBefore = dialer.DohConnections.Count(connection => connection.IsDisposed);

        await easy.DisposeAsync();

        Diagnostics.Assert("closed before the dispose", 0, closedBefore);
        Assert.AreEqual(0, closedBefore);
        Assert.HasCount(2, dialer.DohConnections);
        Assert.IsTrue(dialer.DohConnections.All(connection => connection.IsDisposed));
    }

    [TestMethod]
    [DataRow("doh url")]
    [DataRow("doh verify peer")]
    [DataRow("resolve")]
    public async Task PerformAsync_AfterADohSettingChanges_OpensNewConnectionsAndKeepsTheEarlierOnes(string setting)
    {
        DohRecordingDialer dialer = new(AAnswer) { OtherPortReply = EmptyWebResponse };
        await using IEasyTransfer easy = DialingEngine(dialer).CreateEasy();
        EasyNetworkOptions first = new() { DohUrl = PlainDohUrl };
        EasyTlsOptions firstTls = easy.Options.Tls;
        await PerformThroughDohAsync(easy, "http://example.test:48712/", first);
        if (setting == "doh verify peer")
        {
            easy.Options = easy.Options with { Tls = firstTls with { DohVerifyPeer = false } };
        }

        EasyNetworkOptions changed = setting switch
        {
            "doh url" => first with { DohUrl = PlainDohUrl + "?other" },
            "resolve" => first with { Resolve = ["unused.test:80:192.0.2.1"] },
            _ => first,
        };

        await PerformThroughDohAsync(easy, "http://other.test:48712/", changed);

        Diagnostics.Assert("DoH connections after the second perform", 4, dialer.DohConnections.Count);
        Assert.HasCount(4, dialer.DohConnections);
        Assert.IsFalse(dialer.DohConnections.Any(connection => connection.IsDisposed));

        easy.Options = easy.Options with { Tls = firstTls };
        await PerformThroughDohAsync(easy, "http://third.test:48712/", first);

        // The first settings' connections are still there to ask on.
        Assert.HasCount(4, dialer.DohConnections);
    }

    [TestMethod]
    public async Task PerformAsync_WithMoreDohSettingsThanAreKept_ClosesTheOneAskedForLongestAgo()
    {
        DohRecordingDialer dialer = new(AAnswer) { OtherPortReply = EmptyWebResponse };
        await using IEasyTransfer easy = DialingEngine(dialer).CreateEasy();
        EasyNetworkOptions OfSetting(int number) => new() { DohUrl = PlainDohUrl + "?" + number };
        for (int number = 0; number < KeptDohResolver.MostKept; number++)
        {
            await PerformThroughDohAsync(easy, $"http://host{number}.test:48712/", OfSetting(number));
        }

        // Setting 0 is asked for again, so setting 1 is the one to make room for one more.
        await PerformThroughDohAsync(easy, "http://again.test:48712/", OfSetting(0));
        await PerformThroughDohAsync(easy, "http://more.test:48712/", OfSetting(KeptDohResolver.MostKept));

        List<DisposalRecordingConnection> opened = dialer.DohConnections;
        Diagnostics.Assert("DoH connections opened", 2 * (KeptDohResolver.MostKept + 1), opened.Count);
        Assert.HasCount(2 * (KeptDohResolver.MostKept + 1), opened);
        CollectionAssert.AreEqual(opened.Skip(2).Take(2).ToArray(), opened.Where(connection => connection.IsDisposed).ToArray());
    }

    [TestMethod]
    public async Task ResolverFor_EqualResolveEntriesAndOtherConnectToMappings_GivesTheKeptResolver()
    {
        await using KeptDohResolver keeper = new();
        EasyNetworkOptions network = new() { DohUrl = PlainDohUrl, Resolve = ["a.test:80:192.0.2.1"], ConnectTo = ["::b.test:"] };
        Func<IDnsResolver> create = () => EngineComposition.CreateDohResolver(PlainDohUrl, new ScriptedConnector([]), AddressFamily.Unspecified, readEnvironmentVariable: _ => null);
        IDnsResolver kept = keeper.ResolverFor(network, new EasyTlsOptions(), create);

        IDnsResolver again = keeper.ResolverFor(network with { Resolve = ["a.test:80:192.0.2.1"], ConnectTo = ["::c.test:"] }, new EasyTlsOptions(), create);

        Diagnostics.Assert("kept resolver given again", true, ReferenceEquals(kept, again));
        Assert.AreSame(kept, again);
    }

    [TestMethod]
    public async Task PerformAsync_ByASecondHandleInTheSameMulti_OpensNoNewDohConnection()
    {
        DohRecordingDialer dialer = new(AAnswer) { OtherPortReply = EmptyWebResponse };
        ManagedCurlEngine engine = DialingEngine(dialer);
        await using IMultiTransfer multi = engine.CreateMulti();
        await using IEasyTransfer first = engine.CreateEasy();
        await using IEasyTransfer second = engine.CreateEasy();
        await PerformInMultiThroughDohAsync(multi, first, "http://example.test:48712/");
        List<DisposalRecordingConnection> openedByTheFirst = dialer.DohConnections;

        await PerformInMultiThroughDohAsync(multi, second, "http://other.test:48712/");

        Diagnostics.Assert("DoH connections after the second handle's perform", 2, dialer.DohConnections.Count);
        Assert.HasCount(2, openedByTheFirst);
        CollectionAssert.AreEqual(openedByTheFirst, dialer.DohConnections);
        Assert.IsTrue(openedByTheFirst.All(connection => connection.Requests == 2));
    }

    [TestMethod]
    public async Task PerformAsync_ByASecondHandleSharingConnections_OpensNoNewDohConnection()
    {
        DohRecordingDialer dialer = new(AAnswer) { OtherPortReply = EmptyWebResponse };
        ManagedCurlEngine engine = DialingEngine(dialer);
        IShareHandle share = ShareOfConnections(engine);
        await using IEasyTransfer first = SharingEasy(engine, share);
        await using IEasyTransfer second = SharingEasy(engine, share);
        await PerformThroughDohAsync(first, "http://example.test:48712/", new EasyNetworkOptions { DohUrl = PlainDohUrl });
        List<DisposalRecordingConnection> openedByTheFirst = dialer.DohConnections;

        await PerformThroughDohAsync(second, "http://other.test:48712/", new EasyNetworkOptions { DohUrl = PlainDohUrl });

        Diagnostics.Assert("DoH connections after the second handle's perform", 2, dialer.DohConnections.Count);
        Assert.HasCount(2, openedByTheFirst);
        CollectionAssert.AreEqual(openedByTheFirst, dialer.DohConnections);
        Assert.IsTrue(openedByTheFirst.All(connection => connection.Requests == 2));
    }

    [TestMethod]
    [DataRow("dispose")]
    [DataRow("clear connections")]
    public async Task Multi_CleanedUpOrToldToClearConnections_ClosesTheDohConnectionsItsHandlesKept(string ending)
    {
        DohRecordingDialer dialer = new(AAnswer) { OtherPortReply = EmptyWebResponse };
        ManagedCurlEngine engine = DialingEngine(dialer);
        IMultiTransfer multi = engine.CreateMulti();
        IEasyTransfer easy = engine.CreateEasy();
        await PerformInMultiThroughDohAsync(multi, easy, "http://example.test:48712/");
        await easy.DisposeAsync();
        int closedBefore = dialer.DohConnections.Count(connection => connection.IsDisposed);

        if (ending == "dispose")
        {
            await multi.DisposeAsync();
        }
        else
        {
            // These connections close without waiting, so they are closed when the setter returns.
            multi.Options = multi.Options with { NetworkChanged = CurlNetworkChange.ClearConnections };
        }

        Diagnostics.Assert("closed by the easy handle's cleanup", 0, closedBefore);
        Assert.AreEqual(0, closedBefore);
        Assert.HasCount(2, dialer.DohConnections);
        Assert.IsTrue(dialer.DohConnections.All(connection => connection.IsDisposed));
        await multi.DisposeAsync();
    }

    [TestMethod]
    public async Task Cleanup_OfAShareHandle_ClosesTheDohConnectionsItsHandlesKept()
    {
        DohRecordingDialer dialer = new(AAnswer) { OtherPortReply = EmptyWebResponse };
        ManagedCurlEngine engine = DialingEngine(dialer);
        IShareHandle share = ShareOfConnections(engine);
        IEasyTransfer easy = SharingEasy(engine, share);
        await PerformThroughDohAsync(easy, "http://example.test:48712/", new EasyNetworkOptions { DohUrl = PlainDohUrl });
        await easy.DisposeAsync();
        int closedBefore = dialer.DohConnections.Count(connection => connection.IsDisposed);

        CurlShareCode code = share.Cleanup();

        Diagnostics.Assert("closed by the handle's cleanup", 0, closedBefore);
        Assert.AreEqual(0, closedBefore);
        Assert.AreEqual(CurlShareCode.Ok, code);
        Assert.HasCount(2, dialer.DohConnections);
        Assert.IsTrue(dialer.DohConnections.All(connection => connection.IsDisposed));
    }

    [TestMethod]
    public async Task DisposeAsync_OfTheEngine_ClosesTheKeptDohConnections()
    {
        DohRecordingDialer dialer = new(AAnswer) { OtherPortReply = EmptyWebResponse };
        ManagedCurlEngine engine = DialingEngine(dialer);
        IEasyTransfer easy = engine.CreateEasy();
        await PerformThroughDohAsync(easy, "http://example.test:48712/", new EasyNetworkOptions { DohUrl = PlainDohUrl });

        await engine.DisposeAsync();

        Assert.HasCount(2, dialer.DohConnections);
        Assert.IsTrue(dialer.DohConnections.All(connection => connection.IsDisposed));
    }

    private static IShareHandle ShareOfConnections(ManagedCurlEngine engine)
    {
        IShareHandle share = engine.CreateShare();
        Assert.AreEqual(CurlShareCode.Ok, share.Share(CurlLockData.Connect));
        return share;
    }

    private static IEasyTransfer SharingEasy(ManagedCurlEngine engine, IShareHandle share)
    {
        IEasyTransfer easy = engine.CreateEasy();
        easy.Options = easy.Options with { Behaviour = easy.Options.Behaviour with { Share = share } };
        return easy;
    }

    // Adds the handle to the multi handle and performs until its one transfer is done, which must have succeeded.
    private async Task PerformInMultiThroughDohAsync(IMultiTransfer multi, IEasyTransfer easy, string url)
    {
        easy.Options = easy.Options with { Network = new EasyNetworkOptions { DohUrl = PlainDohUrl, Url = url } };
        Assert.AreEqual(CurlMultiCode.Ok, multi.Add(easy));
        CurlMultiMessage? message;
        do
        {
            await multi.PerformAsync(TestContext.CancellationToken);
            await Task.Yield();
        }
        while (!multi.TryReadInfo(out message, out _));

        Diagnostics.Act("multi perform", $"{url} -> {message.Result} {easy.ErrorText}");
        Assert.AreEqual(CurlCode.Ok, message.Result);
    }

    [TestMethod]
    public async Task ResolverFor_ADohUrlThatMakesNoDohResolver_LeavesTheKeptResolverOpen()
    {
        await using KeptDohResolver keeper = new();
        EasyNetworkOptions network = new() { DohUrl = PlainDohUrl };
        IDnsResolver kept = keeper.ResolverFor(network, new EasyTlsOptions(), () => EngineComposition.CreateDohResolver(PlainDohUrl, new ScriptedConnector([]), AddressFamily.Unspecified, readEnvironmentVariable: _ => null));
        UnusableDohUrlResolver unusable = new();

        IDnsResolver other = keeper.ResolverFor(network with { DohUrl = "bogus" }, new EasyTlsOptions(), () => unusable);
        IDnsResolver again = keeper.ResolverFor(network with { Url = "http://other.test/", Port = 8080 }, new EasyTlsOptions(), () => unusable);

        Diagnostics.Assert("kept resolver given again", kept.GetType().Name, again.GetType().Name);
        Assert.AreSame(unusable, other);
        Assert.AreSame(kept, again);
    }

    private static readonly byte[] EmptyWebResponse = Encoding.Latin1.GetBytes("HTTP/1.1 200 OK\r\nContent-Length: 0\r\n\r\n");

    private static ManagedCurlEngine DialingEngine(ITcpDialer dialer) =>
        new(TimeProvider.System, null, readEnvironmentVariable: _ => null, dialing: new EngineDialing(dialer, new LoopbackDnsResolver(), new RecordingDatagramConnector(CurlExitCode.CouldntConnect, "no udp")));

    // One perform of url on the handle, its other network options as given, which must succeed.
    private async Task PerformThroughDohAsync(IEasyTransfer easy, string url, EasyNetworkOptions network)
    {
        easy.Options = easy.Options with { Network = network with { Url = url } };

        CurlCode code = await easy.PerformAsync(CancellationToken.None);

        Diagnostics.Act("perform", $"{url} -> {code} {easy.ErrorText}");
        Assert.AreEqual(CurlCode.Ok, code);
    }

    // The transports a run dialing in memory builds for --doh-url, or for no DoH URL when it is empty.
    private CurlTransports DialingTransports(string dohUrl, ITcpDialer dialer, IDnsResolver resolver)
    {
        CommandLineOptions options = Parse("--doh-url", dohUrl);
        return EngineComposition.CreateTransports(EasyOptionsFromCommandLine.ToolOptionsOf(options), EasyOptionsFromCommandLine.NetworkOf(options), EasyOptionsFromCommandLine.TlsOf(options), EasyOptionsFromCommandLine.HttpOf(options), EasyOptionsFromCommandLine.ProxyOf(options), EasyOptionsFromCommandLine.AuthenticationOf(options), TimeProvider.System, runConnections: null, tlsSessions: null, diagnosticLog: null, runDnsCache: null, easyErrorText: null, traces: null, static lookUps => lookUps, new EngineDialing(dialer, resolver, new RecordingDatagramConnector(CurlExitCode.CouldntConnect, "no udp")), socketWait: null);
    }

    // Answers each request on a dialled connection with one reply: the DoH answer on the DoH server's port, else OtherPortReply.
    private sealed class DohRecordingDialer(byte[] dohReply) : ITcpDialer
    {
        public byte[] OtherPortReply { get; init; } = [];

        public List<DisposalRecordingConnection> Dialled { get; } = [];

        public List<DisposalRecordingConnection> DohConnections => [.. Dialled.Where(connection => connection.Port == 48711)];

        public ValueTask<DialedTcpConnection> DialAsync(IPEndPoint endPoint, CancellationToken cancellationToken)
        {
            DisposalRecordingConnection connection = new(endPoint.Port, endPoint.Port == 48711 ? dohReply : OtherPortReply);
            lock (Dialled)
            {
                Dialled.Add(connection);
            }

            return ValueTask.FromResult(new DialedTcpConnection(connection, new IPEndPoint(IPAddress.Loopback, 50000)));
        }

        public ValueTask<DialedTcpConnection> DialFromAsync(IPEndPoint endPoint, IPEndPoint localEndPoint, int localPortCount, CancellationToken cancellationToken) =>
            DialAsync(endPoint, cancellationToken);

        public ValueTask<IConnection> DialUnixSocketAsync(UnixSocketAddress address, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("No Unix socket is dialled.");
    }

    private sealed class DisposalRecordingConnection(int port, byte[] reply) : IConnection
    {
        private ReadOnlyMemory<byte> unread = reply;

        public int Port => port;

        public bool IsDisposed { get; private set; }

        public int Requests { get; private set; }

        public bool IsSecure => false;

        public EndPoint? RemoteEndPoint => null;

        public ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken)
        {
            int count = Math.Min(unread.Length, buffer.Length);
            unread[..count].CopyTo(buffer);
            unread = unread[count..];
            return ValueTask.FromResult(count);
        }

        // A request written once the last reply has been read gets the reply again.
        public ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken)
        {
            Requests++;
            if (unread.IsEmpty)
            {
                unread = reply;
            }

            return ValueTask.CompletedTask;
        }

        public ValueTask FlushAsync(CancellationToken cancellationToken) => ValueTask.CompletedTask;

        public ValueTask DisposeAsync()
        {
            IsDisposed = true;
            return ValueTask.CompletedTask;
        }
    }

    private sealed class DisposalRecordingResolver : IDnsResolver, IAsyncDisposable
    {
        public bool IsDisposed { get; private set; }

        public ValueTask<IReadOnlyList<IPAddress>> ResolveAsync(string host, CancellationToken cancellationToken) =>
            ValueTask.FromResult<IReadOnlyList<IPAddress>>([IPAddress.Loopback]);

        public ValueTask DisposeAsync()
        {
            IsDisposed = true;
            return ValueTask.CompletedTask;
        }
    }

    // The TLS record the command line maps to, as Curl.Console hands it to the engine.
    private EasyTlsOptions TlsOf(params string[] arguments) => EasyOptionsFromCommandLine.TlsOf(Parse(arguments));

    private CommandLineOptions Parse(params string[] arguments)
    {
        Diagnostics.Arrange("arguments", string.Join(' ', arguments.Append("http://example.test:48712/")));
        // As the OpenSSL build with c-ares reads it, so --dns-servers is accepted beside --doh-url (ADR-0841).
        CommandLineParseResult parsed = CommandLineParser.Parse([.. arguments, "http://example.test:48712/"], _ => true, new EmptyPasswordPrompt(), DiskDataFileReader.ForProcess, new DefaultConfigFileSearch(_ => null, isWindows: false, null, null), isWindows: false, _ => null, actsAsQuicBuild: false, actsAsEchBuild: false, actsAsCAresBuild: true);
        Assert.IsTrue(parsed.IsAccepted);
        return parsed.Options;
    }

    /// <summary>Records the info lines a connect reports; every other event is dropped.</summary>
    private sealed class RecordingEvents : ITransferEvents
    {
        public List<string> Info { get; } = [];

        public void ReportInfo(string text) => Info.Add(text);

        public void ReportConnectionOpened(ConnectionOpenedEvent opened)
        {
        }

        public void ReportConnectionReused(ConnectionReusedEvent reused)
        {
        }

        public void ReportTlsHandshake(TlsHandshakeEvent handshake)
        {
        }

        public void ReportTlsData(ReadOnlySpan<byte> bytes, bool sent)
        {
        }

        public void ReportRequestHeader(ReadOnlySpan<byte> bytes)
        {
        }

        public void ReportResponseHeader(ReadOnlySpan<byte> bytes)
        {
        }

        public void ReportDataSent(ReadOnlySpan<byte> bytes)
        {
        }

        public void ReportDataReceived(ReadOnlySpan<byte> bytes)
        {
        }
    }
}
