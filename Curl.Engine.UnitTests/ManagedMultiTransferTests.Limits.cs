using Curl.Api.Callbacks;
using Curl.Api.Codes;
using Curl.Api.EngineContracts;
using Curl.Api.EngineContracts.Multi;
using Curl.Api.Options;
using Curl.Api.Options.Http;
using Curl.Api.Options.Network;

namespace Curl.Engine;

/// <summary>
/// The multi handle's <c>CURLMOPT_PIPELINING</c>, <c>MAX_HOST_CONNECTIONS</c>, <c>MAX_TOTAL_CONNECTIONS</c>,
/// <c>MAX_CONCURRENT_STREAMS</c> and <c>PUSHFUNCTION</c> (BL-2428, ADR-0605).
/// </summary>
public sealed partial class ManagedMultiTransferTests
{
    [TestMethod]
    public async Task PerformAsync_MaxTotalConnectionsReached_HoldsTheNextTransferPendingUntilOneFinishes()
    {
        await using ManagedCurlEngine engine = new();
        await using IMultiTransfer multi = engine.CreateMulti();
        multi.Options = new MultiOptions { MaxTotalConnections = 1 };
        ControlledEasyTransfer first = To("http://a.example/"), second = To("http://b.example/");
        multi.Add(first);
        multi.Add(second);

        (CurlMultiCode code, int running) = await multi.PerformAsync(CancellationToken.None);
        int secondPerformsWhileHeld = second.PerformCount;
        (CurlMultiCode, long) pendingWhileHeld = multi.GetInfo(CurlMultiInfo.TransfersPending);
        (CurlMultiCode, long) runningWhileHeld = multi.GetInfo(CurlMultiInfo.TransfersRunning);
        (CurlMultiCode, long) timeoutWhileHeld = multi.GetTimeout();
        first.Finish(CurlCode.Ok);
        await multi.PerformAsync(CancellationToken.None);

        Assert.AreEqual((CurlMultiCode.Ok, 2), (code, running));
        Assert.AreEqual(1, first.PerformCount);
        Assert.AreEqual(0, secondPerformsWhileHeld);
        Assert.AreEqual((CurlMultiCode.Ok, 1L), pendingWhileHeld);
        Assert.AreEqual((CurlMultiCode.Ok, 1L), runningWhileHeld);
        Assert.AreEqual((CurlMultiCode.Ok, -1L), timeoutWhileHeld);
        Assert.AreEqual(1, second.PerformCount);
        Assert.AreEqual((CurlMultiCode.Ok, 0L), multi.GetInfo(CurlMultiInfo.TransfersPending));
    }

    [TestMethod]
    public async Task PerformAsync_MaxHostConnectionsReached_HoldsOnlyTheTransferToThatHost()
    {
        await using ManagedCurlEngine engine = new();
        await using IMultiTransfer multi = engine.CreateMulti();
        multi.Options = new MultiOptions { MaxHostConnections = 1 };
        ControlledEasyTransfer firstToA = To("http://a.example/1"), secondToA = To("HTTP://A.example:80/2"), toB = To("http://b.example/");
        multi.Add(firstToA);
        multi.Add(secondToA);
        multi.Add(toB);

        await multi.PerformAsync(CancellationToken.None);

        Assert.AreEqual(1, firstToA.PerformCount);
        Assert.AreEqual(0, secondToA.PerformCount);
        Assert.AreEqual(1, toB.PerformCount);
        Assert.AreEqual((CurlMultiCode.Ok, 1L), multi.GetInfo(CurlMultiInfo.TransfersPending));
    }

    [TestMethod]
    public async Task PerformAsync_PendingTransfer_IsNotToldToTheSocketCallbackUntilItStarts()
    {
        await using ManagedCurlEngine engine = new();
        await using SocketHoldingTransfer first = HoldingSocket(engine, 0x1001, UntilReadable, "http://a.example/");
        await using SocketHoldingTransfer second = HoldingSocket(engine, 0x1002, UntilReadable, "http://a.example/");
        await using ManagedMultiTransfer multi = (ManagedMultiTransfer)engine.CreateMulti();
        List<(IEasyTransfer Easy, nint Socket)> told = [];
        multi.Options = new MultiOptions
        {
            MaxTotalConnections = 1,
            Socket = (easy, socket, what, _) => { told.Add((easy, socket.Handle)); return 0; },
        };

        // The action lets the woken transfer run on to its end; a busy runner can take longer than the 200 ms default (BL-4050).
        multi.SocketWaitSettleLimit = TimeSpan.FromSeconds(30);
        multi.Add(first);
        multi.Add(second);

        await multi.SocketActionAsync(CurlMultiSockets.Timeout, CurlSocketSelect.None, CancellationToken.None);
        int toldWhileHeld = told.Count;
        await multi.SocketActionAsync(new CurlSocket(0x1001), CurlSocketSelect.In, CancellationToken.None);
        await multi.SocketActionAsync(CurlMultiSockets.Timeout, CurlSocketSelect.None, CancellationToken.None);

        Assert.AreEqual(1, toldWhileHeld);
        CollectionAssert.AreEqual(new (IEasyTransfer, nint)[] { (first, 0x1001), (first, 0x1001), (second, 0x1002) }, told, string.Join(" | ", told));
    }

    [TestMethod]
    [DataRow(HttpVersion.None, "https://h.example/", 2)]
    [DataRow(HttpVersion.Http2Tls, "https://h.example/", 2)]
    [DataRow(HttpVersion.Http11, "https://h.example/", 1)]
    [DataRow(HttpVersion.Http10, "https://h.example/", 1)]
    [DataRow(HttpVersion.Http2, "http://h.example/", 1)]
    [DataRow(HttpVersion.Http2PriorKnowledge, "http://h.example/", 2)]
    public async Task PerformAsync_MultiplexOneConnectionAllowed_SharesItOnlyWhereTheTransferCanMultiplex(HttpVersion version, string url, int expectedStarted)
    {
        await using ManagedCurlEngine engine = new();
        await using IMultiTransfer multi = engine.CreateMulti();
        multi.Options = new MultiOptions { MaxTotalConnections = 1, MaxConcurrentStreams = 2 };
        ControlledEasyTransfer[] transfers = [To(url, version), To(url, version), To(url, version)];
        foreach (ControlledEasyTransfer transfer in transfers)
        {
            multi.Add(transfer);
        }

        await multi.PerformAsync(CancellationToken.None);

        Assert.AreEqual(expectedStarted, transfers.Sum(transfer => transfer.PerformCount));
        Assert.AreEqual((CurlMultiCode.Ok, 3L - expectedStarted), multi.GetInfo(CurlMultiInfo.TransfersPending));
    }

    [TestMethod]
    [DataRow(CurlPipelining.Multiplex, true, HttpVersion.Http2, "http://h.example/", 2)]
    [DataRow(CurlPipelining.Multiplex, false, HttpVersion.Http2, "http://h.example/", 3)]
    [DataRow(CurlPipelining.Multiplex, true, HttpVersion.Http11, "http://h.example/", 3)]
    [DataRow(CurlPipelining.Multiplex, true, HttpVersion.Http2, "http://other.example/", 3)]
    [DataRow(CurlPipelining.Nothing, true, HttpVersion.Http2, "http://h.example/", 3)]
    public async Task GetWaitSockets_PipeWaitingTransferToARunningOneSOrigin_HoldsNoSocketOfItsOwnUnderMultiplex(CurlPipelining pipelining, bool pipeWait, HttpVersion version, string secondUrl, int expectedCount)
    {
        await using ManagedCurlEngine engine = new();
        await using SocketHoldingTransfer first = HoldingSocket(engine, 0x1001, UntilCancelled, "http://h.example/", version, pipeWait);
        await using SocketHoldingTransfer second = HoldingSocket(engine, 0x1002, UntilCancelled, secondUrl, version, pipeWait);
        await using IMultiTransfer multi = engine.CreateMulti();
        multi.Options = new MultiOptions { Pipelining = pipelining };
        multi.Add(first);
        multi.Add(second);
        await multi.PerformAsync(CancellationToken.None);
        await Task.WhenAll(first.SocketTaken.Task, second.SocketTaken.Task).WaitAsync(TimeSpan.FromSeconds(30));
        CurlWaitSocket[] sockets = new CurlWaitSocket[3];

        CurlMultiCode code = multi.GetWaitSockets(sockets, out int count);

        Assert.AreEqual((CurlMultiCode.Ok, expectedCount), (code, count));
        Assert.AreEqual((nint)0x1001, sockets[0].Socket.Handle);
        Assert.AreEqual(expectedCount == 3 ? 0x1002 : ManagedMultiTransfer.ResolverWakeupSocket, sockets[1].Socket.Handle);
    }

    [TestMethod]
    public async Task PerformAsync_OriginHeldByAnHttp1Connection_GivesAMultiplexingTransferNoStreamOnIt()
    {
        await using ManagedCurlEngine engine = new();
        await using IMultiTransfer multi = engine.CreateMulti();
        multi.Options = new MultiOptions { MaxHostConnections = 1 };
        ControlledEasyTransfer other = To("https://other.example/"), http1 = To("https://h.example/", HttpVersion.Http11), multiplexing = To("https://h.example/");
        multi.Add(other);
        multi.Add(http1);
        multi.Add(multiplexing);

        await multi.PerformAsync(CancellationToken.None);

        Assert.AreEqual(1, other.PerformCount);
        Assert.AreEqual(1, http1.PerformCount);
        Assert.AreEqual(0, multiplexing.PerformCount);
    }

    [TestMethod]
    public async Task PerformAsync_PipeliningWithoutMultiplex_GivesEachTransferItsOwnConnection()
    {
        await using ManagedCurlEngine engine = new();
        await using IMultiTransfer multi = engine.CreateMulti();
        multi.Options = new MultiOptions { MaxTotalConnections = 1, Pipelining = CurlPipelining.Http1 };
        ControlledEasyTransfer first = To("https://h.example/"), second = To("https://h.example/");
        multi.Add(first);
        multi.Add(second);

        await multi.PerformAsync(CancellationToken.None);

        Assert.AreEqual(1, first.PerformCount);
        Assert.AreEqual(0, second.PerformCount);
    }

    [TestMethod]
    public async Task PerformAsync_StreamsOfOneConnectionUsed_OpensASecondConnectionWithinTheHostLimit()
    {
        await using ManagedCurlEngine engine = new();
        await using IMultiTransfer multi = engine.CreateMulti();
        multi.Options = new MultiOptions { MaxHostConnections = 2, MaxConcurrentStreams = 1 };
        ControlledEasyTransfer[] transfers = [To("https://h.example/"), To("https://h.example/"), To("https://h.example/")];
        foreach (ControlledEasyTransfer transfer in transfers)
        {
            multi.Add(transfer);
        }

        await multi.PerformAsync(CancellationToken.None);

        CollectionAssert.AreEqual(new[] { 1, 1, 0 }, transfers.Select(transfer => transfer.PerformCount).ToArray());
    }

    [TestMethod]
    [DataRow(0L)]
    [DataRow(-5L)]
    [DataRow(1L + int.MaxValue)]
    public async Task PerformAsync_MaxConcurrentStreamsOutOfRange_UsesOneHundredStreams(long streams)
    {
        await using ManagedCurlEngine engine = new();
        await using IMultiTransfer multi = engine.CreateMulti();
        multi.Options = new MultiOptions { MaxTotalConnections = 1, MaxConcurrentStreams = streams };
        ControlledEasyTransfer[] transfers = [.. Enumerable.Range(0, 101).Select(_ => To("https://h.example/"))];
        foreach (ControlledEasyTransfer transfer in transfers)
        {
            multi.Add(transfer);
        }

        await multi.PerformAsync(CancellationToken.None);

        Assert.AreEqual(100, transfers.Sum(transfer => transfer.PerformCount));
        Assert.AreEqual(100L, ManagedMultiTransfer.DefaultConcurrentStreams);
    }

    [TestMethod]
    public async Task PerformAsync_UrlsThatDoNotParse_AreEachTheirOwnHostByTheirText()
    {
        await using ManagedCurlEngine engine = new();
        await using IMultiTransfer multi = engine.CreateMulti();
        multi.Options = new MultiOptions { MaxHostConnections = 1 };
        ControlledEasyTransfer noUrl = new(), otherNoUrl = new(), text = To("not a url");

        multi.Add(noUrl);
        multi.Add(otherNoUrl);
        multi.Add(text);
        await multi.PerformAsync(CancellationToken.None);

        CollectionAssert.AreEqual(new[] { 1, 0, 1 }, new[] { noUrl.PerformCount, otherNoUrl.PerformCount, text.PerformCount });
    }

    [TestMethod]
    public async Task PerformAsync_PendingTransferRemoved_LeavesNoMessageAndNeverStarts()
    {
        await using ManagedCurlEngine engine = new();
        await using IMultiTransfer multi = engine.CreateMulti();
        multi.Options = new MultiOptions { MaxTotalConnections = 1 };
        ControlledEasyTransfer first = To("http://a.example/"), second = To("http://a.example/");
        multi.Add(first);
        multi.Add(second);
        await multi.PerformAsync(CancellationToken.None);

        CurlMultiCode removed = multi.Remove(second);
        first.Finish(CurlCode.Ok);
        await multi.PerformAsync(CancellationToken.None);

        Assert.AreEqual(CurlMultiCode.Ok, removed);
        Assert.AreEqual(0, second.PerformCount);
        Assert.IsTrue(multi.TryReadInfo(out CurlMultiMessage? message, out int remaining));
        Assert.AreSame(first, message.Transfer);
        Assert.AreEqual(0, remaining);
    }

    [TestMethod]
    public async Task OfferPush_CallbackAnswersOk_AddsThePushedTransferWhichStartsOnTheNextPerform()
    {
        await using ManagedCurlEngine engine = new();
        await using ManagedMultiTransfer multi = (ManagedMultiTransfer)engine.CreateMulti();
        List<(IEasyTransfer Parent, IEasyTransfer Pushed, IReadOnlyList<string> Headers)> offers = [];
        multi.Options = new MultiOptions { Push = (parent, pushed, headers) => { offers.Add((parent, pushed, headers)); return CurlPushResult.Ok; } };
        ControlledEasyTransfer parent = To("https://h.example/"), pushed = To("https://h.example/style.css");
        multi.Add(parent);
        await multi.PerformAsync(CancellationToken.None);
        string[] headers = [":path: /style.css", ":authority: h.example"];

        CurlPushResult answer = multi.OfferPush(parent, pushed, headers);
        await multi.PerformAsync(CancellationToken.None);

        Assert.AreEqual(CurlPushResult.Ok, answer);
        Assert.HasCount(1, offers);
        Assert.AreSame(parent, offers[0].Parent);
        Assert.AreSame(pushed, offers[0].Pushed);
        CollectionAssert.AreEqual(headers, offers[0].Headers.ToArray());
        CollectionAssert.AreEqual(new IEasyTransfer[] { parent, pushed }, multi.GetHandles().ToArray());
        Assert.AreEqual(1, pushed.PerformCount);
    }

    [TestMethod]
    [DataRow(CurlPushResult.Deny)]
    [DataRow((CurlPushResult)7)]
    public async Task OfferPush_CallbackAnswersDenyOrAnyOtherValue_RefusesThePushAndLeavesTheParentRunning(CurlPushResult callbackAnswer)
    {
        await using ManagedCurlEngine engine = new();
        await using ManagedMultiTransfer multi = (ManagedMultiTransfer)engine.CreateMulti();
        multi.Options = new MultiOptions { Push = (_, _, _) => callbackAnswer };
        ControlledEasyTransfer parent = To("https://h.example/"), pushed = new();
        multi.Add(parent);
        await multi.PerformAsync(CancellationToken.None);

        CurlPushResult answer = multi.OfferPush(parent, pushed, []);
        (_, int running) = await multi.PerformAsync(CancellationToken.None);

        Assert.AreEqual(CurlPushResult.Deny, answer);
        CollectionAssert.AreEqual(new IEasyTransfer[] { parent }, multi.GetHandles().ToArray());
        Assert.AreEqual(1, running);
        Assert.AreEqual(0, pushed.PerformCount);
    }

    [TestMethod]
    public async Task OfferPush_CallbackAnswersErrorOut_RefusesThePushAndEndsTheParentWithHttp2()
    {
        await using ManagedCurlEngine engine = new();
        await using ManagedMultiTransfer multi = (ManagedMultiTransfer)engine.CreateMulti();
        multi.Options = new MultiOptions { Push = (_, _, _) => CurlPushResult.ErrorOut };
        ControlledEasyTransfer parent = To("https://h.example/"), pushed = new();
        multi.Add(parent);
        await multi.PerformAsync(CancellationToken.None);

        CurlPushResult answer = multi.OfferPush(parent, pushed, []);
        (_, int running) = await multi.PerformAsync(CancellationToken.None);

        Assert.AreEqual(CurlPushResult.ErrorOut, answer);
        Assert.AreEqual(0, running);
        Assert.IsTrue(parent.PerformToken.IsCancellationRequested);
        Assert.IsTrue(multi.TryReadInfo(out CurlMultiMessage? message, out _));
        Assert.AreEqual(new CurlMultiMessage(CurlMultiMessageType.Done, parent, CurlCode.Http2), message);
        CollectionAssert.AreEqual(new IEasyTransfer[] { parent }, multi.GetHandles().ToArray());
    }

    [TestMethod]
    public async Task OfferPush_NoPushCallback_DeniesThePush()
    {
        await using ManagedCurlEngine engine = new();
        await using ManagedMultiTransfer multi = (ManagedMultiTransfer)engine.CreateMulti();
        ControlledEasyTransfer parent = new();
        multi.Add(parent);

        Assert.AreEqual(CurlPushResult.Deny, multi.OfferPush(parent, new ControlledEasyTransfer(), []));
        Assert.HasCount(1, multi.GetHandles());
    }

    [TestMethod]
    public async Task OfferPush_ParentNotInTheHandlePushedAlreadyAddedOrNull_DeniesWithoutAskingTheCallback()
    {
        await using ManagedCurlEngine engine = new();
        await using ManagedMultiTransfer multi = (ManagedMultiTransfer)engine.CreateMulti();
        int asked = 0;
        multi.Options = new MultiOptions { Push = (_, _, _) => { asked++; return CurlPushResult.Ok; } };
        ControlledEasyTransfer parent = new(), added = new();
        multi.Add(parent);
        multi.Add(added);

        CurlPushResult notInHandle = multi.OfferPush(new ControlledEasyTransfer(), new ControlledEasyTransfer(), []);
        CurlPushResult alreadyAdded = multi.OfferPush(parent, added, []);
        CurlPushResult nullPushed = multi.OfferPush(parent, null!, []);

        Assert.AreEqual(CurlPushResult.Deny, notInHandle);
        Assert.AreEqual(CurlPushResult.Deny, alreadyAdded);
        Assert.AreEqual(CurlPushResult.Deny, nullPushed);
        Assert.AreEqual(0, asked);
    }

    [TestMethod]
    public async Task OfferPush_DisposedHandle_DeniesThePush()
    {
        await using ManagedCurlEngine engine = new();
        ManagedMultiTransfer multi = (ManagedMultiTransfer)engine.CreateMulti();
        multi.Options = new MultiOptions { Push = (_, _, _) => CurlPushResult.Ok };
        ControlledEasyTransfer parent = new();
        multi.Add(parent);
        await multi.DisposeAsync();

        Assert.AreEqual(CurlPushResult.Deny, multi.OfferPush(parent, new ControlledEasyTransfer(), []));
    }

    private static ControlledEasyTransfer To(string url, HttpVersion version = HttpVersion.None) => new()
    {
        Options = new EasyOptions
        {
            Network = new EasyNetworkOptions { Url = url },
            Http = new EasyHttpOptions { HttpVersion = version },
        },
    };
}
