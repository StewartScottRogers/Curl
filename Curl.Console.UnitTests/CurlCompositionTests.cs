using System.Reflection;
using System.Text;

using Curl.Cli;
using Curl.Core;
using Curl.Networking;
using Curl.Protocol.Abstractions;
using Curl.Protocol.Dict;
using Curl.Protocol.File;
using Curl.Protocol.Gopher;
using Curl.Protocol.Mqtt;
using Curl.Protocol.Telnet;
using Curl.Protocol.Tftp;

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
    public void CreateProtocolHandlers_ServesEachSchemeThroughItsHandlerOnce()
    {
        IReadOnlyList<IProtocolHandler> handlers = CurlComposition.CreateProtocolHandlers(
            new RecordingConnector(CurlExitCode.CouldntConnect, ConnectFailure),
            new RecordingDatagramConnector(CurlExitCode.CouldntConnect, ConnectFailure));

        Dictionary<string, Type> served = handlers
            .SelectMany(handler => handler.SupportedSchemes.Select(scheme => (scheme, type: handler.GetType())))
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
        };
        CollectionAssert.AreEquivalent(expected.ToList(), served.ToList());
        _ = new ProtocolDispatcher(handlers);
    }

    [TestMethod]
    [DataRow("gophers://h/", 70, true)]
    [DataRow("mqtts://h/", 8883, true)]
    [DataRow("gopher://h/", 70, false)]
    [DataRow("mqtt://h/", 1883, false)]
    [DataRow("dict://h/d:x", 2628, false)]
    [DataRow("telnet://h/", 23, false)]
    public async Task CreateRunner_TcpSchemeUrl_ReachesConnectorAtDefaultPortWithSchemesTls(
        string url,
        int port,
        bool useTls)
    {
        RecordingConnector connector = new(CurlExitCode.CouldntConnect, ConnectFailure);

        await RunWithFakeConnectorsAsync(url, connector, new RecordingDatagramConnector(CurlExitCode.CouldntConnect, ConnectFailure));

        Assert.AreEqual(new ConnectTarget("h", port, useTls), connector.Targets.Single());
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
                string.Concat(ProgressMeterLines.Opening(null).Select(line => line + Environment.NewLine)),
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
    public void CreateTransports_TcpConnector_ReceivesTcpDialerAndSecureSslStreamTlsProvider()
    {
        CurlTransports transports = CurlComposition.CreateTransports(NoOptions());

        Assert.AreSame(transports.TcpDialer, CapturedDependency<ITcpDialer>(transports.TcpConnector));
        Assert.AreSame(transports.TlsProvider, CapturedDependency<ITlsProvider>(transports.TcpConnector));
        Assert.AreSame(transports.TlsClientOptions, CapturedDependency<TlsClientOptions>(transports.TlsProvider));
        Assert.IsFalse(transports.TlsClientOptions.Insecure);
    }

    [TestMethod]
    public void CreateTransports_InsecureCaCertificateAndTlsv13_SslStreamTlsProviderReceivesMappedOptions()
    {
        CommandLineOptions options = Parse("-k", "--cacert", "x.pem", "--tlsv1.3", "gophers://example.com/");

        CurlTransports transports = CurlComposition.CreateTransports(options);

        TlsClientOptions expected = new(Insecure: true, MinimumVersion: TlsMinimumVersion.Tls13, CaCertificateFile: "x.pem");
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
}
