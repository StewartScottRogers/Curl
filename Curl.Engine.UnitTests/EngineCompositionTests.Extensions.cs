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

    private sealed class ExtensionSchemeHandler(string scheme) : IProtocolHandler
    {
        public IReadOnlyCollection<string> SupportedSchemes { get; } = [scheme];

        public ValueTask<TransferResult> ExecuteAsync(ITransferContext context) => throw new NotSupportedException();
    }
}
