using Curl.Core;
using Curl.Networking;
using Curl.Protocol.Abstractions;
using Curl.Testing;

namespace Curl.Engine;

/// <summary>
/// Pins how <see cref="EngineComposition.CreateProtocolHandlers" /> takes a host's extension handlers (ADR-0999):
/// after curl's own, over the engine's recording transports, and never in place of one of curl's.
/// </summary>
public sealed partial class EngineCompositionTests
{
    [TestMethod]
    public void CreateProtocolHandlers_WithExtensionHandlers_PutsThemAfterCurlsOverTheRecordingTransports()
    {
        PassThroughTlsProvider tlsProvider = new();
        LoopbackDnsResolver dnsResolver = new();
        ProtocolHandlerServices? given = null;
        ExtensionSchemeHandler extension = new("example-extension");
        Diagnostics.Arrange("extension scheme", "example-extension");

        IReadOnlyList<EndPointReportingProtocolHandler> handlers = [.. EngineComposition.CreateProtocolHandlers(
            new RecordingConnector(CurlExitCode.CouldntConnect, ConnectFailure),
            new RecordingDatagramConnector(CurlExitCode.CouldntConnect, ConnectFailure),
            tlsProvider,
            dnsResolver,
            extensionHandlers: services => { given = services; return [extension]; }).Cast<EndPointReportingProtocolHandler>()];
        Diagnostics.Act("last handler's schemes", string.Join(",", handlers[^1].SupportedSchemes));

        Assert.AreSame(extension, handlers[^1].GivenHandler);
        Assert.IsNotNull(given);
        Assert.IsInstanceOfType<EndPointRecordingConnector>(given.Connector);
        Assert.IsInstanceOfType<EndPointRecordingDatagramConnector>(given.DatagramConnector);
        Assert.AreSame(tlsProvider, given.TlsProvider);
        Assert.AreSame(dnsResolver, given.DnsResolver);
    }

    [TestMethod]
    public void CreateProtocolHandlers_WithAnExtensionClaimingACurlScheme_FailsTheDispatcher()
    {
        Diagnostics.Arrange("extension scheme", "http");
        IReadOnlyList<IProtocolHandler> handlers = EngineComposition.CreateProtocolHandlers(
            new RecordingConnector(CurlExitCode.CouldntConnect, ConnectFailure),
            new RecordingDatagramConnector(CurlExitCode.CouldntConnect, ConnectFailure),
            new PassThroughTlsProvider(),
            new LoopbackDnsResolver(),
            extensionHandlers: _ => [new ExtensionSchemeHandler("http")]);

        ArgumentException failure = Assert.ThrowsExactly<ArgumentException>(() => new ProtocolDispatcher(handlers));
        Diagnostics.Act("failure", failure.Message);

        StringAssert.Contains(failure.Message, "\"http\"");
    }

    [TestMethod]
    public void CreateProtocolHandlers_WithoutExtensionTransferHooks_RunsEveryHandlerWithoutAHook()
    {
        Diagnostics.Arrange("extension transfer hooks", "none");
        IReadOnlyList<EndPointReportingProtocolHandler> handlers = [.. EngineComposition.CreateProtocolHandlers(
            new RecordingConnector(CurlExitCode.CouldntConnect, ConnectFailure),
            new RecordingDatagramConnector(CurlExitCode.CouldntConnect, ConnectFailure),
            new PassThroughTlsProvider(),
            new LoopbackDnsResolver()).Cast<EndPointReportingProtocolHandler>()];
        Diagnostics.Act("handlers", handlers.Count.ToString(System.Globalization.CultureInfo.InvariantCulture));

        Assert.IsFalse(handlers.Any(handler => handler.GivenHandler is HookedHandler));
    }

    [TestMethod]
    public void CreateProtocolHandlers_WithExtensionTransferHooks_PutsEveryHandlerThroughThem()
    {
        Diagnostics.Arrange("extension transfer hooks", "one");
        IReadOnlyList<EndPointReportingProtocolHandler> handlers = [.. EngineComposition.CreateProtocolHandlers(
            new RecordingConnector(CurlExitCode.CouldntConnect, ConnectFailure),
            new RecordingDatagramConnector(CurlExitCode.CouldntConnect, ConnectFailure),
            new PassThroughTlsProvider(),
            new LoopbackDnsResolver(),
            extensionHandlers: _ => [new ExtensionSchemeHandler("example-extension")],
            extensionTransferHooks: handler => new HookedHandler(handler)).Cast<EndPointReportingProtocolHandler>()];
        Diagnostics.Act("last handler's schemes", string.Join(",", handlers[^1].SupportedSchemes));

        Assert.IsTrue(handlers.All(handler => handler.GivenHandler is HookedHandler { Inner: not HookedHandler }));
        Assert.IsInstanceOfType<ExtensionSchemeHandler>(((HookedHandler)handlers[^1].GivenHandler).Inner);
    }

    [TestMethod]
    public async Task CreateProtocolHandlers_WithAnExtensionConnector_CarriesAnHttpTransferOverIt()
    {
        RecordingConnector curls = new(CurlExitCode.CouldntConnect, ConnectFailure);
        ScriptedConnector extensions = new([System.Text.Encoding.ASCII.GetBytes("HTTP/1.1 200 OK\r\nContent-Length: 5\r\n\r\nhello")]);
        IConnector? given = null;
        MemoryStream output = new();
        Diagnostics.Arrange("extension connector", "scripted");
        ProtocolDispatcher dispatcher = new(EngineComposition.CreateProtocolHandlers(
            curls,
            new RecordingDatagramConnector(CurlExitCode.CouldntConnect, ConnectFailure),
            new PassThroughTlsProvider(),
            new LoopbackDnsResolver(),
            extensionConnector: connector => { given = connector; return extensions; }));

        TransferResult result = await dispatcher.DispatchAsync(new TransferContext { Url = CurlUrl.Parse("http://example.com/"), Output = output });
        Diagnostics.Act("exit code", result.ExitCode.ToString());

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual("hello", System.Text.Encoding.ASCII.GetString(output.ToArray()));
        Assert.AreEqual("example.com", extensions.Targets.Single().Host);
        Assert.AreSame(curls, given);
        Assert.IsEmpty(curls.Targets);
    }

    [TestMethod]
    public async Task CreateProtocolHandlers_WithoutAnExtensionConnector_CarriesAnHttpTransferOverCurls()
    {
        ScriptedConnector curls = new([System.Text.Encoding.ASCII.GetBytes("HTTP/1.1 200 OK\r\nContent-Length: 5\r\n\r\nhello")]);
        MemoryStream output = new();
        Diagnostics.Arrange("extension connector", "none");
        ProtocolDispatcher dispatcher = new(EngineComposition.CreateProtocolHandlers(
            curls,
            new RecordingDatagramConnector(CurlExitCode.CouldntConnect, ConnectFailure),
            new PassThroughTlsProvider(),
            new LoopbackDnsResolver()));

        TransferResult result = await dispatcher.DispatchAsync(new TransferContext { Url = CurlUrl.Parse("http://example.com/"), Output = output });
        Diagnostics.Act("exit code", result.ExitCode.ToString());

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual("hello", System.Text.Encoding.ASCII.GetString(output.ToArray()));
        Assert.AreEqual("example.com", curls.Targets.Single().Host);
    }

    [TestMethod]
    public void CreateProtocolHandlers_WithAnExtensionDatagramConnector_GivesItToTheExtensionHandlers()
    {
        RecordingDatagramConnector curls = new(CurlExitCode.CouldntConnect, ConnectFailure);
        RecordingDatagramConnector extensions = new(CurlExitCode.CouldntConnect, ConnectFailure);
        IDatagramConnector? given = null;
        ProtocolHandlerServices? services = null;
        Diagnostics.Arrange("extension datagram connector", "recording");

        _ = EngineComposition.CreateProtocolHandlers(
            new RecordingConnector(CurlExitCode.CouldntConnect, ConnectFailure),
            curls,
            new PassThroughTlsProvider(),
            new LoopbackDnsResolver(),
            extensionHandlers: handlerServices => { services = handlerServices; return []; },
            extensionDatagramConnector: datagramConnector => { given = datagramConnector; return extensions; });
        Diagnostics.Act("given", given?.GetType().Name ?? "none");

        Assert.AreSame(curls, given);
        Assert.IsInstanceOfType<EndPointRecordingDatagramConnector>(services!.DatagramConnector);
    }

    private sealed class HookedHandler(IProtocolHandler inner) : IProtocolHandler
    {
        public IProtocolHandler Inner => inner;

        public IReadOnlyCollection<string> SupportedSchemes => inner.SupportedSchemes;

        public ValueTask<TransferResult> ExecuteAsync(ITransferContext context) => inner.ExecuteAsync(context);
    }

    private sealed class ExtensionSchemeHandler(string scheme) : IProtocolHandler
    {
        public IReadOnlyCollection<string> SupportedSchemes { get; } = [scheme];

        public ValueTask<TransferResult> ExecuteAsync(ITransferContext context) => throw new NotSupportedException();
    }
}
