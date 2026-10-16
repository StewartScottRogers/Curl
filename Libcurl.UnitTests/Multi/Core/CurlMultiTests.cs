using Curl.Api.Codes;
using Curl.Api.EngineContracts.Multi;
using Libcurl.Easy;
using Libcurl.Global;
using Libcurl.Multi;
using Libcurl.Options;
using Libcurl.UnitTests.Easy;
using Libcurl.UnitTests.Global;

namespace Libcurl.UnitTests.Multi.Core;

[TestClass]
public sealed class CurlMultiTests
{
    [TestMethod]
    public async Task AddHandle_AddsTheEasyHandlesTransferToTheEngine()
    {
        var engine = new FakeMultiTransfer();
        await using var multi = new CurlMulti(engine);
        var transfer = new FakeEasyTransfer();
        using var easy = NewEasy(transfer);

        Assert.AreEqual(CurlMultiCode.Ok, multi.AddHandle(easy));

        Assert.AreSame(transfer, engine.Added.Single());
    }

    [TestMethod]
    public async Task AddHandle_AHandleAddedAlready_AnswersAddedAlreadyWithoutAskingTheEngine()
    {
        var engine = new FakeMultiTransfer();
        await using var multi = new CurlMulti(engine);
        using var easy = NewEasy(new FakeEasyTransfer());
        multi.AddHandle(easy);

        Assert.AreEqual(CurlMultiCode.AddedAlready, multi.AddHandle(easy));

        Assert.HasCount(1, engine.Added);
    }

    [TestMethod]
    public async Task AddHandle_TheEngineRefuses_AnswersItsCodeAndHoldsNothing()
    {
        var engine = new FakeMultiTransfer { AddResult = CurlMultiCode.AddedAlready };
        await using var multi = new CurlMulti(engine);
        using var easy = NewEasy(new FakeEasyTransfer());

        Assert.AreEqual(CurlMultiCode.AddedAlready, multi.AddHandle(easy));
        engine.AddResult = CurlMultiCode.Ok;
        Assert.AreEqual(CurlMultiCode.Ok, multi.AddHandle(easy));

        Assert.HasCount(2, engine.Added);
    }

    [TestMethod]
    public async Task AddHandle_ANullOrCleanedUpHandle_AnswersBadEasyHandle()
    {
        var engine = new FakeMultiTransfer();
        await using var multi = new CurlMulti(engine);
        var easy = NewEasy(new FakeEasyTransfer());
        easy.Dispose();

        Assert.AreEqual(CurlMultiCode.BadEasyHandle, multi.AddHandle(null));
        Assert.AreEqual(CurlMultiCode.BadEasyHandle, multi.AddHandle(easy));

        Assert.IsEmpty(engine.Added);
    }

    [TestMethod]
    public async Task RemoveHandle_RemovesTheTransferSoItCanBeAddedAgain()
    {
        var engine = new FakeMultiTransfer();
        await using var multi = new CurlMulti(engine);
        var transfer = new FakeEasyTransfer();
        using var easy = NewEasy(transfer);
        multi.AddHandle(easy);

        Assert.AreEqual(CurlMultiCode.Ok, multi.RemoveHandle(easy));
        Assert.AreEqual(CurlMultiCode.Ok, multi.AddHandle(easy));

        Assert.AreSame(transfer, engine.Removed.Single());
        Assert.HasCount(2, engine.Added);
    }

    [TestMethod]
    public async Task RemoveHandle_TheEngineRefuses_AnswersItsCodeAndKeepsTheHandle()
    {
        var engine = new FakeMultiTransfer { RemoveResult = CurlMultiCode.BadEasyHandle };
        await using var multi = new CurlMulti(engine);
        using var easy = NewEasy(new FakeEasyTransfer());
        multi.AddHandle(easy);

        Assert.AreEqual(CurlMultiCode.BadEasyHandle, multi.RemoveHandle(easy));

        Assert.AreEqual(CurlMultiCode.AddedAlready, multi.AddHandle(easy));
    }

    [TestMethod]
    public async Task RemoveHandle_ANullOrCleanedUpHandle_AnswersBadEasyHandle()
    {
        var engine = new FakeMultiTransfer();
        await using var multi = new CurlMulti(engine);
        var easy = NewEasy(new FakeEasyTransfer());
        easy.Dispose();

        Assert.AreEqual(CurlMultiCode.BadEasyHandle, multi.RemoveHandle(null));
        Assert.AreEqual(CurlMultiCode.BadEasyHandle, multi.RemoveHandle(easy));

        Assert.IsEmpty(engine.Removed);
    }

    [TestMethod]
    public async Task PerformAsync_AnswersTheEnginesCodeAndRunningCountAndPassesTheToken()
    {
        var engine = new FakeMultiTransfer { PerformResult = (CurlMultiCode.Ok, 2) };
        await using var multi = new CurlMulti(engine);
        using var source = new CancellationTokenSource();

        var (code, running) = await multi.PerformAsync(source.Token);

        Assert.AreEqual(CurlMultiCode.Ok, code);
        Assert.AreEqual(2, running);
        Assert.AreEqual(source.Token, engine.PerformedToken);
    }

    [TestMethod]
    public async Task InfoRead_MapsEachMessageToTheAddedEasyHandleThenAnswersNull()
    {
        var engine = new FakeMultiTransfer();
        await using var multi = new CurlMulti(engine);
        var first = new FakeEasyTransfer();
        var second = new FakeEasyTransfer();
        using var firstEasy = NewEasy(first);
        using var secondEasy = NewEasy(second);
        multi.AddHandle(firstEasy);
        multi.AddHandle(secondEasy);
        engine.Messages.Enqueue(new CurlMultiMessage(CurlMultiMessageType.Done, second, CurlCode.CouldntConnect));
        engine.Messages.Enqueue(new CurlMultiMessage(CurlMultiMessageType.Done, first, CurlCode.Ok));

        var one = multi.InfoRead(out var afterOne);
        var two = multi.InfoRead(out var afterTwo);
        var none = multi.InfoRead(out var afterNone);

        Assert.AreEqual(new CurlMessage(CurlMultiMessageType.Done, secondEasy, CurlCode.CouldntConnect), one);
        Assert.AreEqual(1, afterOne);
        Assert.AreEqual(new CurlMessage(CurlMultiMessageType.Done, firstEasy, CurlCode.Ok), two);
        Assert.AreEqual(0, afterTwo);
        Assert.IsNull(none);
        Assert.AreEqual(0, afterNone);
    }

    [TestMethod]
    public async Task DisposeAsync_DisposesTheEngineTransferOnceAndRefusesLaterCalls()
    {
        var engine = new FakeMultiTransfer();
        var multi = new CurlMulti(engine);
        using var easy = NewEasy(new FakeEasyTransfer());

        await multi.DisposeAsync();
        await multi.DisposeAsync();

        Assert.IsTrue(multi.IsCleanedUp);
        Assert.AreEqual(1, engine.DisposeCount);
        Assert.ThrowsExactly<ObjectDisposedException>(() => multi.AddHandle(easy));
        Assert.ThrowsExactly<ObjectDisposedException>(() => multi.RemoveHandle(easy));
        Assert.ThrowsExactly<ObjectDisposedException>(() => multi.PerformAsync());
        Assert.ThrowsExactly<ObjectDisposedException>(() => multi.InfoRead(out _));
    }

    [TestMethod]
    public async Task MultiInit_WrapsTheEnginesNewMultiTransfer()
    {
        var engine = new FakeCurlEngine();
        await using var library = CurlGlobal.InitCounting(engine, CurlGlobalFlags.Default, new GlobalInitCounter());

        var multi = library.MultiInit();

        Assert.IsFalse(multi.IsCleanedUp);
        Assert.IsInstanceOfType<IMultiTransfer>(engine.Created.Single());
    }

    private static CurlEasy NewEasy(FakeEasyTransfer transfer) => new(transfer, EasyOptionDispatch.Default);
}
