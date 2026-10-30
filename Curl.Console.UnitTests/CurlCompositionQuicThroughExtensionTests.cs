using Curl.Cli;
using Curl.Engine;
using Curl.Extension.Abstractions;
using Curl.Extension.Abstractions.Connection;
using Curl.Extension.Abstractions.Resolution;
using Curl.Networking;
using Curl.Protocol.Abstractions;

namespace Curl.Console;

/// <summary>
/// Pins that the executable's transports open an <c>--http3-only</c> transfer's QUIC channel through the datagram
/// connector an activated extension connection hook supplies, not a socket of the QUIC dialer's own (BL-4318).
/// </summary>
[TestClass]
public sealed class CurlCompositionQuicThroughExtensionTests
{
    private static readonly ExtensionOption HookOption = new("test-datagrams", ExtensionOptionArgument.None, "Test datagrams");

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task CreateNetworkTransports_WithAHookDatagramConnector_OpensTheHttp3OnlyQuicChannelThroughIt()
    {
        RefusingDatagramConnector hookConnector = new();
        ConnectionHooks hooks = ConnectionHooks.ActivatedBy(new ExtensionOptionValues(new Dictionary<string, string>()), [_ => new DatagramConnectorReplacingHook(hookConnector)]);
        CurlTransports transports = TransportsOf(hooks);

        MultiplexedConnectResult result = await transports.TcpConnector.ConnectMultiplexedAsync(new ConnectTarget("127.0.0.1", 47262, true), TestContext.CancellationToken);

        Assert.IsNull(result.Connection);
        CollectionAssert.AreEqual(new[] { "127.0.0.1:47262" }, hookConnector.Opened);
        Assert.IsInstanceOfType<UdpDatagramConnector>(transports.UdpDatagramConnector);
    }

    [TestMethod]
    public void CreateNetworkTransports_WithoutAHook_KeepsCurlsDatagramConnector()
    {
        CurlTransports transports = TransportsOf(ConnectionHooks.None);

        Assert.IsInstanceOfType<UdpDatagramConnector>(transports.UdpDatagramConnector);
    }

    private static CurlTransports TransportsOf(ConnectionHooks hooks)
    {
        CommandLineOptions options = QuicBuildParser.Parse(["--http3-only", "https://127.0.0.1:47262/"], _ => true).Options!;
        return CurlComposition.CreateNetworkTransports(options, ResolverHooks.None, hooks, new ConnectionCache(TimeProvider.System), new TlsSessionCache(TimeProvider.System), new LateBoundDiagnosticLog(), new DnsCache(), new TransferPerformer(TimeProvider.System, _ => string.Empty));
    }

    /// <summary>A hook that replaces curl's datagram connector and leaves its stream connector alone.</summary>
    private sealed class DatagramConnectorReplacingHook(IDatagramConnector datagramConnector) : IConnectionHook
    {
        public ExtensionOption Option => HookOption;

        public IConnector ConnectorOver(IConnector curlConnector) => curlConnector;

        public IDatagramConnector DatagramConnectorOver(IDatagramConnector curlDatagramConnector) => datagramConnector;
    }

    /// <summary>A datagram connector that records each open and refuses it.</summary>
    private sealed class RefusingDatagramConnector : IDatagramConnector
    {
        public List<string> Opened { get; } = [];

        public ValueTask<DatagramOpenResult> OpenAsync(string host, int port, CancellationToken cancellationToken)
        {
            lock (Opened)
            {
                Opened.Add($"{host}:{port}");
            }

            return ValueTask.FromResult(DatagramOpenResult.Failed(CurlExitCode.CouldntConnect, "Could not connect to server"));
        }
    }
}
