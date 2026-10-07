using System.Reflection;
using System.Text;

using Curl.Cli;
using Curl.Core;
using Curl.Networking;
using Curl.Output;
using Curl.Protocol.Abstractions;
using Curl.Protocol.Dict;
using Curl.Protocol.File;
using Curl.Protocol.Ftp;
using Curl.Protocol.Gopher;
using Curl.Protocol.Http;
using Curl.Protocol.Imap;
using Curl.Protocol.Ldap;
using Curl.Protocol.Mqtt;
using Curl.Protocol.Pop3;
using Curl.Protocol.Rtsp;
using Curl.Protocol.Smb;
using Curl.Protocol.Smtp;
using Curl.Protocol.Ssh;
using Curl.Protocol.Telnet;
using Curl.Protocol.Tftp;
using Curl.Protocol.Ws;
using Curl.Testing;

namespace Curl.Console;

/// <summary>
/// Pins the composition root: which handlers the executable registers, how the network
/// transports are wired, and that the production composition performs a <c>file://</c>
/// transfer end to end. No test here opens a socket.
/// </summary>
[TestClass]
public sealed partial class CurlCompositionTests
{
    private const string ConnectFailure = "Failed to connect to h:2628 after 0 ms: Could not connect to server";

    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    [DataRow(true, WriteOutTimeDialect.WindowsCRuntime)]
    [DataRow(false, WriteOutTimeDialect.Glibc)]
    public void WriteOutTimeDialectFor_Platform_IsThatPlatformsCRuntime(bool runsOnWindows, WriteOutTimeDialect expected)
    {
        Diagnostics.Arrange("runs on windows", runsOnWindows);

        WriteOutTimeDialect actual = CurlComposition.WriteOutTimeDialectFor(runsOnWindows);

        Diagnostics.Act("write-out time dialect", actual);
        Diagnostics.Assert("write-out time dialect", expected, actual);
        Assert.AreEqual(expected, actual);
    }

    [TestMethod]
    public void CreateProtocolHandlers_ServesEachSchemeThroughItsHandlerOnce()
    {
        Diagnostics.Arrange("connect failure scripted into both connectors", ConnectFailure);
        IReadOnlyList<IProtocolHandler> handlers = CurlComposition.CreateProtocolHandlers(
            new RecordingConnector(CurlExitCode.CouldntConnect, ConnectFailure),
            new RecordingDatagramConnector(CurlExitCode.CouldntConnect, ConnectFailure),
            new PassThroughTlsProvider(),
            new LoopbackDnsResolver());

        Dictionary<string, Type> served = handlers
            .SelectMany(handler => handler.SupportedSchemes.Select(scheme => (scheme, type: Unwrapped(handler).GetType())))
            .ToDictionary(pair => pair.scheme, pair => pair.type);
        Dictionary<string, Type> expected = new()
        {
            ["file"] = typeof(FileProtocolHandler),
            ["dict"] = typeof(DictProtocolHandler),
            ["gopher"] = typeof(GopherProtocolHandler),
            ["gophers"] = typeof(GopherProtocolHandler),
            ["telnet"] = typeof(TelnetProtocolHandler),
            ["tftp"] = typeof(TftpProtocolHandler),
            ["mqtt"] = typeof(MqttProtocolHandler),
            ["mqtts"] = typeof(MqttProtocolHandler),
            ["imap"] = typeof(ImapProtocolHandler),
            ["imaps"] = typeof(ImapProtocolHandler),
            ["ldap"] = typeof(LdapProtocolHandler),
            ["ldaps"] = typeof(LdapProtocolHandler),
            ["pop3"] = typeof(Pop3ProtocolHandler),
            ["pop3s"] = typeof(Pop3ProtocolHandler),
            ["rtsp"] = typeof(RtspProtocolHandler),
            ["smb"] = typeof(SmbProtocolHandler),
            ["smbs"] = typeof(SmbProtocolHandler),
            ["scp"] = typeof(SshProtocolHandler),
            ["sftp"] = typeof(SshProtocolHandler),
            ["smtp"] = typeof(SmtpProtocolHandler),
            ["smtps"] = typeof(SmtpProtocolHandler),
            ["ws"] = typeof(WsProtocolHandler),
            ["wss"] = typeof(WsProtocolHandler),
            ["http"] = typeof(HttpProtocolHandler),
            ["https"] = typeof(HttpProtocolHandler),
            ["ftp"] = typeof(RoutingFtpProtocolHandler),
            ["ftps"] = typeof(RoutingFtpProtocolHandler),
        };
        Diagnostics.Act("scheme to handler type", string.Join(", ", served.OrderBy(pair => pair.Key, StringComparer.Ordinal).Select(pair => $"{pair.Key}={pair.Value.Name}")));
        Diagnostics.Assert("scheme to handler type count", expected.Count, served.Count);
        CollectionAssert.AreEquivalent(expected.ToList(), served.ToList());
        _ = new ProtocolDispatcher(handlers);
    }

    [TestMethod]
    [DataRow("gophers://h/", 70, true, null)]
    [DataRow("mqtts://h/", 8883, true, null)]
    [DataRow("imaps://h/", 993, true, null)]
    [DataRow("imap://h/", 143, false, null)]
    [DataRow("ldap://h/", 389, false, null)]
    [DataRow("ldaps://h/", 636, true, null)]
    [DataRow("pop3s://h/", 995, true, null)]
    [DataRow("pop3://h/", 110, false, null)]
    [DataRow("smtps://h/", 465, true, null)]
    [DataRow("smtp://h/", 25, false, null)]
    [DataRow("ws://h/", 80, false, null)]
    [DataRow("wss://h/", 443, true, null)]
    [DataRow("gopher://h/", 70, false, null)]
    [DataRow("mqtt://h/", 1883, false, null)]
    [DataRow("dict://h/d:x", 2628, false, null)]
    [DataRow("telnet://h/", 23, false, null)]
    [DataRow("http://h/", 80, false, "http")]
    [DataRow("https://h/", 443, true, "https")]
    public async Task CreateRunner_TcpSchemeUrl_ReachesConnectorAtDefaultPortWithSchemesTls(
        string url,
        int port,
        bool useTls,
        string? poolScheme)
    {
        Diagnostics.Arrange("url", url);
        RecordingConnector connector = new(CurlExitCode.CouldntConnect, ConnectFailure);

        await RunWithFakeConnectorsAsync(url, connector, new RecordingDatagramConnector(CurlExitCode.CouldntConnect, ConnectFailure));

        ConnectTarget expected = new("h", port, useTls) { PoolScheme = poolScheme };
        ConnectTarget actual = connector.Targets.Single() with { Events = NoTransferEvents.Instance };
        Diagnostics.Act("connector target", actual);
        Diagnostics.Assert("connector target", expected, actual);
        Assert.AreEqual(expected, actual);
    }

    [TestMethod]
    public async Task CreateRunner_ConnectorFailsToConnect_PrintsExit7LineAndReturns7()
    {
        Diagnostics.Arrange("url", "dict://h/d:x");
        Diagnostics.Arrange("scripted connect failure", ConnectFailure);
        RecordingConnector connector = new(CurlExitCode.CouldntConnect, ConnectFailure);

        (int exitCode, string standardErrorText) = await RunWithFakeConnectorsAsync(
            "dict://h/d:x",
            connector,
            new RecordingDatagramConnector(CurlExitCode.CouldntConnect, ConnectFailure));

        Diagnostics.Act("exit code", exitCode);
        Diagnostics.Act("standard error", standardErrorText.ReplaceLineEndings("\\n"));
        Diagnostics.Assert("exit code", 7, exitCode);
        Assert.AreEqual(7, exitCode);
        Assert.AreEqual($"curl: (7) {ConnectFailure}{Environment.NewLine}", standardErrorText);
    }

    [TestMethod]
    public async Task CreateRunner_TftpUrl_ReachesDatagramConnectorAtHostAndPort69()
    {
        Diagnostics.Arrange("url", "tftp://h/f");
        RecordingDatagramConnector datagramConnector = new(CurlExitCode.CouldntConnect, ConnectFailure);

        await RunWithFakeConnectorsAsync(
            "tftp://h/f",
            new RecordingConnector(CurlExitCode.CouldntConnect, ConnectFailure),
            datagramConnector);

        var actual = datagramConnector.Opens.Single();
        Diagnostics.Act("datagram open", actual);
        Diagnostics.Assert("datagram open", ("h", 69), actual);
        Assert.AreEqual(("h", 69), actual);
    }

    [TestMethod]
    public async Task CreateRunner_FileUrlOfTemporaryFile_WritesItsBytesToStandardOutput()
    {
        string path = Path.Combine(Path.GetTempPath(), $"curl-bl068-{Guid.NewGuid():N}.bin");
        byte[] content = [0, 1, 2, 13, 10, 255, (byte)'x'];
        await System.IO.File.WriteAllBytesAsync(path, content);
        Diagnostics.Arrange("file url of a temporary file named", "curl-bl068-*.bin");
        Diagnostics.Bytes("file content", content);

        try
        {
            using MemoryStream standardOutput = new();
            using MemoryStream standardError = new();
            using MemoryStream standardInput = new();

            int exitCode = await CurlComposition
                .CreateRunner(standardOutput, standardError, standardInput, standardOutputIsTerminal: true)
                .RunAsync([new Uri(path).AbsoluteUri]);

            Diagnostics.Act("exit code", exitCode);
            Diagnostics.Bytes("standard output", standardOutput.ToArray());
            Diagnostics.Act("standard error length", standardError.Length);
            Diagnostics.Assert("exit code", 0, exitCode);
            Assert.AreEqual(0, exitCode);
            CollectionAssert.AreEqual(content, standardOutput.ToArray());
            Assert.AreEqual(0, standardError.Length);
        }
        finally
        {
            System.IO.File.Delete(path);
        }
    }

    // IPFS_GATEWAY is set in this process only, to a gateway curl refuses (it has a query), so
    // the run reads the variable and stops with exit 3 before any connection.
    [TestMethod]
    public async Task CreateRunner_IpfsUrl_ReadsTheGatewayFromTheProcessEnvironment()
    {
        string? saved = Environment.GetEnvironmentVariable(IpfsGatewayRewriter.GatewayVariableName);
        Environment.SetEnvironmentVariable(IpfsGatewayRewriter.GatewayVariableName, "http://h/?q");
        Diagnostics.Arrange("gateway variable", "http://h/?q");
        Diagnostics.Arrange("arguments", "-q ipfs://bafyabc");

        try
        {
            using MemoryStream standardOutput = new();
            using MemoryStream standardError = new();
            using MemoryStream standardInput = new();

            int exitCode = await CurlComposition
                .CreateRunner(standardOutput, standardError, standardInput, standardOutputIsTerminal: false)
                .RunAsync(["-q", "ipfs://bafyabc"]);

            string standardErrorText = Encoding.UTF8.GetString(standardError.ToArray());
            Diagnostics.Act("exit code", exitCode);
            Diagnostics.Act("standard error", standardErrorText.ReplaceLineEndings("\\n"));
            Diagnostics.Assert("exit code", 3, exitCode);
            Assert.AreEqual(3, exitCode);
            Assert.StartsWith("curl: malformed target URL", Encoding.UTF8.GetString(standardError.ToArray()));
        }
        finally
        {
            Environment.SetEnvironmentVariable(IpfsGatewayRewriter.GatewayVariableName, saved);
        }
    }

    [TestMethod]
    [TestCategory("Integration")]
    public async Task CreateRunner_WriteOutOutputFile_OpensItOnDisk()
    {
        string directory = Path.Combine(Path.GetTempPath(), $"curl-bl280-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        string source = Path.Combine(directory, "source.bin");
        string target = Path.Combine(directory, "w.txt");
        await System.IO.File.WriteAllBytesAsync(source, [1]);
        Diagnostics.Arrange("arguments", "<source.bin url> -w %output{<directory>/w.txt}F\\n");

        try
        {
            using MemoryStream standardOutput = new();
            using MemoryStream standardError = new();
            using MemoryStream standardInput = new();

            int exitCode = await CurlComposition
                .CreateRunner(standardOutput, standardError, standardInput, standardOutputIsTerminal: true)
                .RunAsync([new Uri(source).AbsoluteUri, "-w", $"%output{{{target}}}F\\n"]);

            Diagnostics.Act("exit code", exitCode);
            Diagnostics.Assert("exit code", 0, exitCode);
            Assert.AreEqual(0, exitCode);
            string expected = OperatingSystem.IsWindows() ? "F\r\n" : "F\n";
            string written = await System.IO.File.ReadAllTextAsync(target);
            Diagnostics.Act("w.txt content length", written.Length);
            Diagnostics.Assert("w.txt content", expected.Replace("\r", "\\r").Replace("\n", "\\n"), written.Replace("\r", "\\r").Replace("\n", "\\n"));
            Assert.AreEqual(expected, written);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [TestMethod]
    public async Task CreateRunner_FileUrlToStandardOutputThatIsNotATerminal_WritesTheProgressMeter()
    {
        string path = Path.Combine(Path.GetTempPath(), $"curl-bl102-{Guid.NewGuid():N}.bin");
        await System.IO.File.WriteAllBytesAsync(path, [1, 2, 3]);
        Diagnostics.Arrange("file url of a temporary file named", "curl-bl102-*.bin");

        try
        {
            using MemoryStream standardOutput = new();
            using MemoryStream standardError = new();
            using MemoryStream standardInput = new();

            int exitCode = await CurlComposition
                .CreateRunner(standardOutput, standardError, standardInput, standardOutputIsTerminal: false)
                .RunAsync([new Uri(path).AbsoluteUri]);

            string expectedMeter = string.Concat(ProgressMeterLines.HeaderLines(null).Append(ProgressMeterLines.ZeroStatusLine).Select(line => line + Environment.NewLine));
            string actualMeter = Encoding.UTF8.GetString(standardError.ToArray());
            Diagnostics.Act("exit code", exitCode);
            Diagnostics.Act("standard error", actualMeter.ReplaceLineEndings("\\n"));
            Diagnostics.Assert("exit code", 0, exitCode);
            Diagnostics.Assert("standard error", expectedMeter.ReplaceLineEndings("\\n"), actualMeter.ReplaceLineEndings("\\n"));
            Assert.AreEqual(0, exitCode);
            Assert.AreEqual(
                string.Concat(ProgressMeterLines.HeaderLines(null).Append(ProgressMeterLines.ZeroStatusLine).Select(line => line + Environment.NewLine)),
                Encoding.UTF8.GetString(standardError.ToArray()));
        }
        finally
        {
            System.IO.File.Delete(path);
        }
    }

    [TestMethod]
    [TestCategory("Integration")]
    public async Task CreateRunnerOverConnectors_WriteOutFileOpenerGiven_OpensTheOutputFileOnDisk()
    {
        string directory = Path.Combine(Path.GetTempPath(), $"curl-bl1445-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        string source = Path.Combine(directory, "source.bin");
        string target = Path.Combine(directory, "w.txt");
        await System.IO.File.WriteAllBytesAsync(source, [1]);
        Diagnostics.Arrange("arguments", "<source.bin url> -w %output{<directory>/w.txt}F\\n");

        try
        {
            using MemoryStream standardOutput = new();
            using MemoryStream standardError = new();
            using MemoryStream standardInput = new();

            int exitCode = await CurlComposition
                .CreateRunner(
                    standardOutput,
                    standardError,
                    standardInput,
                    new RecordingConnector(CurlExitCode.CouldntConnect, ConnectFailure),
                    new RecordingDatagramConnector(CurlExitCode.CouldntConnect, ConnectFailure),
                    writeOutFileOpener: new DiskWriteOutFileOpener(writesLineFeedAsCrLf: false))
                .RunAsync([new Uri(source).AbsoluteUri, "-w", $"%output{{{target}}}F\\n"]);

            string written = await System.IO.File.ReadAllTextAsync(target);
            Diagnostics.Act("exit code", exitCode);
            Diagnostics.Act("w.txt content length", written.Length);
            Diagnostics.Assert("exit code", 0, exitCode);
            Diagnostics.Assert("w.txt content", "F\\n", written.Replace("\r", "\\r").Replace("\n", "\\n"));
            Assert.AreEqual(0, exitCode);
            Assert.AreEqual("F\n", written);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [TestMethod]
    [DataRow(true)]
    [DataRow(false)]
    public async Task CreateRunnerOverConnectors_WritesProgressMeter_WritesTheMeterOnlyWhenAsked(bool writesProgressMeter)
    {
        string path = Path.Combine(Path.GetTempPath(), $"curl-bl1445-{Guid.NewGuid():N}.bin");
        await System.IO.File.WriteAllBytesAsync(path, [1, 2, 3]);
        Diagnostics.Arrange("writes progress meter", writesProgressMeter);
        Diagnostics.Arrange("arguments", "<temporary file url> -o <same name>.out");

        try
        {
            using MemoryStream standardOutput = new();
            using MemoryStream standardError = new();
            using MemoryStream standardInput = new();

            int exitCode = await CurlComposition
                .CreateRunner(
                    standardOutput,
                    standardError,
                    standardInput,
                    new RecordingConnector(CurlExitCode.CouldntConnect, ConnectFailure),
                    new RecordingDatagramConnector(CurlExitCode.CouldntConnect, ConnectFailure),
                    writesProgressMeter: writesProgressMeter)
                .RunAsync([new Uri(path).AbsoluteUri, "-o", Path.ChangeExtension(path, ".out")]);

            Assert.AreEqual(0, exitCode);
            string expected = writesProgressMeter
                ? string.Concat(ProgressMeterLines.HeaderLines(null).Append(ProgressMeterLines.ZeroStatusLine).Select(line => line + Environment.NewLine))
                : string.Empty;
            string actual = Encoding.UTF8.GetString(standardError.ToArray());
            Diagnostics.Act("exit code", exitCode);
            Diagnostics.Act("standard error", actual.ReplaceLineEndings("\\n"));
            Diagnostics.Assert("exit code", 0, exitCode);
            Diagnostics.Assert("standard error", expected.ReplaceLineEndings("\\n"), actual.ReplaceLineEndings("\\n"));
            Assert.AreEqual(expected, Encoding.UTF8.GetString(standardError.ToArray()));
        }
        finally
        {
            System.IO.File.Delete(path);
            System.IO.File.Delete(Path.ChangeExtension(path, ".out"));
        }
    }

    [TestMethod]
    public void CreateTransports_BothConnectors_ShareOneSystemDnsResolverAndTimeProviderSystem()
    {
        Diagnostics.Arrange("arguments", "gophers://example.com/");
        CurlTransports transports = CurlComposition.CreateTransports(NoOptions());

        Diagnostics.Act("dns resolver type", transports.DnsResolver.GetType().Name);
        Diagnostics.Act("time provider is TimeProvider.System", ReferenceEquals(TimeProvider.System, transports.TimeProvider));
        Diagnostics.Assert("dns resolver type", nameof(SystemDnsResolver), transports.DnsResolver.GetType().Name);
        Assert.IsInstanceOfType<SystemDnsResolver>(transports.DnsResolver);
        Assert.AreSame(TimeProvider.System, transports.TimeProvider);
        Assert.AreSame(transports.DnsResolver, CapturedDependency<IDnsResolver>(transports.TcpConnector));
        Assert.AreSame(transports.DnsResolver, CapturedDependency<IDnsResolver>(transports.UdpDatagramConnector));
        Assert.AreSame(TimeProvider.System, CapturedDependency<TimeProvider>(transports.TcpConnector));
        Assert.AreSame(TimeProvider.System, CapturedDependency<TimeProvider>(transports.UdpDatagramConnector));
    }

    [TestMethod]
    public void CreateTransports_NoTimeProviderGiven_SslStreamTlsProviderTimesOnTimeProviderSystem()
    {
        Diagnostics.Arrange("arguments", "gophers://example.com/");
        CurlTransports transports = CurlComposition.CreateTransports(NoOptions());

        TimeProvider captured = CapturedDependency<TimeProvider>(transports.TlsProvider);
        Diagnostics.Act("tls provider time provider is TimeProvider.System", ReferenceEquals(TimeProvider.System, captured));
        Diagnostics.Assert("tls provider time provider is TimeProvider.System", true, ReferenceEquals(TimeProvider.System, captured));
        Assert.AreSame(TimeProvider.System, CapturedDependency<TimeProvider>(transports.TlsProvider));
    }

    [TestMethod]
    public void CreateTransports_GivenTimeProvider_SslStreamTlsProviderSharesTheTcpConnectorsTimeProvider()
    {
        TimeProvider timeProvider = new ReplacementTimeProvider();
        Diagnostics.Arrange("time provider", timeProvider.GetType().Name);

        CurlTransports transports = CurlComposition.CreateTransports(NoOptions(), timeProvider);

        Diagnostics.Act("transports time provider is the given one", ReferenceEquals(timeProvider, transports.TimeProvider));
        Diagnostics.Assert("transports time provider is the given one", true, ReferenceEquals(timeProvider, transports.TimeProvider));
        Assert.AreSame(timeProvider, transports.TimeProvider);
        Assert.AreSame(timeProvider, CapturedDependency<TimeProvider>(transports.TcpConnector));
        Assert.AreSame(timeProvider, CapturedDependency<TimeProvider>(transports.TlsProvider));
        Assert.AreSame(timeProvider, CapturedDependency<TimeProvider>(transports.UdpDatagramConnector));
    }

    // ADR-0140: SslStream for every option set it can honour, the hand-built client for a
    // --tls-max of TLS 1.0 or 1.1 (the legacy-versions row), origin and proxy alike.
    [TestMethod]
    [DataRow(TlsVersion.SystemDefault, typeof(SslStreamTlsProvider))]
    [DataRow(TlsVersion.Tls12, typeof(SslStreamTlsProvider))]
    [DataRow(TlsVersion.Tls13, typeof(SslStreamTlsProvider))]
    [DataRow(TlsVersion.Tls10, typeof(HandBuiltTlsProvider))]
    [DataRow(TlsVersion.Tls11, typeof(HandBuiltTlsProvider))]
    public void CreateTlsProvider_ByTheCeiling_IsTheProviderTheRoutingRuleChooses(TlsVersion maximumVersion, Type expected)
    {
        Diagnostics.Arrange("maximum version", maximumVersion);
        var options = new TlsClientOptions(MaximumVersion: maximumVersion);

        var provider = CurlComposition.CreateTlsProvider(options, TimeProvider.System);

        Diagnostics.Act("provider type", provider.GetType().Name);
        Diagnostics.Assert("provider type", expected.Name, provider.GetType().Name);
        Assert.IsInstanceOfType(provider, expected);
        Assert.AreSame(options, CapturedDependency<TlsClientOptions>(provider));
    }

    [TestMethod]
    public void CreateTransports_WithTlsMax10_UpgradesTheOriginWithTheHandBuiltClientAndTheProxyWithSslStream()
    {
        Diagnostics.Arrange("arguments", "--tls-max 1.0 https://example.com/");
        CurlTransports transports = CurlComposition.CreateTransports(Parse("--tls-max", "1.0", "https://example.com/"));

        Diagnostics.Act("origin tls provider type", transports.TlsProvider.GetType().Name);
        Diagnostics.Act("proxy tls provider type", transports.ProxyTlsProvider.GetType().Name);
        Diagnostics.Assert("origin tls provider type", nameof(HandBuiltTlsProvider), transports.TlsProvider.GetType().Name);
        Assert.IsInstanceOfType<HandBuiltTlsProvider>(transports.TlsProvider);
        Assert.IsInstanceOfType<SslStreamTlsProvider>(transports.ProxyTlsProvider);
        Assert.AreSame(transports.TlsProvider, CapturedDependency<ITlsProvider>(transports.TcpConnector, "<tlsProvider>"));
    }

    // ADR-0151 (BL-709): --curves and --sigalgs run the origin's TLS on the hand-built client
    // on every platform; the proxy's handshake does not carry them.
    [TestMethod]
    [DataRow("--curves", "X25519")]
    [DataRow("--sigalgs", "ECDSA+SHA256")]
    public void CreateTransports_WithCurvesOrSigalgs_UpgradesTheOriginWithTheHandBuiltClient(string option, string value)
    {
        Diagnostics.Arrange("arguments", $"{option} {value} https://example.com/");
        CurlTransports transports = CurlComposition.CreateTransports(Parse(option, value, "https://example.com/"));

        Diagnostics.Act("origin tls provider type", transports.TlsProvider.GetType().Name);
        Diagnostics.Act("proxy tls provider type", transports.ProxyTlsProvider.GetType().Name);
        Diagnostics.Assert("origin tls provider type", nameof(HandBuiltTlsProvider), transports.TlsProvider.GetType().Name);
        Assert.IsInstanceOfType<HandBuiltTlsProvider>(transports.TlsProvider);
        Assert.IsInstanceOfType<SslStreamTlsProvider>(transports.ProxyTlsProvider);
    }

    [TestMethod]
    public void CreateTransports_TcpConnector_ReceivesTcpDialerAndSecureSslStreamTlsProvider()
    {
        Diagnostics.Arrange("arguments", "gophers://example.com/");
        CurlTransports transports = CurlComposition.CreateTransports(NoOptions());

        Diagnostics.Act("insecure", transports.TlsClientOptions.Insecure);
        Diagnostics.Assert("insecure", false, transports.TlsClientOptions.Insecure);
        Assert.AreSame(transports.TcpDialer, CapturedDependency<ITcpDialer>(transports.TcpConnector));
        Assert.AreSame(transports.TlsProvider, CapturedDependency<ITlsProvider>(transports.TcpConnector, "<tlsProvider>"));
        Assert.AreSame(transports.TlsClientOptions, CapturedDependency<TlsClientOptions>(transports.TlsProvider));
        Assert.IsFalse(transports.TlsClientOptions.Insecure);
    }

    [TestMethod]
    public void CreateTransports_InsecureAndCaCertificate_ProxyTlsProviderStillVerifiesAgainstTheSystemStore()
    {
        // curl -s -S -k -x https://localhost:18462 https://example.com/ against a self-signed proxy -> exit 60,
        // and the same with --cacert <the proxy's certificate> (curl 8.21.0, 2026-09-27, BL-362).
        Diagnostics.Arrange("arguments", "-k --cacert x.pem https://example.com/");
        CommandLineOptions options = Parse("-k", "--cacert", "x.pem", "https://example.com/");

        CurlTransports transports = CurlComposition.CreateTransports(options);

        Diagnostics.Act("proxy tls client options", transports.ProxyTlsClientOptions);
        Diagnostics.Assert("proxy tls client options", new TlsClientOptions(), transports.ProxyTlsClientOptions);
        Assert.AreEqual(new TlsClientOptions(), transports.ProxyTlsClientOptions);
        Assert.AreSame(transports.ProxyTlsProvider, CapturedDependency<ITlsProvider>(transports.TcpConnector, "_proxyTlsProvider"));
        Assert.AreSame(transports.ProxyTlsClientOptions, CapturedDependency<TlsClientOptions>(transports.ProxyTlsProvider));
    }

    [TestMethod]
    public void CreateTransports_ProxyInsecureAndProxyCacert_TcpConnectorsProxyTlsProviderIsMadeFromThemNotFromTheTargets()
    {
        // curl -s -S --proxy-insecure (or --proxy-cacert <the proxy's certificate>) -x https://localhost:18462
        // https://example.com/ reaches CONNECT: curl: (7) CONNECT tunnel failed, response 407 (curl 8.21.0, 2026-09-27).
        CommandLineOptions options = Parse(
            "--cacert", "x.pem", "--proxy-insecure", "--proxy-cacert", "proxy.pem", "--proxy-capath", "proxy-certs", "https://example.com/");

        Diagnostics.Arrange("arguments", "--cacert x.pem --proxy-insecure --proxy-cacert proxy.pem --proxy-capath proxy-certs https://example.com/");
        CurlTransports transports = CurlComposition.CreateTransports(options);

        Diagnostics.Act("proxy tls client options", transports.ProxyTlsClientOptions);
        Diagnostics.Act("target tls client options", transports.TlsClientOptions);
        Diagnostics.Assert(
            "proxy tls client options",
            new TlsClientOptions(Insecure: true, CaCertificateFile: "proxy.pem", CaCertificateDirectory: "proxy-certs"),
            transports.ProxyTlsClientOptions);
        Assert.AreEqual(
            new TlsClientOptions(Insecure: true, CaCertificateFile: "proxy.pem", CaCertificateDirectory: "proxy-certs"),
            transports.ProxyTlsClientOptions);
        Assert.AreEqual(new TlsClientOptions(CaCertificateFile: "x.pem"), transports.TlsClientOptions);
        Assert.AreNotSame(transports.TlsProvider, transports.ProxyTlsProvider);
        Assert.AreSame(transports.ProxyTlsProvider, CapturedDependency<ITlsProvider>(transports.TcpConnector, "_proxyTlsProvider"));
        Assert.AreSame(transports.TlsProvider, CapturedDependency<ITlsProvider>(transports.TcpConnector, "<tlsProvider>"));
        Assert.AreSame(transports.ProxyTlsClientOptions, CapturedDependency<TlsClientOptions>(transports.ProxyTlsProvider));
    }

    [TestMethod]
    public void CreateTransports_GivenTimeProvider_ProxyTlsProviderTimesOnIt()
    {
        TimeProvider timeProvider = new ReplacementTimeProvider();
        Diagnostics.Arrange("time provider", timeProvider.GetType().Name);

        CurlTransports transports = CurlComposition.CreateTransports(NoOptions(), timeProvider);

        bool sameClock = ReferenceEquals(timeProvider, CapturedDependency<TimeProvider>(transports.ProxyTlsProvider));
        Diagnostics.Act("proxy tls provider uses the given time provider", sameClock);
        Diagnostics.Assert("proxy tls provider uses the given time provider", true, sameClock);
        Assert.AreSame(timeProvider, CapturedDependency<TimeProvider>(transports.ProxyTlsProvider));
    }

    [TestMethod]
    public void CreateTransports_InsecureCaCertificateAndTlsv13_SslStreamTlsProviderReceivesMappedOptions()
    {
        Diagnostics.Arrange("arguments", "-k --cacert x.pem --tlsv1.3 gophers://example.com/");
        CommandLineOptions options = Parse("-k", "--cacert", "x.pem", "--tlsv1.3", "gophers://example.com/");

        CurlTransports transports = CurlComposition.CreateTransports(options);

        TlsClientOptions expected = new(Insecure: true, MinimumVersion: TlsVersion.Tls13, CaCertificateFile: "x.pem");
        Diagnostics.Act("tls client options", transports.TlsClientOptions);
        Diagnostics.Assert("tls client options", expected, transports.TlsClientOptions);
        Assert.AreEqual(expected, transports.TlsClientOptions);
        Assert.AreSame(transports.TlsClientOptions, CapturedDependency<TlsClientOptions>(transports.TlsProvider));
    }

    [TestMethod]
    public void CreateTransports_EachCall_BuildsItsOwnResolver()
    {
        Diagnostics.Arrange("arguments", "gophers://example.com/");
        IDnsResolver first = CurlComposition.CreateTransports(NoOptions()).DnsResolver;
        IDnsResolver second = CurlComposition.CreateTransports(NoOptions()).DnsResolver;

        Diagnostics.Act("resolvers are the same instance", ReferenceEquals(first, second));
        Diagnostics.Assert("resolvers are the same instance", false, ReferenceEquals(first, second));
        Assert.AreNotSame(first, second);
    }

    [TestMethod]
    public void CreateDispatcher_ProductionTransports_BuildsWithoutADuplicateScheme()
    {
        Diagnostics.Arrange("arguments", "gophers://example.com/");
        ProtocolDispatcher dispatcher = CurlComposition.CreateDispatcher(CurlComposition.CreateTransports(NoOptions()));

        Diagnostics.Act("dispatcher built", dispatcher is not null);
        Diagnostics.Assert("dispatcher built", true, dispatcher is not null);
        Assert.IsNotNull(dispatcher);
    }

    [TestMethod]
    public void CreateTransferDispatch_ProductionTransports_WarnsWithTheProxyTlsProvidersWarnings()
    {
        Diagnostics.Arrange("arguments", "gophers://example.com/");
        CurlTransports transports = CurlComposition.CreateTransports(NoOptions());

        TransferDispatch dispatch = CurlComposition.CreateTransferDispatch(transports);

        Diagnostics.Act("dispatch warning lines", string.Join(" | ", dispatch.WarningLinesBeforeEachTransfer));
        Diagnostics.Assert("dispatch warning lines", string.Join(" | ", transports.ProxyTlsProvider.Warnings), string.Join(" | ", dispatch.WarningLinesBeforeEachTransfer));
        Assert.IsNotNull(dispatch.Dispatcher);
        CollectionAssert.AreEqual(transports.ProxyTlsProvider.Warnings.ToArray(), dispatch.WarningLinesBeforeEachTransfer.ToArray());
    }

    [TestMethod]
    public void CreateTransferDispatch_Cookies_GivesTheRunnerTheRunsCookies()
    {
        Diagnostics.Arrange("arguments", "-c jar.txt http://example.com/");
        CookieEngine cookies = CookieEngine.FromCommandLine(Parse("-c", "jar.txt", "http://example.com/"))!;

        TransferDispatch dispatch = CurlComposition.CreateTransferDispatch(CurlComposition.CreateTransports(NoOptions()), cookies);

        Diagnostics.Act("dispatch cookies are the run's cookies", ReferenceEquals(cookies, dispatch.Cookies));
        Diagnostics.Assert("dispatch cookies are the run's cookies", true, ReferenceEquals(cookies, dispatch.Cookies));
        Assert.AreSame(cookies, dispatch.Cookies);
    }

    [TestMethod]
    [DataRow(new[] { "-Z", "http://example.com/" }, true)]
    [DataRow(new[] { "-Z", "--parallel-immediate", "http://example.com/" }, false)]
    [DataRow(new[] { "http://example.com/" }, false)]
    public void CreateTransports_PoolingConnector_WaitsForMultiplexingUnderParallelWithoutParallelImmediate(string[] arguments, bool waits)
    {
        Diagnostics.Arrange("arguments", string.Join(" ", arguments));
        CurlTransports transports = CurlComposition.CreateTransports(Parse(arguments));

        Diagnostics.Act("pooling connector waits for multiplexing", transports.PoolingConnector.WaitsForMultiplexing);
        Diagnostics.Assert("pooling connector waits for multiplexing", waits, transports.PoolingConnector.WaitsForMultiplexing);
        Assert.AreEqual(waits, transports.PoolingConnector.WaitsForMultiplexing);
    }

    [TestMethod]
    public void CreateTransports_PoolingConnector_WrapsTheTcpConnectorOnTheSameClock()
    {
        TimeProvider timeProvider = new ReplacementTimeProvider();
        Diagnostics.Arrange("time provider", timeProvider.GetType().Name);

        CurlTransports transports = CurlComposition.CreateTransports(NoOptions(), timeProvider);

        bool wrapsTcpConnector = ReferenceEquals(transports.TcpConnector, CapturedDependency<IConnector>(transports.PoolingConnector));
        Diagnostics.Act("pooling connector wraps the tcp connector", wrapsTcpConnector);
        Diagnostics.Assert("pooling connector wraps the tcp connector", true, wrapsTcpConnector);
        Assert.AreSame(transports.TcpConnector, CapturedDependency<IConnector>(transports.PoolingConnector));
        Assert.AreSame(timeProvider, CapturedDependency<TimeProvider>(CapturedDependency<ConnectionCache>(transports.PoolingConnector)));
    }

    [TestMethod]
    public void CreateTransferDispatch_ProductionTransports_EveryTcpHandlerReceivesTheOnePoolingConnector()
    {
        Diagnostics.Arrange("arguments", "gophers://example.com/");
        CurlTransports transports = CurlComposition.CreateTransports(NoOptions());

        TransferDispatch dispatch = CurlComposition.CreateTransferDispatch(transports);

        IProtocolHandler[] handlers = [.. CapturedDependency<Dictionary<string, IProtocolHandler>>(dispatch.Dispatcher).Values.Distinct()];
        IConnector ftpData = Unrecorded(CapturedDependency<IConnector>(FtpHandlerOf(dispatch), "dataConnector"));
        IConnector[] connectors = [.. handlers.SelectMany(ConnectorsOf).Where(connector => !ReferenceEquals(connector, ftpData))];
        string[] connectingHandlers = [.. handlers.Where(handler => ConnectorsOf(handler).Any()).Select(handler => Unwrapped(handler).GetType().Name).Order()];
        Diagnostics.Act("connecting handlers", string.Join(", ", connectingHandlers));
        Diagnostics.Act("connector count", connectors.Length);
        Diagnostics.Assert("connecting handler count", 15, connectingHandlers.Length);
        CollectionAssert.AreEqual(
            new[] { "DictProtocolHandler", "GopherProtocolHandler", "HttpProtocolHandler", "ImapProtocolHandler", "LdapProtocolHandler", "MqttProtocolHandler", "Pop3ProtocolHandler", "RoutingFtpProtocolHandler", "RtspProtocolHandler", "SmbProtocolHandler", "SmtpProtocolHandler", "SshProtocolHandler", "TelnetProtocolHandler", "TftpProtocolHandler", "WsProtocolHandler" },
            connectingHandlers);
        Assert.IsTrue(connectors.All(connector => ReferenceEquals(connector, transports.PoolingConnector)));
        Assert.AreSame(transports.PoolingConnector, dispatch.ConnectionPool);
    }

    [TestMethod]
    public void CreateDispatcher_ProductionTransports_HttpHandlerReceivesThePoolingConnector()
    {
        Diagnostics.Arrange("arguments", "gophers://example.com/");
        CurlTransports transports = CurlComposition.CreateTransports(NoOptions());

        ProtocolDispatcher dispatcher = CurlComposition.CreateDispatcher(transports);

        IProtocolHandler http = CapturedDependency<Dictionary<string, IProtocolHandler>>(dispatcher)["http"];
        Diagnostics.Act("http handler connector count", ConnectorsOf(http).Count());
        Diagnostics.Assert("http handler connector is the pooling connector", true, ReferenceEquals(transports.PoolingConnector, ConnectorsOf(http).Single()));
        Assert.AreSame(transports.PoolingConnector, ConnectorsOf(http).Single());
    }

    [TestMethod]
    public void CreateTransferDispatch_ProductionTransports_FtpHandlerGetsTheListenerTlsProviderAndResolver()
    {
        Diagnostics.Arrange("arguments", "gophers://example.com/");
        CurlTransports transports = CurlComposition.CreateTransports(NoOptions());

        TransferDispatch dispatch = CurlComposition.CreateTransferDispatch(transports);

        Dictionary<string, IProtocolHandler> handlers = CapturedDependency<Dictionary<string, IProtocolHandler>>(dispatch.Dispatcher);
        Diagnostics.Act("ftp and ftps share one handler", ReferenceEquals(handlers["ftp"], handlers["ftps"]));
        Diagnostics.Assert("ftp and ftps share one handler", true, ReferenceEquals(handlers["ftp"], handlers["ftps"]));
        Assert.AreSame(handlers["ftp"], handlers["ftps"]);
        FtpProtocolHandler ftp = FtpHandlerOf(dispatch);
        Assert.IsInstanceOfType<TcpConnectionListener>(CapturedDependency<IConnectionListener>(ftp));
        Assert.AreSame(transports.TlsProvider, CapturedDependency<ITlsProvider>(ftp));
        Assert.AreSame(transports.DnsResolver, CapturedDependency<IDnsResolver>(ftp));
        Assert.IsInstanceOfType<SystemNetworkInterfaceLookup>(CapturedDependency<INetworkInterfaceLookup>(ftp));
        Assert.AreSame(transports.PoolingConnector, Unrecorded(CapturedDependency<IConnector>(ftp, "connector")));
    }

    [TestMethod]
    public void CreateTransferDispatch_ProductionTransports_FtpDataConnectionsGoThroughThePoolOverAConnectorWithoutTheConnectTimeout()
    {
        Diagnostics.Arrange("arguments", "gophers://example.com/");
        CurlTransports transports = CurlComposition.CreateTransports(NoOptions());

        TransferDispatch dispatch = CurlComposition.CreateTransferDispatch(transports);

        // BL-797: curl 8.21.0 does not hold a passive data connect to --connect-timeout.
        PoolingConnector data = (PoolingConnector)Unrecorded(CapturedDependency<IConnector>(FtpHandlerOf(dispatch), "dataConnector"));
        Diagnostics.Act("data connector is the main pooling connector", ReferenceEquals(transports.PoolingConnector, data));
        Diagnostics.Assert("data connector is the main pooling connector", false, ReferenceEquals(transports.PoolingConnector, data));
        Assert.AreNotSame(transports.PoolingConnector, data);
        Assert.AreSame(CapturedDependency<ConnectionCache>(transports.PoolingConnector), CapturedDependency<ConnectionCache>(data));
        IConnector inner = CapturedDependency<IConnector>(data);
        Assert.AreNotSame(transports.TcpConnector, inner);
        Assert.AreSame(transports.TcpConnector, CapturedDependency<TcpConnector>(inner));
    }

    [TestMethod]
    public void CreateDispatcher_ProductionTransports_FtpDataConnectionsGoThroughThePoolOverAConnectorWithoutTheConnectTimeout()
    {
        Diagnostics.Arrange("arguments", "gophers://example.com/");
        CurlTransports transports = CurlComposition.CreateTransports(NoOptions());

        ProtocolDispatcher dispatcher = CurlComposition.CreateDispatcher(transports);

        IProtocolHandler routing = Unwrapped(CapturedDependency<Dictionary<string, IProtocolHandler>>(dispatcher)["ftp"]);
        FtpProtocolHandler ftp = (FtpProtocolHandler)CapturedDependency<IProtocolHandler>(routing, "ftpHandler");
        PoolingConnector data = (PoolingConnector)Unrecorded(CapturedDependency<IConnector>(ftp, "dataConnector"));
        bool sharesCache = ReferenceEquals(CapturedDependency<ConnectionCache>(transports.PoolingConnector), CapturedDependency<ConnectionCache>(data));
        Diagnostics.Act("data connector shares the connection cache", sharesCache);
        Diagnostics.Assert("data connector shares the connection cache", true, sharesCache);
        Assert.AreSame(CapturedDependency<ConnectionCache>(transports.PoolingConnector), CapturedDependency<ConnectionCache>(data));
    }

    /// <summary>The FTP handler behind the <c>ftp</c> scheme's router in <paramref name="dispatch" />.</summary>
    private static FtpProtocolHandler FtpHandlerOf(TransferDispatch dispatch) =>
        (FtpProtocolHandler)CapturedDependency<IProtocolHandler>(
            Unwrapped(CapturedDependency<Dictionary<string, IProtocolHandler>>(dispatch.Dispatcher)["ftp"]),
            "ftpHandler");

    [TestMethod]
    public async Task CreateRunner_FtpsUrlWithFakeConnectors_ReachesConnectorAtPort990WithTls()
    {
        Diagnostics.Arrange("url", "ftps://h/f");
        RecordingConnector connector = new(CurlExitCode.CouldntConnect, ConnectFailure);

        await RunWithFakeConnectorsAsync("ftps://h/f", connector, new RecordingDatagramConnector(CurlExitCode.CouldntConnect, ConnectFailure));

        ConnectTarget target = connector.Targets.Single();
        Diagnostics.Act("connector target host", target.Host);
        Diagnostics.Act("connector target port", target.Port);
        Diagnostics.Act("connector target uses tls", target.UseTls);
        Diagnostics.Assert("connector target port", 990, target.Port);
        Assert.AreEqual("h", target.Host);
        Assert.AreEqual(990, target.Port);
        Assert.IsTrue(target.UseTls);
    }

    [TestMethod]
    public async Task CreateTransferDispatch_TwoUrlsToOneHost_TheSecondReusesTheFirstsConnection()
    {
        ScriptedConnector server = new(
            [Encoding.Latin1.GetBytes("HTTP/1.1 200 OK\r\nContent-Length: 1\r\n\r\na"), Encoding.Latin1.GetBytes("HTTP/1.1 200 OK\r\nContent-Length: 1\r\n\r\nb")]);
        using MemoryStream standardOutput = new();
        Diagnostics.Arrange("arguments", "-sS http://h:18233/a http://h:18233/b");
        Diagnostics.Arrange("scripted responses", "HTTP/1.1 200 OK, Content-Length: 1, body a; then body b");

        int exitCode = await new CurlCommandRunner(
                options => CurlComposition.CreateTransferDispatch(
                    CurlComposition.CreateTransports(options) with { PoolingConnector = new PoolingConnector(server, TimeProvider.System) }),
                new InMemoryFileSystem(),
                new InMemoryFileSystem(),
                standardOutput,
                new MemoryStream(),
                new MemoryStream(),
                runsOnWindows: false)
            .RunAsync(["-sS", "http://h:18233/a", "http://h:18233/b"]);

        string output = Encoding.Latin1.GetString(standardOutput.ToArray());
        Diagnostics.Act("exit code", exitCode);
        Diagnostics.Act("standard output", output);
        Diagnostics.Act("connects made", server.Targets.Count());
        Diagnostics.Assert("standard output", "ab", output);
        Assert.AreEqual(0, exitCode);
        Assert.AreEqual("ab", Encoding.Latin1.GetString(standardOutput.ToArray()));
        Assert.AreEqual(new ConnectTarget("h", 18233, false) { PoolScheme = "http" }, server.Targets.Single());
    }

    [TestMethod]
    public void CreateTransferDispatch_CaPath_WarnsAsThePlatformsCurlBuildDoes()
    {
        Diagnostics.Arrange("arguments", "--capath . https://example.com/");
        CurlTransports transports = CurlComposition.CreateTransports(Parse("--capath", ".", "https://example.com/"));

        TransferDispatch dispatch = CurlComposition.CreateTransferDispatch(transports);

        // ADR-0009: the Schannel build ignores --capath with this warning, which the runner
        // wraps at the terminal width; the OpenSSL build honours it and prints nothing.
        string[] expected = OperatingSystem.IsWindows()
            ? ["Warning: ignoring setting the CA path for the proxy, not supported by libcurl with Schannel"]
            : [];
        Diagnostics.Act("warning lines", string.Join(" | ", dispatch.WarningLinesBeforeEachTransfer));
        Diagnostics.Assert("warning lines", string.Join(" | ", expected), string.Join(" | ", dispatch.WarningLinesBeforeEachTransfer));
        CollectionAssert.AreEqual(expected, dispatch.WarningLinesBeforeEachTransfer.ToArray());
    }

    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    public void CreateTransferDispatch_Tls13CiphersOnWindows_WarnsAsTheSchannelBuildDoes()
    {
        Diagnostics.Arrange("arguments", "--tls13-ciphers BOGUS --proxy-tls13-ciphers BOGUS https://example.com/");
        CurlTransports transports = CurlComposition.CreateTransports(
            Parse("--tls13-ciphers", "BOGUS", "--proxy-tls13-ciphers", "BOGUS", "https://example.com/"));

        TransferDispatch dispatch = CurlComposition.CreateTransferDispatch(transports);

        // Measured, curl 8.21.0 Schannel (BL-1034): one line each, --tls13-ciphers first.
        string[] expected =
        [
            "Warning: ignoring --tls13-ciphers, not supported by libcurl with Schannel",
            "Warning: ignoring --proxy-tls13-ciphers, not supported by libcurl with Schannel",
        ];
        Diagnostics.Act("warning lines", string.Join(" | ", dispatch.WarningLinesBeforeEachTransfer));
        Diagnostics.Assert("warning lines", string.Join(" | ", expected), string.Join(" | ", dispatch.WarningLinesBeforeEachTransfer));
        CollectionAssert.AreEqual(expected, dispatch.WarningLinesBeforeEachTransfer.ToArray());
    }

    [TestMethod]
    [OSCondition(ConditionMode.Exclude, OperatingSystems.Windows)]
    public void CreateTransferDispatch_Tls13CiphersOffWindows_PrintsNoWarning()
    {
        Diagnostics.Arrange("arguments", "--tls13-ciphers TLS_AES_128_GCM_SHA256 --proxy-tls13-ciphers TLS_AES_128_GCM_SHA256 https://example.com/");
        CurlTransports transports = CurlComposition.CreateTransports(
            Parse("--tls13-ciphers", "TLS_AES_128_GCM_SHA256", "--proxy-tls13-ciphers", "TLS_AES_128_GCM_SHA256", "https://example.com/"));

        TransferDispatch dispatch = CurlComposition.CreateTransferDispatch(transports);

        Diagnostics.Act("warning line count", dispatch.WarningLinesBeforeEachTransfer.Count());
        Diagnostics.Assert("warning line count", 0, dispatch.WarningLinesBeforeEachTransfer.Count());
        Assert.IsEmpty(dispatch.WarningLinesBeforeEachTransfer);
    }

    [TestMethod]
    public void WarningLinesBeforeEachTransfer_SchannelBuildWithCaPathAndBothTls13CipherLists_ListsCaPathThenTls13ThenProxyTls13()
    {
        Diagnostics.Arrange("arguments", "--capath . --proxy-tls13-ciphers B --tls13-ciphers BOGUS https://example.com/");
        CurlTransports transports = CurlComposition.CreateTransports(
            Parse("--capath", ".", "--proxy-tls13-ciphers", "B", "--tls13-ciphers", "BOGUS", "https://example.com/"));

        IReadOnlyList<string> lines = CurlComposition.WarningLinesBeforeEachTransfer(
            transports with { ProxyTlsProvider = new WarningTlsProvider("Warning: ignoring setting the CA path for the proxy, not supported by libcurl with Schannel") },
            matchesSchannelBuild: true);

        // Measured, curl 8.21.0 Schannel (BL-1034): this order whatever the command line's.
        string[] expected =
        [
            "Warning: ignoring setting the CA path for the proxy, not supported by libcurl with Schannel",
            "Warning: ignoring --tls13-ciphers, not supported by libcurl with Schannel",
            "Warning: ignoring --proxy-tls13-ciphers, not supported by libcurl with Schannel",
        ];
        Diagnostics.Act("warning lines", string.Join(" | ", lines));
        Diagnostics.Assert("warning lines", string.Join(" | ", expected), string.Join(" | ", lines));
        CollectionAssert.AreEqual(expected, lines.ToArray());
    }

    [TestMethod]
    [DataRow("--tls13-ciphers", "Warning: ignoring --tls13-ciphers, not supported by libcurl with Schannel")]
    [DataRow("--proxy-tls13-ciphers", "Warning: ignoring --proxy-tls13-ciphers, not supported by libcurl with Schannel")]
    public void WarningLinesBeforeEachTransfer_SchannelBuildWithOneTls13CipherList_ListsOnlyItsWarning(string option, string expected)
    {
        Diagnostics.Arrange("arguments", $"{option} BOGUS http://example.com/");
        CurlTransports transports = CurlComposition.CreateTransports(Parse(option, "BOGUS", "http://example.com/"));

        IReadOnlyList<string> lines = CurlComposition.WarningLinesBeforeEachTransfer(transports, matchesSchannelBuild: true);

        Diagnostics.Act("warning lines", string.Join(" | ", lines));
        Diagnostics.Assert("warning lines", expected, string.Join(" | ", lines));
        CollectionAssert.AreEqual(new[] { expected }, lines.ToArray());
    }

    [TestMethod]
    public void WarningLinesBeforeEachTransfer_SchannelBuildWithNoIgnoredOption_ListsNothing()
    {
        Diagnostics.Arrange("arguments", "https://example.com/");
        CurlTransports transports = CurlComposition.CreateTransports(Parse("https://example.com/"));

        IReadOnlyList<string> lines = CurlComposition.WarningLinesBeforeEachTransfer(transports, matchesSchannelBuild: true);

        Diagnostics.Act("warning line count", lines.Count);
        Diagnostics.Assert("warning line count", 0, lines.Count);
        Assert.IsEmpty(lines);
    }

    [TestMethod]
    public void WarningLinesBeforeEachTransfer_OpenSslBuildWithBothTls13CipherLists_ListsNothing()
    {
        Diagnostics.Arrange("arguments", "--tls13-ciphers TLS_AES_128_GCM_SHA256 --proxy-tls13-ciphers TLS_AES_128_GCM_SHA256 https://example.com/");
        CurlTransports transports = CurlComposition.CreateTransports(
            Parse("--tls13-ciphers", "TLS_AES_128_GCM_SHA256", "--proxy-tls13-ciphers", "TLS_AES_128_GCM_SHA256", "https://example.com/"));

        IReadOnlyList<string> lines = CurlComposition.WarningLinesBeforeEachTransfer(
            transports with { ProxyTlsProvider = new WarningTlsProvider() },
            matchesSchannelBuild: false);

        Diagnostics.Act("warning line count", lines.Count);
        Diagnostics.Assert("warning line count", 0, lines.Count);
        Assert.IsEmpty(lines);
    }

    [TestMethod]
    public void CreateTransports_CaPathCertKeyAndCiphers_SslStreamTlsProviderReceivesMappedOptions()
    {
        Diagnostics.Arrange("arguments", "--capath certs --cert c.p12:pw --key k.pem --ciphers AES128-SHA --tls13-ciphers TLS_AES_128_GCM_SHA256 https://example.com/");
        CommandLineOptions options = Parse(
            "--capath", "certs", "--cert", "c.p12:pw", "--key", "k.pem",
            "--ciphers", "AES128-SHA", "--tls13-ciphers", "TLS_AES_128_GCM_SHA256", "https://example.com/");

        CurlTransports transports = CurlComposition.CreateTransports(options);

        TlsClientOptions expected = new(
            CaCertificateDirectory: "certs",
            ClientCertificate: "c.p12:pw",
            PrivateKey: "k.pem",
            Ciphers: "AES128-SHA",
            Tls13Ciphers: "TLS_AES_128_GCM_SHA256");
        Diagnostics.Act("tls client options", transports.TlsClientOptions);
        Diagnostics.Assert("tls client options", expected, transports.TlsClientOptions);
        Assert.AreEqual(expected, transports.TlsClientOptions);
        Assert.AreSame(transports.TlsClientOptions, CapturedDependency<TlsClientOptions>(transports.TlsProvider));
    }

    /// <summary>
    /// Runs <paramref name="url" /> through the production handler set built around the
    /// fake connectors, and returns the exit code and what reached standard error.
    /// </summary>
    private static async Task<(int ExitCode, string StandardErrorText)> RunWithFakeConnectorsAsync(
        string url,
        IConnector connector,
        IDatagramConnector datagramConnector)
    {
        using MemoryStream standardOutput = new();
        using MemoryStream standardError = new();
        using MemoryStream standardInput = new();

        int exitCode = await CurlComposition
            .CreateRunner(standardOutput, standardError, standardInput, connector, datagramConnector)
            .RunAsync([url]);

        return (exitCode, Encoding.UTF8.GetString(standardError.ToArray()));
    }

    private static CommandLineOptions NoOptions() => Parse("gophers://example.com/");

    /// <summary>
    /// Parses <paramref name="arguments" /> as if every path exists, so <c>--cacert</c>
    /// accepts a file name without touching the disk.
    /// </summary>
    private static CommandLineOptions Parse(params string[] arguments)
    {
        CommandLineParseResult parsed = CommandLineParser.Parse(arguments, _ => true);
        Assert.IsTrue(parsed.IsAccepted);
        return parsed.Options;
    }

    /// <summary>
    /// Reads the one dependency of type <typeparamref name="T" /> that <paramref name="owner" />
    /// holds in a private field. The networking types keep what they were constructed with
    /// private, so this is how a test checks the wiring without opening a socket.
    /// </summary>
    private static T CapturedDependency<T>(object owner)
        where T : class
    {
        FieldInfo field = owner.GetType()
            .GetFields(BindingFlags.Instance | BindingFlags.NonPublic)
            .Single(candidate => candidate.FieldType == typeof(T));
        return (T)field.GetValue(owner)!;
    }

    /// <summary>
    /// Reads the dependency of type <typeparamref name="T" /> that <paramref name="owner" /> holds
    /// in the private field whose name contains <paramref name="fieldNameFragment" />, for an owner
    /// that holds more than one of that type: a captured primary-constructor parameter's field is
    /// named <c>&lt;parameter&gt;P</c>.
    /// </summary>
    private static T CapturedDependency<T>(object owner, string fieldNameFragment)
        where T : class
    {
        FieldInfo field = owner.GetType()
            .GetFields(BindingFlags.Instance | BindingFlags.NonPublic)
            .Single(candidate => candidate.FieldType == typeof(T) && candidate.Name.Contains(fieldNameFragment, StringComparison.Ordinal));
        return (T)field.GetValue(owner)!;
    }

    /// <summary>
    /// The handler <paramref name="handler" /> reports the end points of, as every registered
    /// handler is wrapped in an <see cref="EndPointReportingProtocolHandler" /> (ADR-0119).
    /// </summary>
    private static IProtocolHandler Unwrapped(IProtocolHandler handler) =>
        ((EndPointReportingProtocolHandler)handler).Handler;

    /// <summary>
    /// The connector <paramref name="connector" /> records the connections of, as every handler
    /// connects through an <see cref="EndPointRecordingConnector" /> (ADR-0119).
    /// </summary>
    private static IConnector Unrecorded(IConnector connector) =>
        CapturedDependency<IConnector>((EndPointRecordingConnector)connector);

    /// <summary>
    /// Reads every connector <paramref name="handler" /> holds in a private field, and those of
    /// any handler it forwards to, so a test can check which connector each handler was given.
    /// </summary>
    private static IEnumerable<IConnector> ConnectorsOf(IProtocolHandler handler)
    {
        foreach (FieldInfo field in handler.GetType().GetFields(BindingFlags.Instance | BindingFlags.NonPublic))
        {
            switch (field.GetValue(handler))
            {
                case IConnector connector:
                    yield return Unrecorded(connector);
                    break;
                case IProtocolHandler forwardedTo:
                    foreach (IConnector connector in ConnectorsOf(forwardedTo))
                    {
                        yield return connector;
                    }

                    break;
            }
        }
    }

    private sealed class ReplacementTimeProvider : TimeProvider;

    // A proxy TLS provider that only reports the warnings it is given; it never connects.
    private sealed class WarningTlsProvider(params string[] warnings) : ITlsProviderWithWarnings
    {
        public IReadOnlyList<string> Warnings { get; } = warnings;

        public ValueTask<ConnectResult> AuthenticateAsClientAsync(IConnection plaintext, string targetHost, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }
}
