using Curl.Api.Callbacks;
using Curl.Api.Codes;
using Curl.Api.EngineContracts.Multi;
using Libcurl.Multi;

namespace Libcurl.UnitTests.Multi.Wait;

[TestClass]
public sealed class CurlMultiWaitTests
{
    [TestMethod]
    public async Task WaitAsync_HandsTheEngineTheSocketsTimeoutAndToken_AndAnswersItsResult()
    {
        var engine = new FakeWaitMultiTransfer { WaitResult = (CurlMultiCode.Ok, 1) };
        await using var multi = new CurlMulti(engine);
        var extra = new[] { new CurlWaitSocket(new CurlSocket(7), CurlWaitEvents.PollIn, CurlWaitEvents.None) };
        using var cancellation = new CancellationTokenSource();

        var result = await multi.WaitAsync(extra, 250, cancellation.Token);

        Assert.AreEqual((CurlMultiCode.Ok, 1), result);
        Assert.AreEqual(nameof(FakeWaitMultiTransfer.WaitAsync), engine.LastWait);
        Assert.AreEqual(TimeSpan.FromMilliseconds(250), engine.LastTimeout);
        Assert.AreEqual(cancellation.Token, engine.LastToken);
        Assert.AreEqual(CurlWaitEvents.PollIn, extra[0].ReturnedEvents);
    }

    [TestMethod]
    public async Task WaitAsync_ANegativeTimeout_AnswersBadFunctionArgumentWithoutTheEngine()
    {
        var engine = new FakeWaitMultiTransfer { WaitResult = (CurlMultiCode.Ok, 3) };
        await using var multi = new CurlMulti(engine);

        var result = await multi.WaitAsync(Memory<CurlWaitSocket>.Empty, -1);

        Assert.AreEqual((CurlMultiCode.BadFunctionArgument, 0), result);
        Assert.IsNull(engine.LastWait);
    }

    [TestMethod]
    public async Task PollAsync_HandsTheEngineTheSocketsAndTimeout_AndAnswersItsResult()
    {
        var engine = new FakeWaitMultiTransfer { WaitResult = (CurlMultiCode.UnrecoverablePoll, 0) };
        await using var multi = new CurlMulti(engine);
        var extra = new[] { new CurlWaitSocket(new CurlSocket(9), CurlWaitEvents.PollOut, CurlWaitEvents.None) };

        var result = await multi.PollAsync(extra, 0);

        Assert.AreEqual((CurlMultiCode.UnrecoverablePoll, 0), result);
        Assert.AreEqual(nameof(FakeWaitMultiTransfer.PollAsync), engine.LastWait);
        Assert.AreEqual(TimeSpan.Zero, engine.LastTimeout);
        Assert.AreEqual(CurlWaitEvents.PollOut, extra[0].ReturnedEvents);
    }

    [TestMethod]
    public async Task PollAsync_ANegativeTimeout_AnswersBadFunctionArgumentWithoutTheEngine()
    {
        var engine = new FakeWaitMultiTransfer();
        await using var multi = new CurlMulti(engine);

        var result = await multi.PollAsync(Memory<CurlWaitSocket>.Empty, int.MinValue);

        Assert.AreEqual((CurlMultiCode.BadFunctionArgument, 0), result);
        Assert.IsNull(engine.LastWait);
    }

    [TestMethod]
    public async Task Wakeup_AsksTheEngine_AndAnswersItsResult()
    {
        var engine = new FakeWaitMultiTransfer { WakeupResult = CurlMultiCode.WakeupFailure };
        await using var multi = new CurlMulti(engine);

        Assert.AreEqual(CurlMultiCode.WakeupFailure, multi.Wakeup());

        Assert.AreEqual(1, engine.WakeupCount);
    }

    [TestMethod]
    public async Task Timeout_AnswersTheEnginesMilliseconds()
    {
        var engine = new FakeWaitMultiTransfer { TimeoutResult = (CurlMultiCode.Ok, -1) };
        await using var multi = new CurlMulti(engine);

        Assert.AreEqual((CurlMultiCode.Ok, -1L), multi.Timeout());
        engine.TimeoutResult = (CurlMultiCode.Ok, 1500);
        Assert.AreEqual((CurlMultiCode.Ok, 1500L), multi.Timeout());
    }

    [TestMethod]
    public async Task FdSet_PutsEachSocketInTheSetsForItsEvents_AndAnswersTheHighest()
    {
        var engine = new FakeWaitMultiTransfer();
        engine.WaitSockets.Add(new CurlWaitSocket(new CurlSocket(12), CurlWaitEvents.PollIn, CurlWaitEvents.None));
        engine.WaitSockets.Add(new CurlWaitSocket(new CurlSocket(30), CurlWaitEvents.PollOut, CurlWaitEvents.None));
        engine.WaitSockets.Add(new CurlWaitSocket(new CurlSocket(5), CurlWaitEvents.PollIn | CurlWaitEvents.PollOut | CurlWaitEvents.PollPriority, CurlWaitEvents.None));
        await using var multi = new CurlMulti(engine);
        var read = new HashSet<CurlSocket> { new(99) };
        var write = new HashSet<CurlSocket>();
        var exception = new HashSet<CurlSocket>();

        Assert.AreEqual(CurlMultiCode.Ok, multi.FdSet(read, write, exception, out var maxSocket));

        CollectionAssert.AreEquivalent(new[] { new CurlSocket(99), new CurlSocket(12), new CurlSocket(5) }, read.ToArray());
        CollectionAssert.AreEquivalent(new[] { new CurlSocket(30), new CurlSocket(5) }, write.ToArray());
        CollectionAssert.AreEquivalent(new[] { new CurlSocket(5) }, exception.ToArray());
        Assert.AreEqual((nint)30, maxSocket);
    }

    [TestMethod]
    public async Task FdSet_NullSets_AreSkipped()
    {
        var engine = new FakeWaitMultiTransfer();
        engine.WaitSockets.Add(new CurlWaitSocket(new CurlSocket(4), CurlWaitEvents.PollIn | CurlWaitEvents.PollOut | CurlWaitEvents.PollPriority, CurlWaitEvents.None));
        await using var multi = new CurlMulti(engine);

        Assert.AreEqual(CurlMultiCode.Ok, multi.FdSet(null, null, null, out var maxSocket));

        Assert.AreEqual((nint)4, maxSocket);
    }

    [TestMethod]
    public async Task FdSet_NoSockets_ChangesNoSetAndAnswersMinusOne()
    {
        var engine = new FakeWaitMultiTransfer();
        await using var multi = new CurlMulti(engine);
        var read = new HashSet<CurlSocket>();

        Assert.AreEqual(CurlMultiCode.Ok, multi.FdSet(read, null, null, out var maxSocket));

        Assert.IsEmpty(read);
        Assert.AreEqual((nint)(-1), maxSocket);
        Assert.AreEqual(1, engine.WaitSocketsCalls);
    }

    [TestMethod]
    public async Task FdSet_TheEngineFails_AnswersItsCodeAndChangesNoSet()
    {
        var engine = new FakeWaitMultiTransfer { WaitSocketsResult = CurlMultiCode.InternalError };
        engine.WaitSockets.Add(new CurlWaitSocket(new CurlSocket(4), CurlWaitEvents.PollIn, CurlWaitEvents.None));
        await using var multi = new CurlMulti(engine);
        var read = new HashSet<CurlSocket>();

        Assert.AreEqual(CurlMultiCode.InternalError, multi.FdSet(read, null, null, out var maxSocket));

        Assert.IsEmpty(read);
        Assert.AreEqual((nint)(-1), maxSocket);
    }

    [TestMethod]
    public async Task FdSet_TheEngineFailsOnTheSecondRead_AnswersItsCodeAndChangesNoSet()
    {
        var engine = new FakeWaitMultiTransfer { WaitSocketsResult = CurlMultiCode.BadHandle, WaitSocketsResultFromCall = 2 };
        engine.WaitSockets.Add(new CurlWaitSocket(new CurlSocket(4), CurlWaitEvents.PollIn, CurlWaitEvents.None));
        await using var multi = new CurlMulti(engine);
        var read = new HashSet<CurlSocket>();

        Assert.AreEqual(CurlMultiCode.BadHandle, multi.FdSet(read, null, null, out var maxSocket));

        Assert.IsEmpty(read);
        Assert.AreEqual((nint)(-1), maxSocket);
    }

    [TestMethod]
    public async Task EveryMember_AfterCleanup_ThrowsObjectDisposedException()
    {
        var multi = new CurlMulti(new FakeWaitMultiTransfer());
        await multi.DisposeAsync();

        await Assert.ThrowsExactlyAsync<ObjectDisposedException>(async () => await multi.WaitAsync(Memory<CurlWaitSocket>.Empty, 0));
        await Assert.ThrowsExactlyAsync<ObjectDisposedException>(async () => await multi.PollAsync(Memory<CurlWaitSocket>.Empty, 0));
        Assert.ThrowsExactly<ObjectDisposedException>(() => multi.Wakeup());
        Assert.ThrowsExactly<ObjectDisposedException>(() => multi.Timeout());
        Assert.ThrowsExactly<ObjectDisposedException>(() => multi.FdSet(null, null, null, out _));
    }
}
