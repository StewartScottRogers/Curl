using System.Text;
using Curl.Api.Callbacks;
using Curl.Api.Codes;
using Curl.Api.EngineContracts;
using Curl.Api.Options;
using Curl.Api.Options.Network;
using Curl.Networking;
using Curl.Protocol.Abstractions;

namespace Curl.Engine;

// The open-socket, socket-option and close-socket callbacks around a whole transfer's connection (BL-2418, ADR-0610).
public sealed partial class ManagedEasyTransferTests
{
    [TestMethod]
    public async Task PerformAsync_AllThreeSocketCallbacksSet_CallsOpenThenSockoptThenCloseWhenTheEngineClosesThePool()
    {
        List<string> calls = [];
        ManagedCurlEngine engine = CreateSocketCallbackEngine(new ScriptedConnector([Encoding.ASCII.GetBytes(Response)]));
        CurlCode code;
        await using (IEasyTransfer easy = engine.CreateEasy())
        {
            easy.Options = SocketCallbackOptions(calls, new CurlSocket(77), SocketOptionResult.Ok);
            code = await easy.PerformAsync(CancellationToken.None);
            CollectionAssert.AreEqual(new[] { "open IpConnection", "sockopt 77 IpConnection" }, calls);
        }

        await engine.DisposeAsync();

        Assert.AreEqual(CurlCode.Ok, code);
        CollectionAssert.AreEqual(new[] { "open IpConnection", "sockopt 77 IpConnection", "close 77" }, calls);
    }

    [TestMethod]
    public async Task PerformAsync_OpenSocketCallbackReturnsBad_ReturnsCouldntConnectAndCallsNoOtherSocketCallback()
    {
        List<string> calls = [];
        await using ManagedCurlEngine engine = CreateSocketCallbackEngine(new ScriptedConnector([Encoding.ASCII.GetBytes(Response)]));
        await using IEasyTransfer easy = engine.CreateEasy();
        easy.Options = SocketCallbackOptions(calls, CurlSocket.Bad, SocketOptionResult.Ok);

        CurlCode code = await easy.PerformAsync(CancellationToken.None);

        Assert.AreEqual(CurlCode.CouldntConnect, code);
        StringAssert.StartsWith(easy.ErrorText, "Failed to connect to h:80 after ");
        StringAssert.EndsWith(easy.ErrorText, " ms: Could not connect to server");
        CollectionAssert.AreEqual(new[] { "open IpConnection" }, calls);
    }

    [TestMethod]
    public async Task PerformAsync_SockoptCallbackReturnsError_ReturnsAbortedByCallbackAndClosesTheSocket()
    {
        List<string> calls = [];
        await using ManagedCurlEngine engine = CreateSocketCallbackEngine(new ScriptedConnector([Encoding.ASCII.GetBytes(Response)]));
        await using IEasyTransfer easy = engine.CreateEasy();
        easy.Options = SocketCallbackOptions(calls, new CurlSocket(5), SocketOptionResult.Error);

        CurlCode code = await easy.PerformAsync(CancellationToken.None);

        Assert.AreEqual(CurlCode.AbortedByCallback, code);
        StringAssert.EndsWith(easy.ErrorText, " ms: Operation was aborted by an application callback");
        CollectionAssert.AreEqual(new[] { "open IpConnection", "sockopt 5 IpConnection", "close 5" }, calls);
    }

    [TestMethod]
    public void CreateDispatcher_SocketCallbackSet_BuildsTheDispatcherOverASocketCallbackPool()
    {
        ManagedEngineSharedState shared = new(TimeProvider.System, _ => string.Empty);
        EasyOptions options = new() { Callbacks = new() { CloseSocket = _ => 0 } };

        Assert.IsNotNull(ManagedCurlEngine.CreateDispatcher(options, shared, cookieStore: null));
        Assert.IsInstanceOfType<SocketCallbackConnector>(ManagedCurlEngine.CreateConnectOnlyConnector(options, shared));
        Assert.IsInstanceOfType<TcpConnector>(ManagedCurlEngine.CreateConnectOnlyConnector(new EasyOptions(), shared));
    }

    /// <summary>An engine whose handlers connect through <paramref name="connector" /> under each handle's socket callbacks, pooled in the engine's cache as production pools.</summary>
    private static ManagedCurlEngine CreateSocketCallbackEngine(IConnector connector) =>
        new(TimeProvider.System, (options, shared, cookies) => CreateDispatcher(
            new PoolingConnector(SocketCallbackConnector.Over(connector, options.Callbacks, shared), shared.Connections, configuration: null),
            cookies));

    /// <summary>A <c>GET http://h/</c> whose three socket callbacks write what they were called with to <paramref name="calls" />.</summary>
    private static EasyOptions SocketCallbackOptions(List<string> calls, CurlSocket opened, SocketOptionResult sockopt) => new()
    {
        Network = new EasyNetworkOptions { Url = "http://h/" },
        Callbacks = new()
        {
            OpenSocket = (purpose, _) =>
            {
                calls.Add($"open {purpose}");
                return opened;
            },
            SocketOption = (socket, purpose) =>
            {
                calls.Add($"sockopt {socket.Handle} {purpose}");
                return sockopt;
            },
            CloseSocket = socket =>
            {
                calls.Add($"close {socket.Handle}");
                return 0;
            },
        },
    };
}
