using System.Text;
using Curl.Api.Codes;
using Curl.Api.EngineContracts;
using Curl.Api.EngineContracts.Multi;
using Curl.Api.Options;
using Curl.Api.Options.Http;
using Curl.Api.Options.Network;
using Curl.Core;
using Curl.Networking;
using Curl.Protocol.Abstractions;

namespace Curl.Engine;

public sealed partial class ManagedEasyTransferTests
{
    [TestMethod]
    public async Task DisposeAsync_AfterAPerform_ClosesTheHandlesPooledConnectionSoTheNextHandleOpensItsOwn()
    {
        ScriptedConnector connector = new([Encoding.ASCII.GetBytes(Response), Encoding.ASCII.GetBytes(Response)]);
        await using ManagedCurlEngine engine = CreatePoolingEngine(connector);
        EasyOptions options = new() { Network = new EasyNetworkOptions { Url = "http://h/x" } };
        IEasyTransfer first = engine.CreateEasy();
        first.Options = options;
        await first.PerformAsync(CancellationToken.None);

        await first.DisposeAsync();
        await using IEasyTransfer second = engine.CreateEasy();
        second.Options = options;
        CurlCode code = await second.PerformAsync(CancellationToken.None);

        Assert.AreEqual(CurlCode.Ok, code);
        Assert.HasCount(2, connector.Targets);
    }

    [TestMethod]
    public async Task DisposeAsync_ToolSettingsSet_LeavesThePooledConnectionForTheNextHandle()
    {
        ScriptedConnector connector = new([Encoding.ASCII.GetBytes(Response), Encoding.ASCII.GetBytes(Response)]);
        await using ManagedCurlEngine engine = CreatePoolingEngine(connector);
        EasyOptions options = new() { Network = new EasyNetworkOptions { Url = "http://h/x" } };
        ManagedEasyTransfer first = (ManagedEasyTransfer)engine.CreateEasy();
        first.Options = options;
        first.ToolSettings = ManagedEasyTransfer.TransferSettingsOf(options);
        await first.PerformAsync(CancellationToken.None);

        await first.DisposeAsync();
        await using ManagedEasyTransfer second = (ManagedEasyTransfer)engine.CreateEasy();
        second.Options = options;
        second.ToolSettings = ManagedEasyTransfer.TransferSettingsOf(options);
        CurlCode code = await second.PerformAsync(CancellationToken.None);

        Assert.AreEqual(CurlCode.Ok, code);
        Assert.HasCount(1, connector.Targets);
    }

    [TestMethod]
    public async Task DisposeAsync_HandleInAMultiHandle_LeavesThePooledConnectionForTheNextHandleInTheMulti()
    {
        ScriptedConnector connector = new([Encoding.ASCII.GetBytes(Response), Encoding.ASCII.GetBytes(Response)]);
        await using ManagedCurlEngine engine = CreatePoolingEngine(connector);
        EasyOptions options = new() { Network = new EasyNetworkOptions { Url = "http://h/x" } };
        IEasyTransfer first = engine.CreateEasy();
        first.Options = options;
        await using IMultiTransfer multi = engine.CreateMulti();
        multi.Add(first);
        await first.PerformAsync(CancellationToken.None);
        multi.Remove(first);

        await first.DisposeAsync();
        await using IEasyTransfer second = engine.CreateEasy();
        second.Options = options;
        multi.Add(second);
        CurlCode code = await second.PerformAsync(CancellationToken.None);
        multi.Remove(second);

        Assert.AreEqual(CurlCode.Ok, code);
        Assert.HasCount(1, connector.Targets);
    }

    private const string LearnsAltSvc = "HTTP/1.1 200 OK\r\nAlt-Svc: h2=\"alt:443\"; ma=3600; persist=1\r\nContent-Length: 0\r\n\r\n";

    [TestMethod]
    public async Task DisposeAsync_AfterResetAndAPerformThatLearnedAltSvc_SavesTheEntryToTheAltSvcFile()
    {
        MemoryFileSystem files = new();
        ScriptedConnector connector = new([Encoding.ASCII.GetBytes(LearnsAltSvc)]);
        ManagedEasyTransfer easy = (ManagedEasyTransfer)CreateEngine(connector).CreateEasy();
        easy.Options = new EasyOptions { Network = new EasyNetworkOptions { Url = "http://h/x" }, Http = new EasyHttpOptions { AltSvc = "altsvc.txt" } };
        easy.Host = HostOf(files) with { AltSvcUsesOverHttp = true };
        await easy.PerformAsync(CancellationToken.None);
        easy.Reset();

        await easy.DisposeAsync();

        StringAssert.Contains(Encoding.ASCII.GetString(files.Files["altsvc.txt"]), "h1 h 80 h2 alt 443");
    }

    [TestMethod]
    public async Task DisposeAsync_AfterTwoPerformsOnOneHandle_SavesTheEntryItKeptFromTheFirst()
    {
        MemoryFileSystem files = new();
        ScriptedConnector connector = new([Encoding.ASCII.GetBytes(LearnsAltSvc), Encoding.ASCII.GetBytes(Response)]);
        ManagedEasyTransfer easy = (ManagedEasyTransfer)CreateEngine(connector).CreateEasy();
        easy.Options = new EasyOptions { Network = new EasyNetworkOptions { Url = "http://h/x" }, Http = new EasyHttpOptions { AltSvc = "altsvc.txt" } };
        easy.Host = HostOf(files) with { AltSvcUsesOverHttp = true };
        await easy.PerformAsync(CancellationToken.None);
        await easy.PerformAsync(CancellationToken.None);

        await easy.DisposeAsync();

        StringAssert.Contains(Encoding.ASCII.GetString(files.Files["altsvc.txt"]), "h1 h 80 h2 alt 443");
    }

    [TestMethod]
    public async Task PerformAsync_AltSvcOption_WritesNoAltSvcFileUntilTheHandleIsCleanedUp()
    {
        MemoryFileSystem files = new();
        ScriptedConnector connector = new([Encoding.ASCII.GetBytes(LearnsAltSvc)]);
        await using ManagedEasyTransfer easy = (ManagedEasyTransfer)CreateEngine(connector).CreateEasy();
        easy.Options = new EasyOptions { Network = new EasyNetworkOptions { Url = "http://h/x" }, Http = new EasyHttpOptions { AltSvc = "altsvc.txt" } };
        easy.Host = HostOf(files) with { AltSvcUsesOverHttp = true };

        await easy.PerformAsync(CancellationToken.None);

        Assert.IsFalse(files.Files.ContainsKey("altsvc.txt"));
    }

    [TestMethod]
    public async Task CreateEasy_EngineWhoseEnvironmentSetsCurlAltSvcHttp_GivesAHandleHostThatServesAltSvcOverHttp()
    {
        await using ManagedCurlEngine engine = new(TimeProvider.System, createDispatcher: null, readEnvironmentVariable: name => name == "CURL_ALTSVC_HTTP" ? "yeah" : null);
        await using ManagedEasyTransfer easy = (ManagedEasyTransfer)engine.CreateEasy();

        Assert.IsTrue(easy.Host.AltSvcUsesOverHttp);
        Assert.AreSame(EasyTransferHost.Library.StandardInput, easy.Host.StandardInput);
    }

    private static ManagedCurlEngine CreatePoolingEngine(IConnector connector) =>
        new(TimeProvider.System, (_, shared, cookies) => new ProtocolDispatcher(EngineComposition.CreateProtocolHandlers(
            new PoolingConnector(connector, shared.Connections, configuration: null),
            new RecordingDatagramConnector(CurlExitCode.CouldntConnect, ConnectFailure),
            new PassThroughTlsProvider(),
            new LoopbackDnsResolver(),
            cookies)), readEnvironmentVariable: _ => null);
}
