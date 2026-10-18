using System.Text;
using Curl.Api.Callbacks;
using Curl.Api.Codes;
using Curl.Api.EngineContracts;
using Curl.Api.Options;
using Curl.Api.Options.Http;
using Curl.Api.Options.Network;
using Curl.Networking;
using Curl.Protocol.Abstractions;

namespace Curl.Engine;

// The prerequest and resolver-start callbacks through a whole transfer (BL-2419, ADR-0611).
public sealed partial class ManagedEasyTransferTests
{
    [TestMethod]
    public async Task PerformAsync_PrerequestCallbackAnswersOk_CallsItOncePerRequestAndCompletes()
    {
        List<string> calls = [];
        string redirect = "HTTP/1.1 301 Moved\r\nLocation: /next\r\nContent-Length: 0\r\n\r\n";
        ScriptedConnector connector = new([Encoding.ASCII.GetBytes(redirect), Encoding.ASCII.GetBytes(Response)]) { ReusesTheFirstConnection = true };
        await using ManagedCurlEngine engine = CreatePrerequestEngine(connector);
        await using IEasyTransfer easy = engine.CreateEasy();
        easy.Options = PrerequestOptions(calls, PrerequestResult.Ok) with { Http = new EasyHttpOptions { FollowLocation = FollowLocation.All } };

        CurlCode code = await easy.PerformAsync(CancellationToken.None);

        Assert.AreEqual(CurlCode.Ok, code);
        CollectionAssert.AreEqual(new[] { "prereq", "prereq" }, calls);
    }

    [TestMethod]
    public async Task PerformAsync_PrerequestCallbackAborts_ReturnsAbortedByCallbackAndSendsNothing()
    {
        List<string> calls = [];
        ScriptedConnector connector = new([Encoding.ASCII.GetBytes(Response)]);
        await using ManagedCurlEngine engine = CreatePrerequestEngine(connector);
        await using IEasyTransfer easy = engine.CreateEasy();
        easy.Options = PrerequestOptions(calls, PrerequestResult.Abort);

        CurlCode code = await easy.PerformAsync(CancellationToken.None);

        Assert.AreEqual(CurlCode.AbortedByCallback, code);
        Assert.AreEqual("operation aborted by pre-request callback", easy.ErrorText);
        Assert.IsEmpty(connector.Written);
        CollectionAssert.AreEqual(new[] { "prereq" }, calls);
    }

    [TestMethod]
    public async Task PerformAsync_ResolverStartCallbackAborts_ReturnsAbortedByCallbackWithoutConnecting()
    {
        int started = 0;
        await using ManagedCurlEngine engine = new();
        await using IEasyTransfer easy = engine.CreateEasy();
        easy.Options = new EasyOptions
        {
            Network = new EasyNetworkOptions { Url = "http://resolver-start.invalid/" },
            Callbacks = new() { ResolverStart = _ => ++started },
        };

        CurlCode code = await easy.PerformAsync(CancellationToken.None);

        Assert.AreEqual(CurlCode.AbortedByCallback, code);
        Assert.AreEqual("Could not resolve host: resolver-start.invalid", easy.ErrorText);
        Assert.AreEqual(1, started);
    }

    [TestMethod]
    public void CreateConnectOnlyConnector_PrerequestCallbackSet_WrapsTheConnectorInAPrerequestConnector()
    {
        ManagedEngineSharedState shared = new(TimeProvider.System, _ => string.Empty);
        EasyOptions options = new() { Callbacks = new() { Prerequest = (_, _, _, _) => PrerequestResult.Ok } };

        Assert.IsInstanceOfType<PrerequestConnector>(ManagedCurlEngine.CreateConnectOnlyConnector(options, shared));
        Assert.IsNotNull(ManagedCurlEngine.CreateDispatcher(options, shared, cookieStore: null));
    }

    /// <summary>An engine whose handlers connect through <paramref name="connector" /> under each handle's prerequest callback, as production wraps its pool.</summary>
    private static ManagedCurlEngine CreatePrerequestEngine(IConnector connector) =>
        new(TimeProvider.System, (options, _, cookies) => CreateDispatcher(PrerequestConnector.Over(connector, options.Callbacks.Prerequest), cookies));

    /// <summary>A <c>GET http://h/</c> whose prerequest callback records each call and answers <paramref name="answer" />.</summary>
    private static EasyOptions PrerequestOptions(List<string> calls, PrerequestResult answer) => new()
    {
        Network = new EasyNetworkOptions { Url = "http://h/" },
        Callbacks = new()
        {
            Prerequest = (_, _, _, _) =>
            {
                calls.Add("prereq");
                return answer;
            },
        },
    };
}
