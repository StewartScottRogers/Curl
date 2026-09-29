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
using Curl.Protocol.Mqtt;
using Curl.Protocol.Pop3;
using Curl.Protocol.Smtp;
using Curl.Protocol.Telnet;
using Curl.Protocol.Tftp;
using Curl.Protocol.Ws;

namespace Curl.Console;

/// <summary>
/// Pins the composition root: which handlers the executable registers, how the network
/// transports are wired, and that the production composition performs a <c>file://</c>
/// transfer end to end. No test here opens a socket.
/// </summary>
[TestClass]
public sealed class CurlCompositionTests
{
    private const string ConnectFailure = "Failed to connect to h:2628 after 0 ms: Could not connect to server";

    [TestMethod]
    [DataRow(true, WriteOutTimeDialect.WindowsCRuntime)]
    [DataRow(false, WriteOutTimeDialect.Glibc)]
    public void WriteOutTimeDialectFor_Platform_IsThatPlatformsCRuntime(bool runsOnWindows, WriteOutTimeDialect expected)
    {
        Assert.AreEqual(expected, CurlComposition.WriteOutTimeDialectFor(runsOnWindows));
    }

    [TestMethod]
    public void CreateProtocolHandlers_ServesEachSchemeThroughItsHandlerOnce()
    {
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
            ["pop3"] = typeof(Pop3ProtocolHandler),
            ["pop3s"] = typeof(Pop3ProtocolHandler),
            ["smtp"] = typeof(SmtpProtocolHandler),
            ["smtps"] = typeof(SmtpProtocolHandler),
            ["ws"] = typeof(WsProtocolHandler),
            ["wss"] = typeof(WsProtocolHandler),
            ["http"] = typeof(HttpProtocolHandler),
            ["https"] = typeof(HttpProtocolHandler),
            ["ftp"] = typeof(RoutingFtpProtocolHandler),
            ["ftps"] = typeof(RoutingFtpProtocolHandler),
        };
        CollectionAssert.AreEquivalent(expected.ToList(), served.ToList());
        _ = new ProtocolDispatcher(handlers);
    }

    [TestMethod]
    [DataRow("gophers://h/", 70, true, null)]
    [DataRow("mqtts://h/", 8883, true, null)]
    [DataRow("imaps://h/", 993, true, null)]
    [DataRow("imap://h/", 143, false, null)]
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
        RecordingConnector connector = new(CurlExitCode.CouldntConnect, ConnectFailure);

        await RunWithFakeConnectorsAsync(url, connector, new RecordingDatagramConnector(CurlExitCode.CouldntConnect, ConnectFailure));

        Assert.AreEqual(new ConnectTarget("h", port, useTls) { PoolScheme = poolScheme }, connector.Targets.Single());
    }

    [TestMethod]
    public async Task CreateRunner_ConnectorFailsToConnect_PrintsExit7LineAndReturns7()
    {
        RecordingConnector connector = new(CurlExitCode.CouldntConnect, ConnectFailure);

        (int exitCode, string standardErrorText) = await RunWithFakeConnectorsAsync(
            "dict://h/d:x",
            connector,
            new RecordingDatagramConnector(CurlExitCode.CouldntConnect, ConnectFailure));

        Assert.AreEqual(7, exitCode);
        Assert.AreEqual($"curl: (7) {ConnectFailure}{Environment.NewLine}", standardErrorText);
    }

    [TestMethod]
    public async Task CreateRunner_TftpUrl_ReachesDatagramConnectorAtHostAndPort69()
    {
        RecordingDatagramConnector datagramConnector = new(CurlExitCode.CouldntConnect, ConnectFailure);

        await RunWithFakeConnectorsAsync(
            "tftp://h/f",
            new RecordingConnector(CurlExitCode.CouldntConnect, ConnectFailure),
            datagramConnector);

        Assert.AreEqual(("h", 69), datagramConnector.Opens.Single());
    }

    [TestMethod]
    public async Task CreateRunner_FileUrlOfTemporaryFile_WritesItsBytesToStandardOutput()
    {
        string path = Path.Combine(Path.GetTempPath(), $"curl-bl068-{Guid.NewGuid():N}.bin");
        byte[] content = [0, 1, 2, 13, 10, 255, (byte)'x'];
        await System.IO.File.WriteAllBytesAsync(path, content);

        try
        {
            using MemoryStream standardOutput = new();
            using MemoryStream standardError = new();
            using MemoryStream standardInput = new();

            int exitCode = await CurlComposition
                .CreateRunner(standardOutput, standardError, standardInput, standardOutputIsTerminal: true)
                .RunAsync([new Uri(path).AbsoluteUri]);

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

        try
        {
            using MemoryStream standardOutput = new();
            using MemoryStream standardError = new();
            using MemoryStream standardInput = new();

            int exitCode = await CurlComposition
                .CreateRunner(standardOutput, standardError, standardInput, standardOutputIsTerminal: false)
                .RunAsync(["-q", "ipfs://bafyabc"]);

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

        try
        {
            using MemoryStream standardOutput = new();
            using MemoryStream standardError = new();
            using MemoryStream standardInput = new();

            int exitCode = await CurlComposition
                .CreateRunner(standardOutput, standardError, standardInput, standardOutputIsTerminal: true)
                .RunAsync([new Uri(source).AbsoluteUri, "-w", $"%output{{{target}}}F\\n"]);

            Assert.AreEqual(0, exitCode);
            string expected = OperatingSystem.IsWindows() ? "F\r\n" : "F\n";
            Assert.AreEqual(expected, await System.IO.File.ReadAllTextAsync(target));
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

        try
        {
            using MemoryStream standardOutput = new();
            using MemoryStream standardError = new();
            using MemoryStream standardInput = new();

            int exitCode = await CurlComposition
                .CreateRunner(standardOutput, standardError, standardInput, standardOutputIsTerminal: false)
                .RunAsync([new Uri(path).AbsoluteUri]);

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
    public void CreateTransports_BothConnectors_ShareOneSystemDnsResolverAndTimeProviderSystem()
    {
        CurlTransports transports = CurlComposition.CreateTransports(NoOptions());

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
        CurlTransports transports = CurlComposition.CreateTransports(NoOptions());

        Assert.AreSame(TimeProvider.System, CapturedDependency<TimeProvider>(transports.TlsProvider));
    }

    [TestMethod]
    public void CreateTransports_GivenTimeProvider_SslStreamTlsProviderSharesTheTcpConnectorsTimeProvider()
    {
        TimeProvider timeProvider = new ReplacementTimeProvider();

        CurlTransports transports = CurlComposition.CreateTransports(NoOptions(), timeProvider);

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
        var options = new TlsClientOptions(MaximumVersion: maximumVersion);

        var provider = CurlComposition.CreateTlsProvider(options, TimeProvider.System);

        Assert.IsInstanceOfType(provider, expected);
        Assert.AreSame(options, CapturedDependency<TlsClientOptions>(provider));
    }

    [TestMethod]
    public void CreateTransports_WithTlsMax10_UpgradesTheOriginWithTheHandBuiltClientAndTheProxyWithSslStream()
    {
        CurlTransports transports = CurlComposition.CreateTransports(Parse("--tls-max", "1.0", "https://example.com/"));

        Assert.IsInstanceOfType<HandBuiltTlsProvider>(transports.TlsProvider);
        Assert.IsInstanceOfType<SslStreamTlsProvider>(transports.ProxyTlsProvider);
        Assert.AreSame(transports.TlsProvider, CapturedDependency<ITlsProvider>(transports.TcpConnector, "<tlsProvider>"));
    }

    [TestMethod]
    public void CreateTransports_TcpConnector_ReceivesTcpDialerAndSecureSslStreamTlsProvider()
    {
        CurlTransports transports = CurlComposition.CreateTransports(NoOptions());

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
        CommandLineOptions options = Parse("-k", "--cacert", "x.pem", "https://example.com/");

        CurlTransports transports = CurlComposition.CreateTransports(options);

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

        CurlTransports transports = CurlComposition.CreateTransports(options);

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

        CurlTransports transports = CurlComposition.CreateTransports(NoOptions(), timeProvider);

        Assert.AreSame(timeProvider, CapturedDependency<TimeProvider>(transports.ProxyTlsProvider));
    }

    [TestMethod]
    public void CreateTransports_InsecureCaCertificateAndTlsv13_SslStreamTlsProviderReceivesMappedOptions()
    {
        CommandLineOptions options = Parse("-k", "--cacert", "x.pem", "--tlsv1.3", "gophers://example.com/");

        CurlTransports transports = CurlComposition.CreateTransports(options);

        TlsClientOptions expected = new(Insecure: true, MinimumVersion: TlsVersion.Tls13, CaCertificateFile: "x.pem");
        Assert.AreEqual(expected, transports.TlsClientOptions);
        Assert.AreSame(transports.TlsClientOptions, CapturedDependency<TlsClientOptions>(transports.TlsProvider));
    }

    [TestMethod]
    public void CreateTransports_EachCall_BuildsItsOwnResolver()
    {
        Assert.AreNotSame(
            CurlComposition.CreateTransports(NoOptions()).DnsResolver,
            CurlComposition.CreateTransports(NoOptions()).DnsResolver);
    }

    [TestMethod]
    public void CreateDispatcher_ProductionTransports_BuildsWithoutADuplicateScheme()
    {
        ProtocolDispatcher dispatcher = CurlComposition.CreateDispatcher(CurlComposition.CreateTransports(NoOptions()));

        Assert.IsNotNull(dispatcher);
    }

    [TestMethod]
    public void CreateTransferDispatch_ProductionTransports_WarnsWithTheProxyTlsProvidersWarnings()
    {
        CurlTransports transports = CurlComposition.CreateTransports(NoOptions());

        TransferDispatch dispatch = CurlComposition.CreateTransferDispatch(transports);

        Assert.IsNotNull(dispatch.Dispatcher);
        Assert.AreSame(transports.ProxyTlsProvider.Warnings, dispatch.WarningLinesBeforeEachTransfer);
    }

    [TestMethod]
    public void CreateTransferDispatch_Cookies_GivesTheRunnerTheRunsCookies()
    {
        CookieEngine cookies = CookieEngine.FromCommandLine(Parse("-c", "jar.txt", "http://example.com/"))!;

        TransferDispatch dispatch = CurlComposition.CreateTransferDispatch(CurlComposition.CreateTransports(NoOptions()), cookies);

        Assert.AreSame(cookies, dispatch.Cookies);
    }

    [TestMethod]
    public void CreateTransports_PoolingConnector_WrapsTheTcpConnectorOnTheSameClock()
    {
        TimeProvider timeProvider = new ReplacementTimeProvider();

        CurlTransports transports = CurlComposition.CreateTransports(NoOptions(), timeProvider);

        Assert.AreSame(transports.TcpConnector, CapturedDependency<IConnector>(transports.PoolingConnector));
        Assert.AreSame(timeProvider, CapturedDependency<TimeProvider>(transports.PoolingConnector));
    }

    [TestMethod]
    public void CreateTransferDispatch_ProductionTransports_EveryTcpHandlerReceivesTheOnePoolingConnector()
    {
        CurlTransports transports = CurlComposition.CreateTransports(NoOptions());

        TransferDispatch dispatch = CurlComposition.CreateTransferDispatch(transports);

        IProtocolHandler[] handlers = [.. CapturedDependency<Dictionary<string, IProtocolHandler>>(dispatch.Dispatcher).Values.Distinct()];
        IConnector[] connectors = [.. handlers.SelectMany(ConnectorsOf)];
        string[] connectingHandlers = [.. handlers.Where(handler => ConnectorsOf(handler).Any()).Select(handler => Unwrapped(handler).GetType().Name).Order()];
        CollectionAssert.AreEqual(
            new[] { "DictProtocolHandler", "GopherProtocolHandler", "HttpProtocolHandler", "ImapProtocolHandler", "MqttProtocolHandler", "Pop3ProtocolHandler", "RoutingFtpProtocolHandler", "SmtpProtocolHandler", "TelnetProtocolHandler", "TftpProtocolHandler", "WsProtocolHandler" },
            connectingHandlers);
        Assert.IsTrue(connectors.All(connector => ReferenceEquals(connector, transports.PoolingConnector)));
        Assert.AreSame(transports.PoolingConnector, dispatch.ConnectionPool);
    }

    [TestMethod]
    public void CreateDispatcher_ProductionTransports_HttpHandlerReceivesThePoolingConnector()
    {
        CurlTransports transports = CurlComposition.CreateTransports(NoOptions());

        ProtocolDispatcher dispatcher = CurlComposition.CreateDispatcher(transports);

        IProtocolHandler http = CapturedDependency<Dictionary<string, IProtocolHandler>>(dispatcher)["http"];
        Assert.AreSame(transports.PoolingConnector, ConnectorsOf(http).Single());
    }

    [TestMethod]
    public void CreateTransferDispatch_ProductionTransports_FtpHandlerGetsTheListenerTlsProviderAndResolver()
    {
        CurlTransports transports = CurlComposition.CreateTransports(NoOptions());

        TransferDispatch dispatch = CurlComposition.CreateTransferDispatch(transports);

        Dictionary<string, IProtocolHandler> handlers = CapturedDependency<Dictionary<string, IProtocolHandler>>(dispatch.Dispatcher);
        Assert.AreSame(handlers["ftp"], handlers["ftps"]);
        FtpProtocolHandler ftp = (FtpProtocolHandler)CapturedDependency<IProtocolHandler>(Unwrapped(handlers["ftps"]), "ftpHandler");
        Assert.IsInstanceOfType<TcpConnectionListener>(CapturedDependency<IConnectionListener>(ftp));
        Assert.AreSame(transports.TlsProvider, CapturedDependency<ITlsProvider>(ftp));
        Assert.AreSame(transports.DnsResolver, CapturedDependency<IDnsResolver>(ftp));
        Assert.IsInstanceOfType<SystemNetworkInterfaceLookup>(CapturedDependency<INetworkInterfaceLookup>(ftp));
        Assert.AreSame(transports.PoolingConnector, Unrecorded(CapturedDependency<IConnector>(ftp)));
    }

    [TestMethod]
    public async Task CreateRunner_FtpsUrlWithFakeConnectors_ReachesConnectorAtPort990WithTls()
    {
        RecordingConnector connector = new(CurlExitCode.CouldntConnect, ConnectFailure);

        await RunWithFakeConnectorsAsync("ftps://h/f", connector, new RecordingDatagramConnector(CurlExitCode.CouldntConnect, ConnectFailure));

        ConnectTarget target = connector.Targets.Single();
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

        Assert.AreEqual(0, exitCode);
        Assert.AreEqual("ab", Encoding.Latin1.GetString(standardOutput.ToArray()));
        Assert.AreEqual(new ConnectTarget("h", 18233, false) { PoolScheme = "http" }, server.Targets.Single());
    }

    [TestMethod]
    public void CreateTransferDispatch_CaPath_WarnsAsThePlatformsCurlBuildDoes()
    {
        CurlTransports transports = CurlComposition.CreateTransports(Parse("--capath", ".", "https://example.com/"));

        TransferDispatch dispatch = CurlComposition.CreateTransferDispatch(transports);

        // ADR-0009: the Schannel build ignores --capath with this warning, which the runner
        // wraps at the terminal width; the OpenSSL build honours it and prints nothing.
        string[] expected = OperatingSystem.IsWindows()
            ? ["Warning: ignoring setting the CA path for the proxy, not supported by libcurl with Schannel"]
            : [];
        CollectionAssert.AreEqual(expected, dispatch.WarningLinesBeforeEachTransfer.ToArray());
    }

    [TestMethod]
    public void CreateTransports_CaPathCertKeyAndCiphers_SslStreamTlsProviderReceivesMappedOptions()
    {
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
}
