using Curl.Cli;
using Curl.Console;
using Curl.Networking;
using Curl.Protocol.Abstractions;

namespace Curl.Engine;

/// <summary>
/// Pins that QUIC opens its channels through an activated extension connection hook's datagram connector on the
/// real network, and that without one, or with one that keeps curl's, the socket QUIC dialer is built as before (BL-4318).
/// </summary>
public sealed partial class EngineCompositionTests
{
    [TestMethod]
    public async Task CreateTransports_WithAHookDatagramConnector_OpensTheHttp3OnlyQuicChannelThroughIt()
    {
        RecordingDatagramConnector hookConnector = new(CurlExitCode.CouldntConnect, ConnectFailure);
        IDatagramConnector? givenToHook = null;
        CurlTransports transports = TransportsOf(["--http3-only", "https://127.0.0.1:47261/"], curl => { givenToHook = curl; return hookConnector; });
        Diagnostics.Arrange("URL", "https://127.0.0.1:47261/");

        MultiplexedConnectResult result = await transports.TcpConnector.ConnectMultiplexedAsync(new ConnectTarget("127.0.0.1", 47261, true), TestContext.CancellationToken);
        Diagnostics.Act("opens through the hook", string.Join(",", hookConnector.Opens));

        Diagnostics.Assert("opens through the hook", "(127.0.0.1, 47261)", string.Join(",", hookConnector.Opens));
        Assert.IsNull(result.Connection);
        CollectionAssert.AreEqual(new[] { ("127.0.0.1", 47261) }, hookConnector.Opens);
        Assert.AreSame(transports.UdpDatagramConnector, givenToHook);
        Assert.AreNotSame(hookConnector, transports.UdpDatagramConnector);
    }

    [TestMethod]
    public void QuicDatagramConnectorOf_WithoutAHook_IsNullSoQuicKeepsItsSockets()
    {
        RecordingDatagramConnector curl = new(CurlExitCode.CouldntConnect, ConnectFailure);
        Diagnostics.Arrange("hook", "none");

        IDatagramConnector? quic = EngineComposition.QuicDatagramConnectorOf(curl, null);
        Diagnostics.Act("QUIC datagram connector", quic);

        Diagnostics.Assert("QUIC datagram connector", null, quic);
        Assert.IsNull(quic);
    }

    [TestMethod]
    public void QuicDatagramConnectorOf_WithAHookThatKeepsCurls_IsNullSoQuicKeepsItsSockets()
    {
        RecordingDatagramConnector curl = new(CurlExitCode.CouldntConnect, ConnectFailure);
        Diagnostics.Arrange("hook", "keeps curl's");

        IDatagramConnector? quic = EngineComposition.QuicDatagramConnectorOf(curl, given => given);
        Diagnostics.Act("QUIC datagram connector", quic);

        Diagnostics.Assert("QUIC datagram connector", null, quic);
        Assert.IsNull(quic);
    }

    [TestMethod]
    public void QuicDatagramConnectorOf_WithAHookThatReplacesCurls_IsTheHooks()
    {
        RecordingDatagramConnector curl = new(CurlExitCode.CouldntConnect, ConnectFailure);
        RecordingDatagramConnector hook = new(CurlExitCode.CouldntConnect, ConnectFailure);
        Diagnostics.Arrange("hook", "replaces curl's");

        IDatagramConnector? quic = EngineComposition.QuicDatagramConnectorOf(curl, _ => hook);
        Diagnostics.Act("QUIC datagram connector is the hook's", ReferenceEquals(hook, quic));

        Diagnostics.Assert("QUIC datagram connector is the hook's", true, ReferenceEquals(hook, quic));
        Assert.AreSame(hook, quic);
    }

    [TestMethod]
    public void CreateTransports_WithAHookThatKeepsCurls_KeepsCurlsDatagramConnector()
    {
        IDatagramConnector? givenToHook = null;
        Diagnostics.Arrange("hook", "keeps curl's");

        CurlTransports transports = TransportsOf(["--http3-only", "https://127.0.0.1:47261/"], curl => givenToHook = curl);
        Diagnostics.Act("hook given the transports' connector", ReferenceEquals(givenToHook, transports.UdpDatagramConnector));

        Diagnostics.Assert("hook given the transports' connector", true, ReferenceEquals(givenToHook, transports.UdpDatagramConnector));
        Assert.AreSame(transports.UdpDatagramConnector, givenToHook);
        Assert.IsInstanceOfType<UdpDatagramConnector>(transports.UdpDatagramConnector);
    }

    [TestMethod]
    public async Task CreateTransports_UnderDialingWithAHookDatagramConnector_OpensTheQuicChannelThroughTheHook()
    {
        RecordingDatagramConnector dialingConnector = new(CurlExitCode.CouldntConnect, ConnectFailure);
        RecordingDatagramConnector hookConnector = new(CurlExitCode.CouldntConnect, ConnectFailure);
        Diagnostics.Arrange("URL", "https://127.0.0.1:47261/");

        CurlTransports transports = TransportsOf(["--http3-only", "https://127.0.0.1:47261/"], _ => hookConnector, new EngineDialing(new TcpDialer(), new LoopbackDnsResolver(), dialingConnector));
        await transports.TcpConnector.ConnectMultiplexedAsync(new ConnectTarget("127.0.0.1", 47261, true), TestContext.CancellationToken);
        Diagnostics.Act("opens through the hook", $"{hookConnector.Opens.Count},{dialingConnector.Opens.Count}");

        Diagnostics.Assert("opens through the hook", "1,0", $"{hookConnector.Opens.Count},{dialingConnector.Opens.Count}");
        Assert.AreSame(dialingConnector, transports.UdpDatagramConnector);
        Assert.HasCount(1, hookConnector.Opens);
        Assert.IsEmpty(dialingConnector.Opens);
    }

    [TestMethod]
    public async Task CreateTransports_UnderDialingWithoutAHookOrWithOneThatKeepsCurls_OpensTheQuicChannelThroughTheDialingConnector()
    {
        foreach (Func<IDatagramConnector, IDatagramConnector>? hook in new Func<IDatagramConnector, IDatagramConnector>?[] { null, given => given })
        {
            RecordingDatagramConnector dialingConnector = new(CurlExitCode.CouldntConnect, ConnectFailure);
            Diagnostics.Arrange("hook", hook is null ? "none" : "keeps curl's");

            CurlTransports transports = TransportsOf(["--http3-only", "https://127.0.0.1:47261/"], hook, new EngineDialing(new TcpDialer(), new LoopbackDnsResolver(), dialingConnector));
            await transports.TcpConnector.ConnectMultiplexedAsync(new ConnectTarget("127.0.0.1", 47261, true), TestContext.CancellationToken);
            Diagnostics.Act("opens through the dialing connector", dialingConnector.Opens.Count);

            Diagnostics.Assert("opens through the dialing connector", 1, dialingConnector.Opens.Count);
            Assert.AreSame(dialingConnector, transports.UdpDatagramConnector);
            Assert.HasCount(1, dialingConnector.Opens);
        }
    }

    private static CurlTransports TransportsOf(string[] arguments, Func<IDatagramConnector, IDatagramConnector>? extensionDatagramConnector, EngineDialing? dialing = null)
    {
        CommandLineOptions options = CommandLineParser.Parse(arguments, _ => true, new EmptyPasswordPrompt(), DiskDataFileReader.ForProcess, new DefaultConfigFileSearch(_ => null, isWindows: false, null, null), isWindows: false, _ => null, actsAsQuicBuild: true).Options!;
        return EngineComposition.CreateTransports(
            EasyOptionsFromCommandLine.ToolOptionsOf(options),
            EasyOptionsFromCommandLine.NetworkOf(options),
            EasyOptionsFromCommandLine.TlsOf(options),
            EasyOptionsFromCommandLine.HttpOf(options),
            EasyOptionsFromCommandLine.ProxyOf(options),
            EasyOptionsFromCommandLine.AuthenticationOf(options),
            TimeProvider.System,
            null,
            null,
            null,
            null,
            null,
            null,
            static resolver => resolver,
            dialing,
            extensionDatagramConnector: extensionDatagramConnector);
    }
}
