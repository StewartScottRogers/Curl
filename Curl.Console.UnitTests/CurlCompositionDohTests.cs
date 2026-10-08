using System.Net.Sockets;
using System.Text;

using Curl.Cli;
using Curl.Networking;
using Curl.Protocol.Abstractions;
using Curl.Testing;

namespace Curl.Console;

/// <summary>
/// Pins how the composition resolves through <c>--doh-url</c> (BL-642, ADR-0152), each case as curl
/// 8.21.0 (Schannel) was measured with <c>Record-CurlExchange.ps1 -Tls</c> standing in for the DoH
/// server on 2026-09-29 (BL-642 Notes): the DoH server's A answer <c>127.0.0.1</c> is dialled, a
/// <c>--resolve</c> entry for the host wins without asking the DoH server, and <c>--doh-url bogus</c>
/// and an <c>ftp://</c> DoH URL fail the transfer with exit 6. The DoH server and the transfer's
/// server are both <see cref="ScriptedConnector" />s, so no socket is opened.
/// </summary>
[TestClass]
public sealed class CurlCompositionDohTests
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
        CurlTransports transports = CurlComposition.CreateTransports(Parse("--doh-url", DohUrl));
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
        ScriptedConnector dohServer = new([AAnswer, AAnswer]);
        ScriptedConnector webServer = new([]);
        RecordingEvents events = new();

        ConnectResult result = await ConnectAsync(DohUrl, dohServer, webServer, events, "--doh-insecure");

        Diagnostics.Assert("result.Connection is not null", true, result.Connection is not null);
        Assert.IsNotNull(result.Connection);
        Diagnostics.Assert("dohServer.Targets count", 2, dohServer.Targets.Count);
        Assert.HasCount(2, dohServer.Targets);
        Assert.AreEqual(new ConnectTarget("127.0.0.1", 48711, true), dohServer.Targets[0] with { PoolScheme = null });
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
        // the filter's lines, the DoH lines once both queries are done, then the resolve and Trying
        // (measured, BL-1102 Notes; the DoH lines' texts as BL-850 measured them), each DoH sub-transfer's
        // [DNS] lines before them (BL-1180; the scripted DoH server reports no connect lines); network's [HAPPY-EYEBALLS], [TCP] and [TIMER] lines (BL-1161, BL-1186) aside.
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
                "[DNS] using HTTP/1.x",
                "[DNS] upload completely sent off: 30 bytes",
                "[DNS] Connection #1 to host 127.0.0.1:48711 left intact",
                "[DNS] a DoH request is completed, 1 to go",
                "[DNS] using HTTP/1.x",
                "[DNS] upload completely sent off: 30 bytes",
                "[DNS] Connection #2 to host 127.0.0.1:48711 left intact",
                "[DNS] a DoH request is completed, 0 to go",
                "[DNS] DoH: Unexpected TYPE type AAAA for example.test",
                "[DNS] hostname: example.test",
                "[DoH] TTL: 60 seconds",
                "[DoH] A: 127.0.0.1",
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

        Diagnostics.Assert("[DNS] line present", false, events.Info.Any(line => line.StartsWith("[DNS]", StringComparison.Ordinal)));
        Assert.IsFalse(events.Info.Any(line => line.StartsWith("[DNS]", StringComparison.Ordinal)));
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
        // -> "Added ... to DNS cache", "Hostname example.test was found in DNS cache", no byte to the DoH server.
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
    public async Task Connect_WithBogusDohUrl_AsksHostBogusOverPlainHttpAndFailsWithExit6()
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
        Assert.AreEqual(new ConnectTarget("bogus", 80, false), dohServer.Targets[0] with { PoolScheme = null });
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
        IDnsResolver resolver = CurlComposition.CreateDohResolver("http://[bad/", new ScriptedConnector([]), AddressFamily.Unspecified);
        Diagnostics.Act("resolver type", resolver.GetType().Name);

        Diagnostics.Assert("resolver type", nameof(UnusableDohUrlResolver), resolver.GetType().Name);
        Assert.IsInstanceOfType<UnusableDohUrlResolver>(resolver);
    }

    [TestMethod]
    public async Task UnusableDohUrlResolver_ResolvesEveryNameToNoAddress()
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
    [DataRow("127.0.0.1:48711/dns-query", "http://127.0.0.1:48711/dns-query")]
    [DataRow("bogus", "http://bogus/")]
    public void DohUrlOf_MakesTheUrlCurlAsks(string value, string expected)
    {
        Diagnostics.Arrange("value", value);
        Uri? url = CurlComposition.DohUrlOf(value);
        Diagnostics.Act("DoH URL", url);

        Diagnostics.Assert("DoH URL", new Uri(expected), url);
        Assert.AreEqual(new Uri(expected), CurlComposition.DohUrlOf(value));
    }

    [TestMethod]
    [DataRow("ftp://127.0.0.1:48711/")]
    [DataRow("http://[bad/")]
    [DataRow("://")]
    public void DohUrlOf_ANonHttpOrUnparsableValue_IsNone(string value)
    {
        Diagnostics.Arrange("value", value);
        Uri? url = CurlComposition.DohUrlOf(value);
        Diagnostics.Act("DoH URL", url?.ToString() ?? "(none)");

        Diagnostics.Assert("DoH URL is none", true, url is null);
        Assert.IsNull(CurlComposition.DohUrlOf(value));
    }

    [TestMethod]
    public void DohFromCommandLine_TakesTheDohFlagsAndNotTheTransfersInsecureOrCertStatus()
    {
        TlsClientOptions dohOnly = TlsClientOptionsMapping.DohFromCommandLine(Parse("--doh-insecure", "--doh-cert-status"));
        TlsClientOptions transferOnly = TlsClientOptionsMapping.DohFromCommandLine(Parse("-k", "--cert-status"));
        Diagnostics.Act("DoH-only options", dohOnly);
        Diagnostics.Act("transfer-only options", transferOnly);

        Diagnostics.Assert("DoH-only insecure", true, dohOnly.Insecure);
        Assert.IsTrue(dohOnly.Insecure);
        Diagnostics.Assert("DoH-only requires certificate status", true, dohOnly.RequireCertificateStatus);
        Assert.IsTrue(dohOnly.RequireCertificateStatus);
        Diagnostics.Assert("transferOnly", new TlsClientOptions(), transferOnly);
        Assert.AreEqual(new TlsClientOptions(), transferOnly);
    }

    [TestMethod]
    public void DohFromCommandLine_CopiesTheTransfersTrustOptionsAsCurlsDohCopiesThem()
    {
        // curl --ssl-no-revoke --cacert root.pem --doh-url ... (no --doh-insecure) verified the DoH server's
        // certificate against root.pem and resolved; -k alone did not reach it (measured).
        TlsClientOptions options = TlsClientOptionsMapping.DohFromCommandLine(Parse(
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
        TcpConnector connector = CurlComposition.CreateDohConnector(
            Parse("--http2", "--resolve", "x:1:192.0.2.1", "--doh-url", DohUrl), new TcpDialer(), TimeProvider.System);
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
        IDnsResolver resolver = CurlComposition.CreateDnsResolver(Parse(arguments), TimeProvider.System, new TcpDialer());
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
        FlowScopedTransferEvents? resolverEvents = CurlComposition.TracesDns(options) ? new() : null;
        TcpConnector connector = CurlComposition.CreateTcpConnector(
            options,
            CurlComposition.CreateDohResolver(dohUrl, dohServer, CurlComposition.AddressFamilyOf(options), resolverEvents),
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

    private CommandLineOptions Parse(params string[] arguments)
    {
        Diagnostics.Arrange("arguments", string.Join(' ', arguments.Append("http://example.test:48712/")));
        CommandLineParseResult parsed = OpenSslBuildParser.Parse([.. arguments, "http://example.test:48712/"], _ => true);
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
