using System.Text;
using Curl.Api.Codes;
using Curl.Api.EngineContracts;
using Curl.Api.EngineContracts.Multi;
using Curl.Api.EngineContracts.Share;
using Curl.Api.Options;
using Curl.Api.Options.Http;
using Curl.Api.Options.Network;
using Curl.Core;
using Curl.Networking;
using Curl.Protocol.Abstractions;

namespace Curl.Engine.UnitTests;

/// <summary>
/// Two library easy handles of one engine share no cookies, connections, DNS, TLS sessions or HSTS unless a share handle
/// links them for exactly the kinds it shares, while a multi handle pools its handles' connections (BL-3204); alt-svc
/// entries stay the handle's whatever is shared, as libcurl has no share kind for them (BL-3272).
/// </summary>
[TestClass]
public sealed class EasyHandleIsolationTests
{
    private const string SetsCookie = "HTTP/1.1 200 OK\r\nSet-Cookie: a=1\r\nContent-Length: 0\r\n\r\n";

    private const string SetsSecondCookie = "HTTP/1.1 200 OK\r\nSet-Cookie: z=9\r\nContent-Length: 0\r\n\r\n";

    private const string Echo = "HTTP/1.1 200 OK\r\nContent-Length: 0\r\n\r\n";

    private const string LearnsHsts = "HTTP/1.1 200 OK\r\nStrict-Transport-Security: max-age=31536000\r\nContent-Length: 0\r\n\r\n";

    private const string LearnsAltSvc = "HTTP/1.1 200 OK\r\nAlt-Svc: h1=\"alt:8080\"; ma=3600\r\nContent-Length: 0\r\n\r\n";

    private static readonly EasyOptions CookieEngineOn = new()
    {
        Network = new EasyNetworkOptions { Url = "http://h/x" },
        Http = new EasyHttpOptions { CookieFiles = [string.Empty] },
    };

    [TestMethod]
    public async Task PerformAsync_TwoUnsharedHandles_ShareNoCookieAndNoConnection()
    {
        (ScriptedConnector connector, string secondRequest, long secondConnects) = await PerformTwiceAsync(share: null);

        Assert.DoesNotContain("Cookie:", secondRequest);
        Assert.HasCount(2, connector.Targets);
        Assert.AreEqual(1, secondConnects);
    }

    [TestMethod]
    public async Task PerformAsync_CookieOnlyShare_SharesTheCookiesButNotTheConnection()
    {
        (ScriptedConnector connector, string secondRequest, long secondConnects) = await PerformTwiceAsync(CurlLockData.Cookie);

        StringAssert.Contains(secondRequest, "Cookie: a=1\r\n");
        Assert.HasCount(2, connector.Targets);
        Assert.AreEqual(1, secondConnects);
    }

    [TestMethod]
    public async Task PerformAsync_ConnectionOnlyShare_ReusesTheConnectionButSharesNoCookie()
    {
        (ScriptedConnector connector, string secondRequest, long secondConnects) = await PerformTwiceAsync(CurlLockData.Connect);

        Assert.DoesNotContain("Cookie:", secondRequest);
        Assert.HasCount(1, connector.Targets);
        Assert.AreEqual(0, secondConnects);
    }

    [TestMethod]
    public async Task PerformAsync_DnsOnlyShare_SharesNeitherCookiesNorTheConnection()
    {
        (ScriptedConnector connector, string secondRequest, long secondConnects) = await PerformTwiceAsync(CurlLockData.Dns);

        Assert.DoesNotContain("Cookie:", secondRequest);
        Assert.HasCount(2, connector.Targets);
        Assert.AreEqual(1, secondConnects);
    }

    [TestMethod]
    public async Task PerformAsync_CookiesUnshared_ANewCookieSetByTheFirstNoLongerReachesTheSecond()
    {
        ScriptedConnector connector = new([Encoding.ASCII.GetBytes(SetsCookie), Encoding.ASCII.GetBytes(Echo), Encoding.ASCII.GetBytes(SetsSecondCookie), Encoding.ASCII.GetBytes(Echo)]);
        await using ManagedCurlEngine engine = CreateEngine(connector);
        IShareHandle share = engine.CreateShare();
        share.Share(CurlLockData.Cookie);
        await using IEasyTransfer first = engine.CreateEasy();
        await using IEasyTransfer second = engine.CreateEasy();
        first.Options = Attached(share);
        second.Options = Attached(share);
        await first.PerformAsync(CancellationToken.None);
        await second.PerformAsync(CancellationToken.None);

        first.Options = CookieEngineOn;
        second.Options = CookieEngineOn;
        Assert.AreEqual(CurlShareCode.Ok, share.Unshare(CurlLockData.Cookie));
        first.Options = Attached(share);
        second.Options = Attached(share);
        await first.PerformAsync(CancellationToken.None);
        int written = connector.Written.Length;
        await second.PerformAsync(CancellationToken.None);

        Assert.DoesNotContain("z=9", Encoding.ASCII.GetString(connector.Written[written..]));
    }

    [TestMethod]
    public async Task PerformAsync_TwoHandlesInOneMulti_ReuseTheMultisConnectionAndSendNoCookieOfEachOther()
    {
        ScriptedConnector connector = new([Encoding.ASCII.GetBytes(SetsCookie), Encoding.ASCII.GetBytes(Echo)]) { ReusesTheFirstConnection = true };
        await using ManagedCurlEngine engine = CreateEngine(connector);
        await using ManagedMultiTransfer multi = (ManagedMultiTransfer)engine.CreateMulti();
        await using IEasyTransfer first = engine.CreateEasy();
        await using IEasyTransfer second = engine.CreateEasy();
        first.Options = CookieEngineOn;
        second.Options = CookieEngineOn;

        multi.Add(first);
        await PerformUntilDoneAsync(multi);
        int written = connector.Written.Length;
        multi.Add(second);
        await PerformUntilDoneAsync(multi);

        Assert.HasCount(1, connector.Targets);
        Assert.AreEqual(0, second.Info.NumConnects);
        Assert.DoesNotContain("Cookie:", Encoding.ASCII.GetString(connector.Written[written..]));
    }

    [TestMethod]
    public async Task PerformAsync_HstsLearnedByAnUnsharedHandle_IsNotUsedByAnother()
    {
        IReadOnlyList<ConnectTarget> targets = await PerformLearningHstsTwiceAsync(sharesHsts: false);

        Assert.AreEqual(80, targets[1].Port);
    }

    [TestMethod]
    public async Task PerformAsync_HstsLearnedByAHandleSharingHsts_SwitchesTheOtherToHttps()
    {
        IReadOnlyList<ConnectTarget> targets = await PerformLearningHstsTwiceAsync(sharesHsts: true);

        Assert.AreEqual(443, targets[1].Port);
    }

    [TestMethod]
    public async Task PerformAsync_AltSvcLearnedByOneHandle_RoutesOnlyThatHandle()
    {
        ScriptedConnector connector = new([Encoding.ASCII.GetBytes(LearnsAltSvc), Encoding.ASCII.GetBytes(Echo), Encoding.ASCII.GetBytes(Echo)]);
        await using ManagedCurlEngine engine = CreateEngine(connector);
        EasyOptions options = new() { Network = new EasyNetworkOptions { Url = "http://h/x" }, Http = new EasyHttpOptions { AltSvc = string.Empty } };
        await using ManagedEasyTransfer first = (ManagedEasyTransfer)engine.CreateEasy();
        await using ManagedEasyTransfer second = (ManagedEasyTransfer)engine.CreateEasy();
        foreach (ManagedEasyTransfer handle in new[] { first, second })
        {
            handle.Options = options;
            handle.Host = EasyTransferHost.Library with { AltSvcUsesOverHttp = true };
        }

        await first.PerformAsync(CancellationToken.None);
        await second.PerformAsync(CancellationToken.None);
        await first.PerformAsync(CancellationToken.None);

        Assert.IsNull(connector.Targets[1].AltSvcRoute);
        Assert.IsNotNull(connector.Targets[2].AltSvcRoute);
    }

    [TestMethod]
    public async Task PerformAsync_TwoUnsharedHandles_ResumeNoTlsSessionOfEachOther()
    {
        (ManagedEngineSharedState first, ManagedEngineSharedState second, _) = await PerformRecordingStatesAsync(sharesTlsSessions: false);

        Assert.AreNotSame(first.TlsSessions, second.TlsSessions);
    }

    [TestMethod]
    public async Task PerformAsync_TwoHandlesSharingTlsSessions_ResumeTheSharesSessions()
    {
        (ManagedEngineSharedState first, ManagedEngineSharedState second, ManagedShareHandle? share) = await PerformRecordingStatesAsync(sharesTlsSessions: true);

        Assert.AreSame(share!.TlsSessions, first.TlsSessions);
        Assert.AreSame(first.TlsSessions, second.TlsSessions);
        Assert.AreNotSame(first.Hsts, second.Hsts);
    }

    [TestMethod]
    public async Task WithOwnCaches_EngineState_GivesFreshCachesAndItsOwnNumbering()
    {
        Dictionary<string, string> environment = new() { ["CURL_HSTS_HTTP"] = "yes" };
        await using ManagedCurlEngine engine = new(TimeProvider.System, null, readEnvironmentVariable: name => environment.GetValueOrDefault(name));

        ManagedEngineSharedState own = engine.Shared.WithOwnCaches();
        bool stored = own.Hsts.StoreFromResponse(CurlUrl.Parse("http://hsts.example.com/"), "max-age=31536000", DateTimeOffset.UtcNow);

        Assert.AreNotSame(engine.Shared.Connections, own.Connections);
        Assert.AreNotSame(engine.Shared.TlsSessions, own.TlsSessions);
        Assert.AreNotSame(engine.Shared.DnsCache, own.DnsCache);
        Assert.AreNotSame(engine.Shared.Cookies, own.Cookies);
        Assert.AreNotSame(engine.Shared.Hsts, own.Hsts);
        Assert.AreSame(engine.Shared, own.EngineWide);
        Assert.AreNotSame(engine.Shared.Performer, own.Performer);
        Assert.IsTrue(stored);
        Assert.IsFalse(engine.Shared.Hsts.TrySwitchToHttps("http://hsts.example.com/", CurlUrl.Parse("http://hsts.example.com/"), out _));
    }

    [TestMethod]
    public async Task Duplicate_HandleWhoseHstsCacheKnowsAHost_GivesTheCopyThatEntryInACacheOfItsOwn()
    {
        Dictionary<string, string> environment = new() { ["CURL_HSTS_HTTP"] = "yes" };
        await using ManagedCurlEngine engine = new(TimeProvider.System, null, readEnvironmentVariable: name => environment.GetValueOrDefault(name));
        ManagedEngineSharedState source = engine.Shared.WithOwnCaches();
        _ = source.Hsts.StoreFromResponse(CurlUrl.Parse("http://hsts.example.com/"), "max-age=31536000", DateTimeOffset.UtcNow);

        ManagedEngineSharedState duplicate = source.DuplicatedWithOwnCaches();

        Assert.AreNotSame(source.Hsts, duplicate.Hsts);
        Assert.IsTrue(duplicate.Hsts.TrySwitchToHttps("http://hsts.example.com/", CurlUrl.Parse("http://hsts.example.com/"), out _));
    }

    [TestMethod]
    public async Task Duplicate_HandleWithAnEmptyHstsCache_GivesTheCopyAnEmptyCacheOfItsOwn()
    {
        await using ManagedCurlEngine engine = new();
        ManagedEngineSharedState source = engine.Shared.WithOwnCaches();

        ManagedEngineSharedState duplicate = source.DuplicatedWithOwnCaches();

        Assert.AreNotSame(source.Hsts, duplicate.Hsts);
        Assert.IsFalse(duplicate.Hsts.TrySwitchToHttps("http://hsts.example.com/", CurlUrl.Parse("http://hsts.example.com/"), out _));
    }

    [TestMethod]
    public async Task DisposeAsync_EngineWithAnIdleConnectionInAHandlesOwnPool_ClosesIt()
    {
        ManagedCurlEngine engine = new();
        ManagedEngineSharedState own = engine.Shared.WithOwnCaches();
        QueuedConnection idle = new();
        PoolingConnector pool = new(new OneConnectionConnector(idle), own.Connections, configuration: null);
        ConnectResult result = await pool.ConnectAsync(new ConnectTarget("h.example", 80, false) { PoolScheme = "http" }, CancellationToken.None);
        result.Connection!.MarkReusable();
        await result.Connection.DisposeAsync();

        await engine.DisposeAsync();

        Assert.IsTrue(idle.IsDisposed);
    }

    [TestMethod]
    public async Task InMulti_HandleState_TakesTheMultisConnectionsAndKeepsItsOwnCookiesAndHsts()
    {
        await using ManagedCurlEngine engine = new();
        ManagedEngineSharedState handle = engine.Shared.WithOwnCaches();
        ManagedEngineSharedState multi = engine.Shared.WithOwnCaches();

        ManagedEngineSharedState inMulti = handle.InMulti(multi);

        Assert.AreSame(multi.Connections, inMulti.Connections);
        Assert.AreSame(multi.TlsSessions, inMulti.TlsSessions);
        Assert.AreSame(multi.DnsCache, inMulti.DnsCache);
        Assert.AreSame(handle.Cookies, inMulti.Cookies);
        Assert.AreSame(handle.Hsts, inMulti.Hsts);
    }

    private static async Task<(ScriptedConnector Connector, string SecondRequest, long SecondConnects)> PerformTwiceAsync(CurlLockData? share)
    {
        ScriptedConnector connector = new([Encoding.ASCII.GetBytes(SetsCookie), Encoding.ASCII.GetBytes(Echo)]) { ReusesTheFirstConnection = share == CurlLockData.Connect };
        await using ManagedCurlEngine engine = CreateEngine(connector);
        EasyOptions options = CookieEngineOn;
        if (share is { } kind)
        {
            IShareHandle handle = engine.CreateShare();
            handle.Share(kind);
            options = Attached(handle);
        }

        await using IEasyTransfer first = engine.CreateEasy();
        await using IEasyTransfer second = engine.CreateEasy();
        first.Options = options;
        second.Options = options;
        await first.PerformAsync(CancellationToken.None);
        int written = connector.Written.Length;
        await second.PerformAsync(CancellationToken.None);
        return (connector, Encoding.ASCII.GetString(connector.Written[written..]), second.Info.NumConnects);
    }

    private static async Task<IReadOnlyList<ConnectTarget>> PerformLearningHstsTwiceAsync(bool sharesHsts)
    {
        ScriptedConnector connector = new([Encoding.ASCII.GetBytes(LearnsHsts), Encoding.ASCII.GetBytes(Echo)]);
        await using ManagedCurlEngine engine = CreateEngine(connector);
        EasyOptions options = new() { Network = new EasyNetworkOptions { Url = "http://h/x" }, Http = new EasyHttpOptions { HstsControl = HstsControl.Enable } };
        if (sharesHsts)
        {
            IShareHandle share = engine.CreateShare();
            share.Share(CurlLockData.Hsts);
            options = options with { Behaviour = options.Behaviour with { Share = share } };
        }

        await using IEasyTransfer first = engine.CreateEasy();
        await using IEasyTransfer second = engine.CreateEasy();
        first.Options = options with { Network = new EasyNetworkOptions { Url = "https://h/x" } };
        second.Options = options;
        await first.PerformAsync(CancellationToken.None);
        await second.PerformAsync(CancellationToken.None);
        return connector.Targets;
    }

    private static async Task<(ManagedEngineSharedState First, ManagedEngineSharedState Second, ManagedShareHandle? Share)> PerformRecordingStatesAsync(bool sharesTlsSessions)
    {
        ScriptedConnector connector = new([Encoding.ASCII.GetBytes(Echo), Encoding.ASCII.GetBytes(Echo)]);
        List<ManagedEngineSharedState> states = [];
        await using ManagedCurlEngine engine = CreateEngine(connector, recordState: states.Add);
        EasyOptions options = CookieEngineOn;
        ManagedShareHandle? share = null;
        if (sharesTlsSessions)
        {
            share = (ManagedShareHandle)engine.CreateShare();
            share.Share(CurlLockData.SslSession);
            options = Attached(share);
        }

        await using IEasyTransfer first = engine.CreateEasy();
        await using IEasyTransfer second = engine.CreateEasy();
        first.Options = options;
        second.Options = options;
        await first.PerformAsync(CancellationToken.None);
        await second.PerformAsync(CancellationToken.None);
        return (states[0], states[1], share);
    }

    private static EasyOptions Attached(IShareHandle share) =>
        CookieEngineOn with { Behaviour = CookieEngineOn.Behaviour with { Share = share } };

    private static ManagedCurlEngine CreateEngine(ScriptedConnector connector, Action<ManagedEngineSharedState>? recordState = null) =>
        new(TimeProvider.System, (_, shared, cookies) => DispatcherOf(connector, shared, cookies, recordState));

    private static ProtocolDispatcher DispatcherOf(ScriptedConnector connector, ManagedEngineSharedState shared, ICookieStore? cookies, Action<ManagedEngineSharedState>? recordState)
    {
        recordState?.Invoke(shared);
        return new ProtocolDispatcher(EngineComposition.CreateProtocolHandlers(
            new PoolingConnector(connector, shared.Connections, configuration: null),
            new RecordingDatagramConnector(CurlExitCode.CouldntConnect, "Failed to connect to h port 80 after 0 ms: Could not connect to server"),
            new PassThroughTlsProvider(),
            new LoopbackDnsResolver(),
            cookies));
    }

    /// <summary>Gives the one connection it was made with.</summary>
    private sealed class OneConnectionConnector(QueuedConnection connection) : IConnector
    {
        public ValueTask<ConnectResult> ConnectAsync(ConnectTarget target, CancellationToken cancellationToken) =>
            ValueTask.FromResult(ConnectResult.Connected(connection));
    }

    private static async Task PerformUntilDoneAsync(ManagedMultiTransfer multi)
    {
        int running;
        do
        {
            (_, running) = await multi.PerformAsync(CancellationToken.None);
            await multi.WaitAsync(Memory<CurlWaitSocket>.Empty, TimeSpan.FromMilliseconds(100), CancellationToken.None);
        }
        while (running > 0);
    }
}
