using Curl.Api.Codes;
using Curl.Api.Options;
using Libcurl.Easy;
using Libcurl.Global;
using Libcurl.Options;
using Libcurl.UnitTests.Global;

namespace Libcurl.UnitTests.Easy;

[TestClass]
public sealed class CurlEasyTests
{
    private static readonly EasyOptionDispatch VerboseOnly = new(new Dictionary<CurlOption, EasyOptionSetter>
    {
        [CurlOption.Verbose] = (ref EasyOptions options, object? value) =>
        {
            options = options with { Behaviour = options.Behaviour with { Verbose = Convert.ToInt64(value) != 0 } };
            return CurlCode.Ok;
        },
    });

    [TestMethod]
    public void Perform_ReturnsTheEnginesCodeAndPassesTheHandlesOptions()
    {
        var transfer = new FakeEasyTransfer { Result = CurlCode.CouldntConnect };
        using var easy = new CurlEasy(transfer, VerboseOnly);

        Assert.AreEqual(CurlCode.Ok, easy.SetOption(CurlOption.Verbose, 1L));
        var code = easy.Perform();

        Assert.AreEqual(CurlCode.CouldntConnect, code);
        Assert.IsTrue(transfer.PerformedWith!.Behaviour.Verbose);
        Assert.AreSame(transfer.PerformedWith, easy.Options);
        Assert.AreEqual(CancellationToken.None, transfer.PerformedToken);
    }

    [TestMethod]
    public async Task PerformAsync_ReturnsTheEnginesCodeAndPassesTheToken()
    {
        var transfer = new FakeEasyTransfer { Result = CurlCode.RecvError };
        await using var easy = new CurlEasy(transfer, VerboseOnly);
        using var source = new CancellationTokenSource();

        var code = await easy.PerformAsync(source.Token);

        Assert.AreEqual(CurlCode.RecvError, code);
        Assert.AreEqual(source.Token, transfer.PerformedToken);
        Assert.AreSame(transfer.Options, transfer.PerformedWith);
    }

    [TestMethod]
    public void SetOption_AFailingCode_LeavesTheOptionsUnchanged()
    {
        var transfer = new FakeEasyTransfer();
        var before = transfer.Options;
        using var easy = new CurlEasy(transfer, VerboseOnly);

        Assert.AreEqual(CurlCode.NotBuiltIn, easy.SetOption(CurlOption.Url, "http://example.com/"));
        Assert.AreEqual(CurlCode.BadFunctionArgument, easy.SetOption(CurlOption.Verbose, "1"));

        Assert.AreSame(before, transfer.Options);
    }

    [TestMethod]
    public void Dispose_DisposesTheTransferOnce()
    {
        var transfer = new FakeEasyTransfer();
        var easy = new CurlEasy(transfer, VerboseOnly);

        easy.Dispose();
        easy.Dispose();

        Assert.AreEqual(1, transfer.DisposeCount);
        Assert.IsTrue(easy.IsCleanedUp);
    }

    [TestMethod]
    public async Task DisposeAsync_DisposesTheTransferOnce()
    {
        var transfer = new FakeEasyTransfer();
        var easy = new CurlEasy(transfer, VerboseOnly);
        Assert.IsFalse(easy.IsCleanedUp);

        await easy.DisposeAsync();
        await easy.DisposeAsync();

        Assert.AreEqual(1, transfer.DisposeCount);
    }

    [TestMethod]
    public void Members_AfterCleanup_ThrowObjectDisposed()
    {
        var transfer = new FakeEasyTransfer();
        var easy = new CurlEasy(transfer, VerboseOnly);
        easy.Dispose();

        Assert.ThrowsExactly<ObjectDisposedException>(() => easy.SetOption(CurlOption.Verbose, 1L));
        Assert.ThrowsExactly<ObjectDisposedException>(() => easy.Perform());
        Assert.ThrowsExactly<ObjectDisposedException>(() => easy.PerformAsync());
        Assert.ThrowsExactly<ObjectDisposedException>(() => easy.Options);
        Assert.IsNull(transfer.PerformedWith);
    }

    [TestMethod]
    public void Reset_AfterSetOption_RestoresTheDefaultOptionsAndKeepsTheConnection()
    {
        var transfer = new FakeEasyTransfer { OpenConnection = "connection #0" };
        using var easy = new CurlEasy(transfer, VerboseOnly);
        Assert.AreEqual(CurlCode.Ok, easy.SetOption(CurlOption.Verbose, 1L));

        easy.Reset();

        Assert.AreEqual(1, transfer.ResetCount);
        Assert.AreEqual(new EasyOptions(), easy.Options);
        Assert.IsFalse(easy.Options.Behaviour.Verbose);
        Assert.AreEqual("connection #0", transfer.OpenConnection);
    }

    [TestMethod]
    public void DupHandle_CarriesTheOptionsButNotTheConnection()
    {
        var transfer = new FakeEasyTransfer { OpenConnection = "connection #0" };
        using var easy = new CurlEasy(transfer, VerboseOnly);
        Assert.AreEqual(CurlCode.Ok, easy.SetOption(CurlOption.Verbose, 1L));

        using var duplicate = easy.DupHandle();

        Assert.AreNotSame(easy, duplicate);
        Assert.IsTrue(duplicate.Options.Behaviour.Verbose);
        Assert.AreEqual(easy.Options, duplicate.Options);
        Assert.AreNotSame(transfer, duplicate.Transfer);
        Assert.IsNull(((FakeEasyTransfer)duplicate.Transfer).OpenConnection);
        Assert.IsFalse(duplicate.IsCleanedUp);
    }

    [TestMethod]
    public void DupHandle_ThenSetOptionOnTheDuplicate_LeavesTheOriginalUnchanged()
    {
        using var easy = new CurlEasy(new FakeEasyTransfer(), VerboseOnly);
        using var duplicate = easy.DupHandle();

        Assert.AreEqual(CurlCode.Ok, duplicate.SetOption(CurlOption.Verbose, 1L));

        Assert.IsTrue(duplicate.Options.Behaviour.Verbose);
        Assert.IsFalse(easy.Options.Behaviour.Verbose);
    }

    [TestMethod]
    public void DupHandle_CleanupOfTheDuplicate_LeavesTheOriginalLive()
    {
        var transfer = new FakeEasyTransfer();
        using var easy = new CurlEasy(transfer, VerboseOnly);
        var duplicate = easy.DupHandle();

        duplicate.Dispose();

        Assert.IsTrue(duplicate.IsCleanedUp);
        Assert.IsFalse(easy.IsCleanedUp);
        Assert.AreEqual(0, transfer.DisposeCount);
    }

    [TestMethod]
    public void ResetAndDupHandle_AfterCleanup_ThrowObjectDisposed()
    {
        var transfer = new FakeEasyTransfer();
        var easy = new CurlEasy(transfer, VerboseOnly);
        easy.Dispose();

        Assert.ThrowsExactly<ObjectDisposedException>(easy.Reset);
        Assert.ThrowsExactly<ObjectDisposedException>(() => easy.DupHandle());
        Assert.AreEqual(0, transfer.ResetCount);
    }

    [TestMethod]
    public async Task EasyInit_WrapsTheEnginesNewTransfer()
    {
        var engine = new FakeCurlEngine();
        await using var library = CurlGlobal.InitCounting(engine, CurlGlobalFlags.Default, new GlobalInitCounter());

        var easy = library.EasyInit();

        Assert.HasCount(1, engine.Created);
        Assert.IsFalse(easy.IsCleanedUp);
    }
}
