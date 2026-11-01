using System.Reflection;

using Curl.Cli;
using Curl.Console;
using Curl.Core;
using Curl.Networking;
using Curl.Protocol.Abstractions;
using Curl.Original.Protocol.Dict;
using Curl.Original.Protocol.File;
using Curl.Original.Protocol.Ftp;
using Curl.Original.Protocol.Gopher;
using Curl.Original.Protocol.Http;
using Curl.Original.Protocol.Imap;
using Curl.Original.Protocol.Ldap;
using Curl.Original.Protocol.Mqtt;
using Curl.Original.Protocol.Pop3;
using Curl.Original.Protocol.Rtsp;
using Curl.Original.Protocol.Smtp;
using Curl.Original.Protocol.Ssh;
using Curl.Original.Protocol.Telnet;
using Curl.Original.Protocol.Tftp;
using Curl.Original.Protocol.Ws;
using Curl.Testing;

namespace Curl.Engine;

/// <summary>
/// Pins <see cref="EngineComposition" />'s factories: which handlers it registers and how it wires the
/// network transports. No test here opens a socket.
/// </summary>
[TestClass]
public sealed partial class EngineCompositionTests
{
    private const string ConnectFailure = "Failed to connect to h:2628 after 0 ms: Could not connect to server";

    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);
    [TestMethod]
    public void CreateProtocolHandlers_HttpHandlerUsed_BuildsNoOtherHandler()
    {
        IReadOnlyList<EndPointReportingProtocolHandler> handlers = [.. EngineComposition.CreateProtocolHandlers(
            new RecordingConnector(CurlExitCode.CouldntConnect, ConnectFailure),
            new RecordingDatagramConnector(CurlExitCode.CouldntConnect, ConnectFailure),
            new PassThroughTlsProvider(),
            new LoopbackDnsResolver()).Cast<EndPointReportingProtocolHandler>()];
        LazyProtocolHandler http = (LazyProtocolHandler)handlers.Single(handler => handler.SupportedSchemes.Contains("http")).GivenHandler;
        Diagnostics.Arrange("handlers", handlers.Count);

        _ = http.Handler;
        string[] built = [.. handlers.Select(handler => (LazyProtocolHandler)handler.GivenHandler).Where(lazy => lazy.IsCreated).SelectMany(lazy => lazy.SupportedSchemes)];
        Diagnostics.Act("built handlers' schemes", string.Join(",", built));

        Diagnostics.Assert("built handlers' schemes", "http,https", string.Join(",", built));
        CollectionAssert.AreEqual(new[] { "http", "https" }, built);
    }

    [TestMethod]
    public void CreateProtocolHandlers_EachLazyHandler_ReportsTheSchemesItsBuiltHandlerServes()
    {
        IReadOnlyList<EndPointReportingProtocolHandler> handlers = [.. EngineComposition.CreateProtocolHandlers(
            new RecordingConnector(CurlExitCode.CouldntConnect, ConnectFailure),
            new RecordingDatagramConnector(CurlExitCode.CouldntConnect, ConnectFailure),
            new PassThroughTlsProvider(),
            new LoopbackDnsResolver()).Cast<EndPointReportingProtocolHandler>()];
        Diagnostics.Arrange("handlers", handlers.Count);

        string[] lazySchemes = [.. handlers.SelectMany(handler => handler.SupportedSchemes)];
        string[] builtSchemes = [.. handlers.SelectMany(handler => handler.Handler.SupportedSchemes)];
        Diagnostics.Act("lazy schemes", string.Join(",", lazySchemes));

        Diagnostics.Assert("built schemes", string.Join(",", lazySchemes), string.Join(",", builtSchemes));
        CollectionAssert.AreEqual(lazySchemes, builtSchemes);
    }

    [TestMethod]
    public void CreateProtocolHandlers_ServesEachSchemeThroughItsHandlerOnce()
    {
        Diagnostics.Arrange("connect failure scripted into both connectors", ConnectFailure);
        IReadOnlyList<IProtocolHandler> handlers = EngineComposition.CreateProtocolHandlers(
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
            ["smb"] = typeof(DisabledProtocolHandler),
            ["smbs"] = typeof(DisabledProtocolHandler),
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
    public void CreateTransports_BothConnectors_ShareOneSystemDnsResolverAndTimeProviderSystem()
    {
        Diagnostics.Arrange("arguments", "gophers://example.com/");
        CurlTransports transports = EngineComposition.CreateTransports(EasyOptionsFromCommandLine.ToolOptionsOf(NoOptions()), EasyOptionsFromCommandLine.NetworkOf(NoOptions()), EasyOptionsFromCommandLine.TlsOf(NoOptions()), EasyOptionsFromCommandLine.HttpOf(NoOptions()), EasyOptionsFromCommandLine.ProxyOf(NoOptions()), EasyOptionsFromCommandLine.AuthenticationOf(NoOptions()));

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
        CurlTransports transports = EngineComposition.CreateTransports(EasyOptionsFromCommandLine.ToolOptionsOf(NoOptions()), EasyOptionsFromCommandLine.NetworkOf(NoOptions()), EasyOptionsFromCommandLine.TlsOf(NoOptions()), EasyOptionsFromCommandLine.HttpOf(NoOptions()), EasyOptionsFromCommandLine.ProxyOf(NoOptions()), EasyOptionsFromCommandLine.AuthenticationOf(NoOptions()));

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

        CurlTransports transports = EngineComposition.CreateTransports(EasyOptionsFromCommandLine.ToolOptionsOf(NoOptions()), EasyOptionsFromCommandLine.NetworkOf(NoOptions()), EasyOptionsFromCommandLine.TlsOf(NoOptions()), EasyOptionsFromCommandLine.HttpOf(NoOptions()), EasyOptionsFromCommandLine.ProxyOf(NoOptions()), EasyOptionsFromCommandLine.AuthenticationOf(NoOptions()), timeProvider);

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

        var provider = EngineComposition.CreateTlsProvider(options, TimeProvider.System);

        Diagnostics.Act("provider type", provider.GetType().Name);
        Diagnostics.Assert("provider type", expected.Name, provider.GetType().Name);
        Assert.IsInstanceOfType(provider, expected);
        Assert.AreSame(options, CapturedDependency<TlsClientOptions>(provider));
    }

    [TestMethod]
    public void CreateTransports_WithTlsMax10_UpgradesTheOriginWithTheHandBuiltClientAndTheProxyWithSslStream()
    {
        Diagnostics.Arrange("arguments", "--tls-max 1.0 https://example.com/");
        CurlTransports transports = EngineComposition.CreateTransports(EasyOptionsFromCommandLine.ToolOptionsOf(Parse("--tls-max", "1.0", "https://example.com/")), EasyOptionsFromCommandLine.NetworkOf(Parse("--tls-max", "1.0", "https://example.com/")), EasyOptionsFromCommandLine.TlsOf(Parse("--tls-max", "1.0", "https://example.com/")), EasyOptionsFromCommandLine.HttpOf(Parse("--tls-max", "1.0", "https://example.com/")), EasyOptionsFromCommandLine.ProxyOf(Parse("--tls-max", "1.0", "https://example.com/")), EasyOptionsFromCommandLine.AuthenticationOf(Parse("--tls-max", "1.0", "https://example.com/")));

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
        CurlTransports transports = EngineComposition.CreateTransports(EasyOptionsFromCommandLine.ToolOptionsOf(Parse(option, value, "https://example.com/")), EasyOptionsFromCommandLine.NetworkOf(Parse(option, value, "https://example.com/")), EasyOptionsFromCommandLine.TlsOf(Parse(option, value, "https://example.com/")), EasyOptionsFromCommandLine.HttpOf(Parse(option, value, "https://example.com/")), EasyOptionsFromCommandLine.ProxyOf(Parse(option, value, "https://example.com/")), EasyOptionsFromCommandLine.AuthenticationOf(Parse(option, value, "https://example.com/")));

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
        CurlTransports transports = EngineComposition.CreateTransports(EasyOptionsFromCommandLine.ToolOptionsOf(NoOptions()), EasyOptionsFromCommandLine.NetworkOf(NoOptions()), EasyOptionsFromCommandLine.TlsOf(NoOptions()), EasyOptionsFromCommandLine.HttpOf(NoOptions()), EasyOptionsFromCommandLine.ProxyOf(NoOptions()), EasyOptionsFromCommandLine.AuthenticationOf(NoOptions()));

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

        CurlTransports transports = EngineComposition.CreateTransports(EasyOptionsFromCommandLine.ToolOptionsOf(options), EasyOptionsFromCommandLine.NetworkOf(options), EasyOptionsFromCommandLine.TlsOf(options), EasyOptionsFromCommandLine.HttpOf(options), EasyOptionsFromCommandLine.ProxyOf(options), EasyOptionsFromCommandLine.AuthenticationOf(options));

        Diagnostics.Act("proxy tls client options", transports.ProxyTlsClientOptions);
        Diagnostics.Assert("proxy tls client options", new TlsClientOptions(PemPassPhrasePrompt: TerminalPemPassPhrasePrompt.Instance), transports.ProxyTlsClientOptions);
        Assert.AreEqual(new TlsClientOptions(PemPassPhrasePrompt: TerminalPemPassPhrasePrompt.Instance), transports.ProxyTlsClientOptions);
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
        CurlTransports transports = EngineComposition.CreateTransports(EasyOptionsFromCommandLine.ToolOptionsOf(options), EasyOptionsFromCommandLine.NetworkOf(options), EasyOptionsFromCommandLine.TlsOf(options), EasyOptionsFromCommandLine.HttpOf(options), EasyOptionsFromCommandLine.ProxyOf(options), EasyOptionsFromCommandLine.AuthenticationOf(options));

        Diagnostics.Act("proxy tls client options", transports.ProxyTlsClientOptions);
        Diagnostics.Act("target tls client options", transports.TlsClientOptions);
        Diagnostics.Assert(
            "proxy tls client options",
            new TlsClientOptions(Insecure: true, CaCertificateFile: "proxy.pem", CaCertificateDirectory: "proxy-certs", PemPassPhrasePrompt: TerminalPemPassPhrasePrompt.Instance),
            transports.ProxyTlsClientOptions);
        Assert.AreEqual(
            new TlsClientOptions(Insecure: true, CaCertificateFile: "proxy.pem", CaCertificateDirectory: "proxy-certs", PemPassPhrasePrompt: TerminalPemPassPhrasePrompt.Instance),
            transports.ProxyTlsClientOptions);
        Assert.AreEqual(new TlsClientOptions(CaCertificateFile: "x.pem", PemPassPhrasePrompt: TerminalPemPassPhrasePrompt.Instance), transports.TlsClientOptions);
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

        CurlTransports transports = EngineComposition.CreateTransports(EasyOptionsFromCommandLine.ToolOptionsOf(NoOptions()), EasyOptionsFromCommandLine.NetworkOf(NoOptions()), EasyOptionsFromCommandLine.TlsOf(NoOptions()), EasyOptionsFromCommandLine.HttpOf(NoOptions()), EasyOptionsFromCommandLine.ProxyOf(NoOptions()), EasyOptionsFromCommandLine.AuthenticationOf(NoOptions()), timeProvider);

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

        CurlTransports transports = EngineComposition.CreateTransports(EasyOptionsFromCommandLine.ToolOptionsOf(options), EasyOptionsFromCommandLine.NetworkOf(options), EasyOptionsFromCommandLine.TlsOf(options), EasyOptionsFromCommandLine.HttpOf(options), EasyOptionsFromCommandLine.ProxyOf(options), EasyOptionsFromCommandLine.AuthenticationOf(options));

        TlsClientOptions expected = new(Insecure: true, MinimumVersion: TlsVersion.Tls13, CaCertificateFile: "x.pem", PemPassPhrasePrompt: TerminalPemPassPhrasePrompt.Instance);
        Diagnostics.Act("tls client options", transports.TlsClientOptions);
        Diagnostics.Assert("tls client options", expected, transports.TlsClientOptions);
        Assert.AreEqual(expected, transports.TlsClientOptions);
        Assert.AreSame(transports.TlsClientOptions, CapturedDependency<TlsClientOptions>(transports.TlsProvider));
    }

    [TestMethod]
    public void CreateTransports_EachCall_BuildsItsOwnResolver()
    {
        Diagnostics.Arrange("arguments", "gophers://example.com/");
        IDnsResolver first = EngineComposition.CreateTransports(EasyOptionsFromCommandLine.ToolOptionsOf(NoOptions()), EasyOptionsFromCommandLine.NetworkOf(NoOptions()), EasyOptionsFromCommandLine.TlsOf(NoOptions()), EasyOptionsFromCommandLine.HttpOf(NoOptions()), EasyOptionsFromCommandLine.ProxyOf(NoOptions()), EasyOptionsFromCommandLine.AuthenticationOf(NoOptions())).DnsResolver;
        IDnsResolver second = EngineComposition.CreateTransports(EasyOptionsFromCommandLine.ToolOptionsOf(NoOptions()), EasyOptionsFromCommandLine.NetworkOf(NoOptions()), EasyOptionsFromCommandLine.TlsOf(NoOptions()), EasyOptionsFromCommandLine.HttpOf(NoOptions()), EasyOptionsFromCommandLine.ProxyOf(NoOptions()), EasyOptionsFromCommandLine.AuthenticationOf(NoOptions())).DnsResolver;

        Diagnostics.Act("resolvers are the same instance", ReferenceEquals(first, second));
        Diagnostics.Assert("resolvers are the same instance", false, ReferenceEquals(first, second));
        Assert.AreNotSame(first, second);
    }

    [TestMethod]
    [DataRow(new[] { "-Z", "http://example.com/" }, true)]
    [DataRow(new[] { "-Z", "--parallel-immediate", "http://example.com/" }, false)]
    [DataRow(new[] { "http://example.com/" }, false)]
    public void CreateTransports_PoolingConnector_WaitsForMultiplexingUnderParallelWithoutParallelImmediate(string[] arguments, bool waits)
    {
        Diagnostics.Arrange("arguments", string.Join(" ", arguments));
        CurlTransports transports = EngineComposition.CreateTransports(EasyOptionsFromCommandLine.ToolOptionsOf(Parse(arguments)), EasyOptionsFromCommandLine.NetworkOf(Parse(arguments)), EasyOptionsFromCommandLine.TlsOf(Parse(arguments)), EasyOptionsFromCommandLine.HttpOf(Parse(arguments)), EasyOptionsFromCommandLine.ProxyOf(Parse(arguments)), EasyOptionsFromCommandLine.AuthenticationOf(Parse(arguments)));

        Diagnostics.Act("pooling connector waits for multiplexing", transports.PoolingConnector.WaitsForMultiplexing);
        Diagnostics.Assert("pooling connector waits for multiplexing", waits, transports.PoolingConnector.WaitsForMultiplexing);
        Assert.AreEqual(waits, transports.PoolingConnector.WaitsForMultiplexing);
    }

    [TestMethod]
    public void CreateTransports_PoolingConnector_WrapsTheTcpConnectorOnTheSameClock()
    {
        TimeProvider timeProvider = new ReplacementTimeProvider();
        Diagnostics.Arrange("time provider", timeProvider.GetType().Name);

        CurlTransports transports = EngineComposition.CreateTransports(EasyOptionsFromCommandLine.ToolOptionsOf(NoOptions()), EasyOptionsFromCommandLine.NetworkOf(NoOptions()), EasyOptionsFromCommandLine.TlsOf(NoOptions()), EasyOptionsFromCommandLine.HttpOf(NoOptions()), EasyOptionsFromCommandLine.ProxyOf(NoOptions()), EasyOptionsFromCommandLine.AuthenticationOf(NoOptions()), timeProvider);

        bool wrapsTcpConnector = ReferenceEquals(transports.TcpConnector, CapturedDependency<IConnector>(transports.PoolingConnector));
        Diagnostics.Act("pooling connector wraps the tcp connector", wrapsTcpConnector);
        Diagnostics.Assert("pooling connector wraps the tcp connector", true, wrapsTcpConnector);
        Assert.AreSame(transports.TcpConnector, CapturedDependency<IConnector>(transports.PoolingConnector));
        Assert.AreSame(timeProvider, CapturedDependency<TimeProvider>(CapturedDependency<ConnectionCache>(transports.PoolingConnector)));
    }

    [TestMethod]
    public void CreateTransports_CaPathCertKeyAndCiphers_SslStreamTlsProviderReceivesMappedOptions()
    {
        Diagnostics.Arrange("arguments", "--capath certs --cert c.p12:pw --key k.pem --ciphers AES128-SHA --tls13-ciphers TLS_AES_128_GCM_SHA256 https://example.com/");
        CommandLineOptions options = Parse(
            "--capath", "certs", "--cert", "c.p12:pw", "--key", "k.pem",
            "--ciphers", "AES128-SHA", "--tls13-ciphers", "TLS_AES_128_GCM_SHA256", "https://example.com/");

        CurlTransports transports = EngineComposition.CreateTransports(EasyOptionsFromCommandLine.ToolOptionsOf(options), EasyOptionsFromCommandLine.NetworkOf(options), EasyOptionsFromCommandLine.TlsOf(options), EasyOptionsFromCommandLine.HttpOf(options), EasyOptionsFromCommandLine.ProxyOf(options), EasyOptionsFromCommandLine.AuthenticationOf(options));

        TlsClientOptions expected = new(
            CaCertificateDirectory: "certs",
            ClientCertificate: "c.p12",
            Passphrase: "pw",
            PrivateKey: "k.pem",
            Ciphers: "AES128-SHA",
            Tls13Ciphers: "TLS_AES_128_GCM_SHA256",
            PemPassPhrasePrompt: TerminalPemPassPhrasePrompt.Instance);
        Diagnostics.Act("tls client options", transports.TlsClientOptions);
        Diagnostics.Assert("tls client options", expected, transports.TlsClientOptions);
        Assert.AreEqual(expected, transports.TlsClientOptions);
        Assert.AreSame(transports.TlsClientOptions, CapturedDependency<TlsClientOptions>(transports.TlsProvider));
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

    private sealed class ReplacementTimeProvider : TimeProvider;
}
