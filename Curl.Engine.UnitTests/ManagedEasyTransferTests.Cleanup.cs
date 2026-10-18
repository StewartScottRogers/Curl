using System.Text;
using Curl.Api.Codes;
using Curl.Api.EngineContracts;
using Curl.Api.EngineContracts.Multi;
using Curl.Api.Options;
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
        await using IEasyTransfer second = engine.CreateEasy();
        second.Options = options;
        CurlCode code = await second.PerformAsync(CancellationToken.None);

        Assert.AreEqual(CurlCode.Ok, code);
        Assert.HasCount(1, connector.Targets);
    }

    [TestMethod]
    public async Task DisposeAsync_HandleInAMultiHandle_LeavesThePooledConnectionForTheNextHandle()
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
        CurlCode code = await second.PerformAsync(CancellationToken.None);

        Assert.AreEqual(CurlCode.Ok, code);
        Assert.HasCount(1, connector.Targets);
    }

    private static ManagedCurlEngine CreatePoolingEngine(IConnector connector) =>
        new(TimeProvider.System, (_, shared, cookies) => new ProtocolDispatcher(EngineComposition.CreateProtocolHandlers(
            new PoolingConnector(connector, shared.Connections, configuration: null),
            new RecordingDatagramConnector(CurlExitCode.CouldntConnect, ConnectFailure),
            new PassThroughTlsProvider(),
            new LoopbackDnsResolver(),
            cookies)), readEnvironmentVariable: _ => null);
}
