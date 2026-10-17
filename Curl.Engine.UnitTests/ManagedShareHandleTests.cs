using System.Text;
using Curl.Api.Codes;
using Curl.Api.EngineContracts;
using Curl.Api.EngineContracts.Share;
using Curl.Api.Options;
using Curl.Api.Options.Behaviour;
using Curl.Api.Options.Http;
using Curl.Api.Options.Network;
using Curl.Cookies;
using Curl.Core;
using Curl.Networking;
using Curl.Protocol.Abstractions;

namespace Curl.Engine;

/// <summary>
/// Pins the engine's share handle (ADR-0606): handles of two engines attached to one share handle send each
/// other's cookies and reuse each other's connections for the kinds it shares, the lock callbacks are called
/// around each access, and <c>curl_share_setopt</c> and <c>curl_share_cleanup</c> answer as curl 8.22.0's do.
/// </summary>
[TestClass]
public sealed class ManagedShareHandleTests
{
    private const string SetsCookie = "HTTP/1.1 200 OK\r\nSet-Cookie: got=2\r\nContent-Length: 0\r\n\r\n";

    private const string KeepsAlive = "HTTP/1.1 200 OK\r\nContent-Length: 0\r\n\r\n";

    private const string LearnsHsts = "HTTP/1.1 200 OK\r\nStrict-Transport-Security: max-age=86400\r\nContent-Length: 0\r\n\r\n";

    private const string ConnectFailure = "Failed to connect to h port 80 after 0 ms: Could not connect to server";

    [TestMethod]
    public async Task PerformAsync_CookiesShared_SendsTheCookieOneEnginesHandleGotFromAnotherEnginesHandle()
    {
        ScriptedConnector connector = new([Encoding.ASCII.GetBytes(SetsCookie), Encoding.ASCII.GetBytes(KeepsAlive)]);
        await using ManagedCurlEngine first = CreateEngine(connector);
        await using ManagedCurlEngine second = CreateEngine(connector);
        IShareHandle share = first.CreateShare();
        share.Share(CurlLockData.Cookie);

        CurlCode setting = await PerformAsync(first, "http://example.com/a", share);
        CurlCode sending = await PerformAsync(second, "http://example.com/b", share);

        Assert.AreEqual(CurlCode.Ok, setting);
        Assert.AreEqual(CurlCode.Ok, sending);
        StringAssert.Contains(SecondRequestOf(connector), "Cookie: got=2\r\n");
    }

    [TestMethod]
    public async Task PerformAsync_CookiesUnshared_SendsTheOtherEnginesCookieNoMore()
    {
        ScriptedConnector connector = new([Encoding.ASCII.GetBytes(SetsCookie), Encoding.ASCII.GetBytes(KeepsAlive)]);
        await using ManagedCurlEngine first = CreateEngine(connector);
        await using ManagedCurlEngine second = CreateEngine(connector);
        IShareHandle share = first.CreateShare();
        share.Share(CurlLockData.Cookie);
        await PerformAsync(first, "http://example.com/a", share);

        CurlShareCode unshared = share.Unshare(CurlLockData.Cookie);
        await PerformAsync(second, "http://example.com/b", share);

        Assert.AreEqual(CurlShareCode.Ok, unshared);
        Assert.DoesNotContain("Cookie:", SecondRequestOf(connector));
    }

    [TestMethod]
    public async Task PerformAsync_ConnectionsShared_ReusesTheConnectionAnotherEnginesHandleOpened()
    {
        OpenCountingConnector connector = new(new ScriptedConnector([Encoding.ASCII.GetBytes(KeepsAlive), Encoding.ASCII.GetBytes(KeepsAlive)]));
        await using ManagedCurlEngine first = CreateEngine(connector);
        await using ManagedCurlEngine second = CreateEngine(connector);
        IShareHandle share = first.CreateShare();
        share.Share(CurlLockData.Connect);

        CurlCode opening = await PerformAsync(first, "http://h/a", share);
        CurlCode reusing = await PerformAsync(second, "http://h/b", share);

        Assert.AreEqual(CurlCode.Ok, opening);
        Assert.AreEqual(CurlCode.Ok, reusing);
        Assert.AreEqual(1, connector.Opened);
    }

    [TestMethod]
    public async Task PerformAsync_NothingShared_OpensAConnectionForEachEngine()
    {
        OpenCountingConnector connector = new(new ScriptedConnector([Encoding.ASCII.GetBytes(KeepsAlive), Encoding.ASCII.GetBytes(KeepsAlive)]));
        await using ManagedCurlEngine first = CreateEngine(connector);
        await using ManagedCurlEngine second = CreateEngine(connector);
        IShareHandle share = first.CreateShare();

        await PerformAsync(first, "http://h/a", share);
        await PerformAsync(second, "http://h/b", share);

        Assert.AreEqual(2, connector.Opened);
    }

    [TestMethod]
    public async Task PerformAsync_HstsShared_SwitchesAnotherEnginesHandleToHttpsForAHostOneHandleLearned()
    {
        ScriptedConnector connector = new([Encoding.ASCII.GetBytes(LearnsHsts), Encoding.ASCII.GetBytes(KeepsAlive)]);
        await using ManagedCurlEngine first = CreateEngine(connector);
        await using ManagedCurlEngine second = CreateEngine(connector);
        IShareHandle share = first.CreateShare();
        share.Share(CurlLockData.Hsts);

        CurlCode learning = await PerformAsync(first, "https://example.com/a", share);
        CurlCode switching = await PerformAsync(second, "http://example.com/b", share);

        Assert.AreEqual(CurlCode.Ok, learning);
        Assert.AreEqual(CurlCode.Ok, switching);
        Assert.AreEqual(443, connector.Targets[1].Port);
    }

    [TestMethod]
    public async Task PerformAsync_HstsNotShared_LeavesAnotherEnginesHandleOnHttp()
    {
        ScriptedConnector connector = new([Encoding.ASCII.GetBytes(LearnsHsts), Encoding.ASCII.GetBytes(KeepsAlive)]);
        await using ManagedCurlEngine first = CreateEngine(connector);
        await using ManagedCurlEngine second = CreateEngine(connector);
        IShareHandle share = first.CreateShare();

        await PerformAsync(first, "https://example.com/a", share);
        await PerformAsync(second, "http://example.com/b", share);

        Assert.AreEqual(80, connector.Targets[1].Port);
    }

    [TestMethod]
    public async Task PerformAsync_PslShared_TakesThePublicSuffixListUnderItsLock()
    {
        ScriptedConnector connector = new([Encoding.ASCII.GetBytes(KeepsAlive)]);
        await using ManagedCurlEngine engine = CreateEngine(connector);
        IShareHandle share = engine.CreateShare();
        share.Share(CurlLockData.Psl);
        List<string> calls = [];
        share.SetLockCallback((_, data, _) => calls.Add($"lock {data}"));
        share.SetUnlockCallback((_, data) => calls.Add($"unlock {data}"));

        CurlCode code = await PerformAsync(engine, "http://example.com/a", share);

        Assert.AreEqual(CurlCode.Ok, code);
        CollectionAssert.AreEqual(new[] { "lock Share", "unlock Share", "lock Psl", "unlock Psl", "lock Share", "unlock Share" }, calls);
    }

    [TestMethod]
    public async Task PerformAsync_CookiesSharedButNoCookieOption_SendsNoCookie()
    {
        ScriptedConnector connector = new([Encoding.ASCII.GetBytes(KeepsAlive)]);
        await using ManagedCurlEngine engine = CreateEngine(connector);
        IShareHandle share = engine.CreateShare();
        share.Share(CurlLockData.Cookie);
        await using IEasyTransfer easy = engine.CreateEasy();
        easy.Options = new EasyOptions
        {
            Network = new EasyNetworkOptions { Url = "http://example.com/a" },
            Behaviour = new EasyBehaviourOptions { Share = share },
        };

        CurlCode code = await easy.PerformAsync(CancellationToken.None);

        Assert.AreEqual(CurlCode.Ok, code);
        Assert.DoesNotContain("Cookie:", Encoding.ASCII.GetString(connector.Written));
    }

    [TestMethod]
    public async Task PerformAsync_LockCallbacks_LockTheShareAroundAttachAndDetachAndEachCacheAroundItsAccess()
    {
        ScriptedConnector connector = new([Encoding.ASCII.GetBytes(SetsCookie)]);
        await using ManagedCurlEngine engine = CreateEngine(connector);
        IShareHandle share = engine.CreateShare();
        share.Share(CurlLockData.Cookie);
        share.Share(CurlLockData.Dns);
        List<string> calls = [];
        share.SetLockCallback((transfer, data, access) => calls.Add($"lock {data} {access} {transfer is not null}"));
        share.SetUnlockCallback((transfer, data) => calls.Add($"unlock {data} {transfer is not null}"));

        await PerformAsync(engine, "http://example.com/a", share);

        CollectionAssert.AreEqual(
            new[] { "lock Share Single True", "unlock Share True", "lock Cookie Single True", "unlock Cookie True" },
            calls.Take(4).ToArray());
        CollectionAssert.AreEqual(new[] { "lock Share Single True", "unlock Share True" }, calls.TakeLast(2).ToArray());
        Assert.IsGreaterThan(8, calls.Count);
        for (int call = 0; call < calls.Count; call += 2)
        {
            Assert.AreEqual(calls[call].Replace("lock ", "unlock ", StringComparison.Ordinal).Replace(" Single", string.Empty, StringComparison.Ordinal), calls[call + 1]);
        }
    }

    [TestMethod]
    public async Task PerformAsync_CookiesAndDnsShared_LocksInLibcurlsOrderAndLooksAHostUpOnceAddedOnce()
    {
        ScriptedConnector connector = new([Encoding.ASCII.GetBytes(SetsCookie), Encoding.ASCII.GetBytes(SetsCookie)]);
        await using ManagedCurlEngine engine = CreateEngine(connector);
        IShareHandle share = engine.CreateShare();
        share.Share(CurlLockData.Cookie);
        share.Share(CurlLockData.Dns);
        List<string> calls = [];
        share.SetLockCallback((_, data, _) => calls.Add(data.ToString()));

        await PerformAsync(engine, "http://example.com/a", share);
        int firstPerform = calls.Count;
        await PerformAsync(engine, "http://example.com/b", share);

        string[] expectedFirst =
        [
            "Share", "Cookie", "Cookie", "Dns", "Dns", "Dns", "Cookie", "Cookie", "Dns", "Cookie", "Share",
        ];
        CollectionAssert.AreEqual(expectedFirst, calls.Take(firstPerform).ToArray());
        string[] expectedSecond = ["Share", "Cookie", "Cookie", "Dns", "Dns", "Cookie", "Cookie", "Dns", "Cookie", "Share"];
        CollectionAssert.AreEqual(expectedSecond, calls.Skip(firstPerform).ToArray());
    }

    [TestMethod]
    public async Task PerformAsync_DnsSharedAndUrlMalformed_PrunesAndUnlinksWithoutALookUp()
    {
        ScriptedConnector connector = new([]);
        await using ManagedCurlEngine engine = CreateEngine(connector);
        IShareHandle share = engine.CreateShare();
        share.Share(CurlLockData.Dns);
        List<string> calls = [];
        share.SetLockCallback((_, data, _) => calls.Add(data.ToString()));

        CurlCode code = await PerformAsync(engine, "http://[x/", share);

        Assert.AreEqual(CurlCode.UrlMalformat, code);
        CollectionAssert.AreEqual(new[] { "Share", "Dns", "Dns", "Share" }, calls);
    }

    [TestMethod]
    public void ShareHostKeyOf_UrlSetOrNot_GivesTheHostAndPortOrNone()
    {
        string? parsed = ManagedEasyTransfer.ShareHostKeyOf(new EasyOptions { Network = new EasyNetworkOptions { Url = "http://Example.com/a" } });
        string? malformed = ManagedEasyTransfer.ShareHostKeyOf(new EasyOptions { Network = new EasyNetworkOptions { Url = "http://[x/" } });
        string? none = ManagedEasyTransfer.ShareHostKeyOf(new EasyOptions());

        Assert.AreEqual("example.com:80", parsed, ignoreCase: true);
        Assert.IsNull(malformed);
        Assert.IsNull(none);
    }

    [TestMethod]
    public async Task Options_ShareCookieListAndCleanup_LockAsLibcurlDoesAndCleanupInUseTakesNoLock()
    {
        await using ManagedCurlEngine engine = CreateEngine(new ScriptedConnector([]));
        IShareHandle share = engine.CreateShare();
        share.Share(CurlLockData.Cookie);
        List<string> calls = [];
        share.SetLockCallback((_, data, _) => calls.Add($"lock {data}"));
        share.SetUnlockCallback((_, data) => calls.Add($"unlock {data}"));
        IEasyTransfer easy = engine.CreateEasy();

        easy.Options = easy.Options with { Behaviour = easy.Options.Behaviour with { Share = share } };
        easy.Options = easy.Options with { Behaviour = easy.Options.Behaviour with { Share = share } };
        easy.Options = easy.Options with { Http = easy.Options.Http with { CookieList = ["ALL"] } };
        easy.Options = easy.Options with { Http = easy.Options.Http with { CookieList = ["ALL", "SESS"] } };
        calls.Add("set");
        CurlShareCode inUse = share.Cleanup();
        await easy.DisposeAsync();
        CurlShareCode cleaned = share.Cleanup();

        Assert.AreEqual(CurlShareCode.InUse, inUse);
        Assert.AreEqual(CurlShareCode.Ok, cleaned);
        CollectionAssert.AreEqual(
            new[] { "lock Share", "unlock Share", "lock Cookie", "unlock Cookie", "lock Cookie", "unlock Cookie", "set", "lock Cookie", "unlock Cookie", "lock Share", "unlock Share", "lock Share", "unlock Share" },
            calls);
    }

    [TestMethod]
    public async Task Options_ShareChangedOrCleanedUp_DetachesFromTheOldAndAttachesOnlyALiveOne()
    {
        await using ManagedCurlEngine engine = CreateEngine(new ScriptedConnector([]));
        IShareHandle first = engine.CreateShare();
        IShareHandle cleanedUp = engine.CreateShare();
        cleanedUp.Cleanup();
        await using IEasyTransfer easy = engine.CreateEasy();

        easy.Options = easy.Options with { Behaviour = easy.Options.Behaviour with { Share = first } };
        CurlShareCode whileAttached = first.Share(CurlLockData.Dns);
        easy.Options = easy.Options with { Behaviour = easy.Options.Behaviour with { Share = cleanedUp } };
        CurlShareCode afterDetach = first.Share(CurlLockData.Dns);

        Assert.AreEqual(CurlShareCode.InUse, whileAttached);
        Assert.AreEqual(CurlShareCode.Ok, afterDetach);
        Assert.AreEqual(CurlShareCode.Ok, first.Cleanup());
    }

    [TestMethod]
    public async Task SetOptions_WhileATransferIsAttached_AnswerInUse()
    {
        ScriptedConnector connector = new([Encoding.ASCII.GetBytes(SetsCookie)]);
        await using ManagedCurlEngine engine = CreateEngine(connector);
        IShareHandle share = engine.CreateShare();
        share.Share(CurlLockData.Cookie);
        List<CurlShareCode> codes = [];
        share.SetLockCallback((_, data, _) =>
        {
            if (data == CurlLockData.Cookie && codes.Count == 0)
            {
                codes.Add(share.Share(CurlLockData.Dns));
                codes.Add(share.Unshare(CurlLockData.Cookie));
                codes.Add(share.SetUnlockCallback(null));
                codes.Add(share.Cleanup());
            }
        });

        await PerformAsync(engine, "http://example.com/a", share);

        CollectionAssert.AreEqual(new[] { CurlShareCode.InUse, CurlShareCode.InUse, CurlShareCode.InUse, CurlShareCode.InUse }, codes);
        Assert.AreEqual(CurlShareCode.Ok, share.Cleanup());
    }

    [TestMethod]
    [DataRow(CurlLockData.Cookie)]
    [DataRow(CurlLockData.Dns)]
    [DataRow(CurlLockData.SslSession)]
    [DataRow(CurlLockData.Connect)]
    [DataRow(CurlLockData.Psl)]
    [DataRow(CurlLockData.Hsts)]
    public void Share_AKindCurlShares_AnswersOk(CurlLockData data)
    {
        ManagedShareHandle share = new(TimeProvider.System);

        Assert.AreEqual(CurlShareCode.Ok, share.Share(data));
        Assert.AreEqual(CurlShareCode.Ok, share.Unshare(data));
    }

    [TestMethod]
    [DataRow(CurlLockData.None)]
    [DataRow(CurlLockData.Share)]
    [DataRow((CurlLockData)8)]
    public void Share_AKindCurlDoesNotShare_AnswersBadOption(CurlLockData data)
    {
        ManagedShareHandle share = new(TimeProvider.System);

        Assert.AreEqual(CurlShareCode.BadOption, share.Share(data));
        Assert.AreEqual(CurlShareCode.BadOption, share.Unshare(data));
    }

    [TestMethod]
    public void Cleanup_ThenAnyCall_AnswersInvalid()
    {
        ManagedShareHandle share = new(TimeProvider.System);
        List<string> calls = [];
        share.SetLockCallback((transfer, data, _) => calls.Add($"lock {data} {transfer is null}"));

        CurlShareCode cleaned = share.Cleanup();

        Assert.AreEqual(CurlShareCode.Ok, cleaned);
        CollectionAssert.AreEqual(new[] { "lock Share True" }, calls);
        Assert.AreEqual(CurlShareCode.Invalid, share.Cleanup());
        Assert.AreEqual(CurlShareCode.Invalid, share.Share(CurlLockData.Cookie));
        Assert.AreEqual(CurlShareCode.Invalid, share.SetLockCallback(null));
    }

    [TestMethod]
    public async Task PerformAsync_ShareCleanedUp_RunsOverTheEnginesOwnCaches()
    {
        OpenCountingConnector connector = new(new ScriptedConnector([Encoding.ASCII.GetBytes(KeepsAlive), Encoding.ASCII.GetBytes(KeepsAlive)]));
        await using ManagedCurlEngine first = CreateEngine(connector);
        await using ManagedCurlEngine second = CreateEngine(connector);
        IShareHandle share = first.CreateShare();
        share.Share(CurlLockData.Connect);
        share.Cleanup();

        CurlCode code = await PerformAsync(first, "http://h/a", share);
        await PerformAsync(second, "http://h/b", share);

        Assert.AreEqual(CurlCode.Ok, code);
        Assert.AreEqual(2, connector.Opened);
    }

    [TestMethod]
    public void CookieStoreFor_CookiesNotShared_GivesTheStoreItself()
    {
        ManagedShareHandle share = new(TimeProvider.System);
        ICookieStore store = CookieEngine.FromHttpOptions(new EasyHttpOptions { CookieJar = "j" }, share.Cookies)!.HandlerStore;
        IEasyTransfer transfer = new ManagedCurlEngine().CreateEasy();

        Assert.AreSame(store, share.CookieStoreFor(store, transfer));
        share.Share(CurlLockData.Cookie);
        Assert.IsNull(share.CookieStoreFor(null, transfer));
    }

    [TestMethod]
    public void CookieStoreFor_CookiesShared_StoresAndSendsUnderTheCookieLock()
    {
        ManagedShareHandle share = new(TimeProvider.System);
        share.Share(CurlLockData.Cookie);
        List<string> calls = [];
        share.SetLockCallback((_, data, _) => calls.Add($"lock {data}"));
        share.SetUnlockCallback((_, data) => calls.Add($"unlock {data}"));
        ICookieStore store = CookieEngine.FromHttpOptions(new EasyHttpOptions { CookieJar = "j" }, share.Cookies)!.HandlerStore;
        ICookieStore locked = share.CookieStoreFor(store, new ManagedCurlEngine().CreateEasy())!;
        CurlUrl url = CurlUrl.Parse("http://example.com/x");
        DateTimeOffset now = DateTimeOffset.UnixEpoch.AddYears(56);

        int stored = locked.StoreFromResponse(url, "got=2", 0, now, NoTransferEvents.Instance);
        string? sent = locked.GetCookieHeader(url, secure: false, now, NoTransferEvents.Instance);
        string? storedSent = locked.GetStoredCookieHeader(url, secure: false, now, NoTransferEvents.Instance);

        Assert.AreEqual(1, stored);
        Assert.AreEqual("got=2", sent);
        Assert.AreEqual("got=2", storedSent);
        CollectionAssert.AreEqual(new[] { "lock Cookie", "unlock Cookie", "lock Cookie", "unlock Cookie", "lock Cookie", "unlock Cookie" }, calls);
    }

    [TestMethod]
    public async Task Options_CookieListReloadThenGetInfo_LoadsTheFileAtSetoptAndListsUnderOneMoreCookieLock()
    {
        const string Jar = ".example.com\tTRUE\t/\tFALSE\t4102444800\tone\t1\n.example.com\tTRUE\t/\tFALSE\t4102444800\ttwo\t2\n";
        await using ManagedCurlEngine engine = CreateEngine(new ScriptedConnector([]));
        IShareHandle share = engine.CreateShare();
        share.Share(CurlLockData.Cookie);
        List<string> calls = [];
        share.SetLockCallback((_, data, _) => calls.Add($"lock {data}"));
        share.SetUnlockCallback((_, data) => calls.Add($"unlock {data}"));
        await using ManagedEasyTransfer easy = (ManagedEasyTransfer)engine.CreateEasy();
        easy.Host = new EasyTransferHost(new OneFileSystem("jar", Jar), Stream.Null, () => Stream.Null, NoDiagnosticLog.Instance, RunsOnWindows: false, AltSvcUsesOverHttp: false);
        easy.Options = easy.Options with { Behaviour = easy.Options.Behaviour with { Share = share } };
        easy.Options = easy.Options with { Http = easy.Options.Http with { CookieList = ["Set-Cookie: gone=yes; domain=example.com"] } };
        easy.Options = easy.Options with { Http = easy.Options.Http with { CookieList = [.. easy.Options.Http.CookieList, "ALL"], CookieFiles = ["jar"] } };
        calls.Clear();

        easy.Options = easy.Options with { Http = easy.Options.Http with { CookieList = [.. easy.Options.Http.CookieList, "RELOAD"] } };
        calls.Add("reloaded");
        IReadOnlyList<string> listed = [.. easy.Info.CookieList];

        CollectionAssert.AreEqual(
            new[] { ".example.com\tTRUE\t/\tFALSE\t4102444800\tone\t1", ".example.com\tTRUE\t/\tFALSE\t4102444800\ttwo\t2" },
            listed.ToArray());
        CollectionAssert.AreEqual(new[] { "lock Cookie", "unlock Cookie", "reloaded", "lock Cookie", "unlock Cookie" }, calls);
    }

    private static async Task<CurlCode> PerformAsync(ManagedCurlEngine engine, string url, IShareHandle share)
    {
        await using ManagedEasyTransfer easy = (ManagedEasyTransfer)engine.CreateEasy();
        easy.Options = new EasyOptions
        {
            Network = new EasyNetworkOptions { Url = url },
            Http = new EasyHttpOptions { CookieFiles = ["none.txt"] },
            Behaviour = new EasyBehaviourOptions { Share = share },
        };
        easy.Host = new EasyTransferHost(new NoFileSystem(), Stream.Null, () => Stream.Null, NoDiagnosticLog.Instance, RunsOnWindows: false, AltSvcUsesOverHttp: false);
        return await easy.PerformAsync(CancellationToken.None);
    }

    private static string SecondRequestOf(ScriptedConnector connector)
    {
        string written = Encoding.ASCII.GetString(connector.Written);
        return written[written.IndexOf("GET /b", StringComparison.Ordinal)..];
    }

    private static ManagedCurlEngine CreateEngine(IConnector connector) =>
        new(TimeProvider.System, (_, shared, cookies) => new ProtocolDispatcher(EngineComposition.CreateProtocolHandlers(
            new PoolingConnector(connector, shared.Connections, configuration: null),
            new RecordingDatagramConnector(CurlExitCode.CouldntConnect, ConnectFailure),
            new PassThroughTlsProvider(),
            new LoopbackDnsResolver(),
            cookies)));

    /// <summary>Counts the connections its inner connector opens.</summary>
    private sealed class OpenCountingConnector(IConnector inner) : IConnector
    {
        public int Opened { get; private set; }

        public ValueTask<ConnectResult> ConnectAsync(ConnectTarget target, CancellationToken cancellationToken)
        {
            Opened++;
            return inner.ConnectAsync(target, cancellationToken);
        }
    }

    /// <summary>A file system holding one file to read and refusing every write.</summary>
    private sealed class OneFileSystem(string name, string content) : IFileSystem
    {
        public ValueTask<FileOpenResult> OpenForReadAsync(string path, CancellationToken cancellationToken)
        {
            byte[] bytes = Encoding.Latin1.GetBytes(content);
            return ValueTask.FromResult(path == name
                ? FileOpenResult.Opened(new MemoryStream(bytes, writable: false), bytes.Length, null)
                : FileOpenResult.Failed(FileAccessStatus.NotFound));
        }

        public ValueTask<FileOpenResult> OpenForWriteAsync(string path, FileWriteMode mode, UnixFileMode createMode, CancellationToken cancellationToken) =>
            ValueTask.FromResult(FileOpenResult.Failed(FileAccessStatus.NotFound));
    }

    /// <summary>A file system holding no file, so a cookie file loads nothing.</summary>
    private sealed class NoFileSystem : IFileSystem
    {
        public ValueTask<FileOpenResult> OpenForReadAsync(string path, CancellationToken cancellationToken) =>
            ValueTask.FromResult(FileOpenResult.Failed(FileAccessStatus.NotFound));

        public ValueTask<FileOpenResult> OpenForWriteAsync(string path, FileWriteMode mode, UnixFileMode createMode, CancellationToken cancellationToken) =>
            ValueTask.FromResult(FileOpenResult.Failed(FileAccessStatus.NotFound));
    }
}
