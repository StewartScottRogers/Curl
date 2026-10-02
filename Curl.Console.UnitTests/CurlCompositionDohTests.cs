using System.Net.Sockets;
using System.Text;

using Curl.Cli;
using Curl.Networking;
using Curl.Protocol.Abstractions;

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
        Assert.IsInstanceOfType<DohDnsResolver>(CreateDnsResolver("--doh-url", DohUrl));
    }

    [TestMethod]
    public void CreateDnsResolver_WithDohUrlAndDnsServers_AsksTheDohServer()
    {
        Assert.IsInstanceOfType<DohDnsResolver>(CreateDnsResolver("--dns-servers", "192.0.2.1", "--doh-url", DohUrl));
    }

    [TestMethod]
    public void CreateDnsResolver_WithAnEmptyDohUrlLast_IsTheSystemResolver()
    {
        Assert.IsInstanceOfType<SystemDnsResolver>(CreateDnsResolver("--doh-url", DohUrl, "--doh-url", ""));
    }

    [TestMethod]
    public void CreateTransports_WithDohUrl_SharesTheDohResolverBetweenBothConnectors()
    {
        CurlTransports transports = CurlComposition.CreateTransports(Parse("--doh-url", DohUrl));

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

        Assert.IsNotNull(result.Connection);
        Assert.HasCount(2, dohServer.Targets);
        Assert.AreEqual(new ConnectTarget("127.0.0.1", 48711, true), dohServer.Targets[0] with { PoolScheme = null });
        Assert.AreEqual("https", dohServer.Targets[0].PoolScheme);
        Assert.AreEqual(MeasuredPost(0x01) + MeasuredPost(0x1C), Encoding.Latin1.GetString(dohServer.Written));
        Assert.HasCount(1, webServer.Targets);
        Assert.AreEqual("127.0.0.1", webServer.Targets[0].Host);
        Assert.AreEqual(48712, webServer.Targets[0].Port);
        CollectionAssert.AreEqual(
            new[] { "Host example.test:48712 was resolved.", "IPv6: (none)", "IPv4: 127.0.0.1", "  Trying 127.0.0.1:48712..." },
            events.Info.ToArray());
    }

    [TestMethod]
    public async Task Connect_WithIpv4Only_PostsOnlyTheAQuery()
    {
        // curl -sS -4 --doh-url https://127.0.0.1:P1/dns-query --doh-insecure http://example.test:P2/
        // -> one POST to the DoH server, QTYPE A (measured, BL-642).
        ScriptedConnector dohServer = new([AAnswer]);
        ScriptedConnector webServer = new([]);

        ConnectResult result = await ConnectAsync(DohUrl, dohServer, webServer, new RecordingEvents(), "--doh-insecure", "-4");

        Assert.IsNotNull(result.Connection);
        Assert.HasCount(1, dohServer.Targets);
        Assert.AreEqual(MeasuredPost(0x01), Encoding.Latin1.GetString(dohServer.Written));
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

        Assert.IsNotNull(result.Connection);
        Assert.IsEmpty(dohServer.Targets);
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

        Assert.AreEqual(CurlExitCode.CouldntResolveHost, result.ExitCode);
        Assert.AreEqual("Could not resolve host: example.test", result.ErrorMessage);
        Assert.AreEqual(new ConnectTarget("bogus", 80, false), dohServer.Targets[0] with { PoolScheme = null });
        Assert.IsEmpty(webServer.Targets);
    }

    [TestMethod]
    public async Task Connect_WithFtpDohUrl_FailsWithExit6WithoutAskingAnyone()
    {
        // curl -sS -v --doh-url ftp://127.0.0.1:48711/ http://example.test:48712/ -> exit 6, nothing sent.
        ScriptedConnector dohServer = new([AAnswer, AAnswer]);
        ScriptedConnector webServer = new([]);

        ConnectResult result = await ConnectAsync("ftp://127.0.0.1:48711/", dohServer, webServer, new RecordingEvents());

        Assert.AreEqual(CurlExitCode.CouldntResolveHost, result.ExitCode);
        Assert.IsEmpty(dohServer.Targets);
        Assert.IsEmpty(webServer.Targets);
    }

    [TestMethod]
    public void CreateDohResolver_WithAUrlThatDoesNotParse_ResolvesNothing()
    {
        Assert.IsInstanceOfType<UnusableDohUrlResolver>(CurlComposition.CreateDohResolver("http://[bad/", new ScriptedConnector([]), AddressFamily.Unspecified));
    }

    [TestMethod]
    public async Task UnusableDohUrlResolver_ResolvesEveryNameToNoAddress()
    {
        Assert.IsEmpty(await new UnusableDohUrlResolver().ResolveAsync("example.test", CancellationToken.None));
    }

    [TestMethod]
    [DataRow("https://127.0.0.1:48711/dns-query", "https://127.0.0.1:48711/dns-query")]
    [DataRow("HTTPS://dns.example/q", "https://dns.example/q")]
    [DataRow("http://dns.example/", "http://dns.example/")]
    [DataRow("127.0.0.1:48711/dns-query", "http://127.0.0.1:48711/dns-query")]
    [DataRow("bogus", "http://bogus/")]
    public void DohUrlOf_MakesTheUrlCurlAsks(string value, string expected)
    {
        Assert.AreEqual(new Uri(expected), CurlComposition.DohUrlOf(value));
    }

    [TestMethod]
    [DataRow("ftp://127.0.0.1:48711/")]
    [DataRow("http://[bad/")]
    [DataRow("://")]
    public void DohUrlOf_ANonHttpOrUnparsableValue_IsNone(string value)
    {
        Assert.IsNull(CurlComposition.DohUrlOf(value));
    }

    [TestMethod]
    public void DohFromCommandLine_TakesTheDohFlagsAndNotTheTransfersInsecureOrCertStatus()
    {
        TlsClientOptions dohOnly = TlsClientOptionsMapping.DohFromCommandLine(Parse("--doh-insecure", "--doh-cert-status"));
        TlsClientOptions transferOnly = TlsClientOptionsMapping.DohFromCommandLine(Parse("-k", "--cert-status"));

        Assert.IsTrue(dohOnly.Insecure);
        Assert.IsTrue(dohOnly.RequireCertificateStatus);
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

    private static IDnsResolver CreateDnsResolver(params string[] arguments) =>
        CurlComposition.CreateDnsResolver(Parse(arguments), TimeProvider.System, new TcpDialer());

    /// <summary>
    /// Builds the DoH resolver the composition makes of <paramref name="dohUrl" /> over
    /// <paramref name="dohServer" />, and the production TCP connector for the parsed
    /// <paramref name="arguments" /> over that resolver and <paramref name="webServer" />, then connects to
    /// <c>example.test:48712</c> as the measured transfer does.
    /// </summary>
    private static async Task<ConnectResult> ConnectAsync(
        string dohUrl,
        ScriptedConnector dohServer,
        ScriptedConnector webServer,
        RecordingEvents events,
        params string[] arguments)
    {
        CommandLineOptions options = Parse([.. arguments, "--doh-url", dohUrl]);
        TcpConnector connector = CurlComposition.CreateTcpConnector(
            options,
            CurlComposition.CreateDohResolver(dohUrl, dohServer, CurlComposition.AddressFamilyOf(options)),
            new ScriptedTcpDialer(webServer),
            new PassThroughTlsProvider(),
            TimeProvider.System,
            HttpProxyTunnelOptions.Default);

        return await connector.ConnectAsync(new ConnectTarget("example.test", 48712, false) { Events = events }, CancellationToken.None);
    }

    private static CommandLineOptions Parse(params string[] arguments)
    {
        CommandLineParseResult parsed = CommandLineParser.Parse([.. arguments, "http://example.test:48712/"], _ => true);
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
