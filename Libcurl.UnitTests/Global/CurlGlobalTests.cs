using Curl.Api.Codes;
using Libcurl.Global;

namespace Libcurl.UnitTests.Global;

[TestClass]
public sealed class CurlGlobalTests
{
    private static readonly CurlMallocCallback Malloc = _ => 0;
    private static readonly CurlFreeCallback Free = _ => { };
    private static readonly CurlReallocCallback Realloc = (_, _) => 0;
    private static readonly CurlStrdupCallback Strdup = _ => 0;
    private static readonly CurlCallocCallback Calloc = (_, _) => 0;

    [TestMethod]
    public async Task Init_CountsOneInitAndKeepsTheFlags()
    {
        var counter = new GlobalInitCounter();

        await using var library = CurlGlobal.InitCounting(new FakeCurlEngine(), CurlGlobalFlags.Win32 | CurlGlobalFlags.AckEintr, counter);

        Assert.AreEqual(1, counter.Count);
        Assert.AreEqual(CurlGlobalFlags.Win32 | CurlGlobalFlags.AckEintr, library.Flags);
        Assert.IsFalse(library.IsCleanedUp);
    }

    [TestMethod]
    public async Task Init_TwiceCountsTwo_AndEachCleanupCountsOneDown()
    {
        var counter = new GlobalInitCounter();
        var first = CurlGlobal.InitCounting(new FakeCurlEngine(), CurlGlobalFlags.Default, counter);
        var second = CurlGlobal.InitCounting(new FakeCurlEngine(), CurlGlobalFlags.Nothing, counter);
        Assert.AreEqual(2, counter.Count);

        await first.DisposeAsync();
        Assert.AreEqual(1, counter.Count);

        await second.DisposeAsync();
        Assert.AreEqual(0, counter.Count);
    }

    [TestMethod]
    public async Task Cleanup_DisposesTheEngineOnce_AndASecondCleanupDoesNothing()
    {
        var counter = new GlobalInitCounter();
        var engine = new FakeCurlEngine();
        var library = CurlGlobal.InitCounting(engine, CurlGlobalFlags.All, counter);

        await library.DisposeAsync();
        await library.DisposeAsync();

        Assert.AreEqual(1, engine.DisposeCount);
        Assert.AreEqual(0, counter.Count);
        Assert.IsTrue(library.IsCleanedUp);
    }

    [TestMethod]
    public void Init_WithoutAnEngine_Throws()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => CurlGlobal.Init(null!));
    }

    [TestMethod]
    public async Task Init_ThePublicOverload_CountsOnTheProcessCounterAndDefaultsTheFlags()
    {
        // Other tests may init in parallel, so only this library's own effect is checked.
        var engine = new FakeCurlEngine();
        var library = CurlGlobal.Init(engine);
        Assert.IsGreaterThanOrEqualTo(1, CurlGlobal.InitCount);
        Assert.AreEqual(CurlGlobalFlags.Default, library.Flags);

        await library.DisposeAsync();

        Assert.AreEqual(1, engine.DisposeCount);
    }

    [TestMethod]
    public async Task InitMemory_AcceptsTheCallbacksAndNeverCallsThem()
    {
        var called = false;

        await using var library = CurlGlobal.InitMemory(
            new FakeCurlEngine(),
            CurlGlobalFlags.Ssl,
            _ => { called = true; return 0; },
            _ => called = true,
            (_, _) => { called = true; return 0; },
            _ => { called = true; return 0; },
            (_, _) => { called = true; return 0; });
        _ = library.CreateEasyTransfer();

        Assert.IsFalse(called);
        Assert.AreEqual(CurlGlobalFlags.Ssl, library.Flags);
    }

    [TestMethod]
    public void InitMemory_CountsOneInit()
    {
        var counter = new GlobalInitCounter();

        _ = CurlGlobal.InitMemoryCounting(new FakeCurlEngine(), CurlGlobalFlags.Default, new MemoryCallbacks(Malloc, Free, Realloc, Strdup, Calloc), counter);

        Assert.AreEqual(1, counter.Count);
    }

    [TestMethod]
    [DataRow("malloc")]
    [DataRow("free")]
    [DataRow("realloc")]
    [DataRow("strdup")]
    [DataRow("calloc")]
    public void InitMemory_WithAMissingCallback_ThrowsNamingIt_AndCountsNothing(string missing)
    {
        var counter = new GlobalInitCounter();
        var callbacks = new MemoryCallbacks(
            missing == "malloc" ? null! : Malloc,
            missing == "free" ? null! : Free,
            missing == "realloc" ? null! : Realloc,
            missing == "strdup" ? null! : Strdup,
            missing == "calloc" ? null! : Calloc);

        var thrown = Assert.ThrowsExactly<ArgumentNullException>(() => CurlGlobal.InitMemoryCounting(new FakeCurlEngine(), CurlGlobalFlags.Default, callbacks, counter));

        Assert.AreEqual(missing, thrown.ParamName);
        Assert.AreEqual(0, counter.Count);
    }

    [TestMethod]
    public void InitMemory_ThePublicOverloadChecksTheCallbacks()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => CurlGlobal.InitMemory(new FakeCurlEngine(), CurlGlobalFlags.Default, Malloc, null!, Realloc, Strdup, Calloc));
    }

    [TestMethod]
    [DataRow(CurlSslBackendId.Schannel, null, CurlSslSet.Ok)]
    [DataRow(CurlSslBackendId.None, "schannel", CurlSslSet.Ok)]
    [DataRow(CurlSslBackendId.None, "SChannel", CurlSslSet.Ok)]
    [DataRow(CurlSslBackendId.Schannel, "openssl", CurlSslSet.Ok)]
    [DataRow(CurlSslBackendId.OpenSsl, null, CurlSslSet.TooLate)]
    [DataRow(CurlSslBackendId.None, "openssl", CurlSslSet.TooLate)]
    [DataRow(CurlSslBackendId.None, null, CurlSslSet.TooLate)]
    [DataRow(CurlSslBackendId.Rustls, "nosuch", CurlSslSet.TooLate)]
    public void SslSet_TheSchannelBuild_AnswersOkOnlyForSchannel(CurlSslBackendId id, string? name, CurlSslSet expected)
    {
        var answer = CurlGlobal.SslSetFor(id, name, CurlSslBackend.For(windows: true), out var available);

        Assert.AreEqual(expected, answer);
        Assert.HasCount(1, available);
        Assert.AreEqual(new CurlSslBackend(CurlSslBackendId.Schannel, "schannel"), available[0]);
    }

    [TestMethod]
    [DataRow(CurlSslBackendId.OpenSsl, null, CurlSslSet.Ok)]
    [DataRow(CurlSslBackendId.None, "OpenSSL", CurlSslSet.Ok)]
    [DataRow(CurlSslBackendId.Schannel, null, CurlSslSet.TooLate)]
    public void SslSet_TheOpenSslBuild_AnswersOkOnlyForOpenSsl(CurlSslBackendId id, string? name, CurlSslSet expected)
    {
        var answer = CurlGlobal.SslSetFor(id, name, CurlSslBackend.For(windows: false), out var available);

        Assert.AreEqual(expected, answer);
        Assert.AreEqual(new CurlSslBackend(CurlSslBackendId.OpenSsl, "openssl"), available[0]);
    }

    [TestMethod]
    public void SslSet_ThePublicOverload_AnswersForThisPlatformsBuild()
    {
        var expectedId = OperatingSystem.IsWindows() ? CurlSslBackendId.Schannel : CurlSslBackendId.OpenSsl;

        var answer = CurlGlobal.SslSet(expectedId, null, out var available);

        Assert.AreEqual(CurlSslSet.Ok, answer);
        Assert.AreEqual(expectedId, available[0].Id);
    }

    [TestMethod]
    [DataRow(null)]
    [DataRow("")]
    [DataRow("all")]
    [DataRow("-all,+http/2, tls")]
    [DataRow("no-such-component")]
    public void Trace_AnswersOkForAnyConfiguration(string? configuration)
    {
        Assert.AreEqual(CurlCode.Ok, CurlGlobal.Trace(configuration));
    }
}
