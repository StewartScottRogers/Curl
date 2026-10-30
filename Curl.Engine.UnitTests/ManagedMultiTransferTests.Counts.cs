using System.Collections.Concurrent;
using System.Net;
using Curl.Api.Callbacks;
using Curl.Api.Codes;
using Curl.Api.EngineContracts;
using Curl.Api.EngineContracts.Multi;
using Curl.Api.Options;
using Curl.Api.Options.Network;
using Curl.Networking;
using Curl.Protocol.Abstractions;

namespace Curl.Engine;

/// <summary>
/// The multi handle's get_handles, waitfds, get_offt, notifications and <c>CURLMOPT_NETWORK_CHANGED</c>
/// (BL-2427, ADR-0604).
/// </summary>
public sealed partial class ManagedMultiTransferTests
{
    [TestMethod]
    public async Task GetHandles_TransfersAddedAndOneRemoved_AreTheOnesStillAddedInAddOrder()
    {
        await using ManagedCurlEngine engine = new();
        await using IMultiTransfer multi = engine.CreateMulti();
        ControlledEasyTransfer first = new(), second = new(), third = new();
        multi.Add(first);
        multi.Add(second);
        multi.Add(third);

        IReadOnlyList<IEasyTransfer> beforeRemove = multi.GetHandles();
        multi.Remove(second);
        IReadOnlyList<IEasyTransfer> afterRemove = multi.GetHandles();

        CollectionAssert.AreEqual(new IEasyTransfer[] { first, second, third }, beforeRemove.ToArray());
        CollectionAssert.AreEqual(new IEasyTransfer[] { first, third }, afterRemove.ToArray());
    }

    [TestMethod]
    public async Task GetHandles_DisposedHandle_IsEmpty()
    {
        await using ManagedCurlEngine engine = new();
        IMultiTransfer multi = engine.CreateMulti();
        multi.Add(new ControlledEasyTransfer());

        await multi.DisposeAsync();

        Assert.IsEmpty(multi.GetHandles());
    }

    [TestMethod]
    public async Task GetWaitSockets_TransfersRunningWithNoSocket_GiveNoSocketAndNoResolverWakeup()
    {
        await using ManagedCurlEngine engine = new();
        await using IMultiTransfer multi = engine.CreateMulti();
        List<CurlSocket> told = [];
        multi.Options = new MultiOptions { Socket = (_, socket, _, _) => { told.Add(socket); return 0; } };
        multi.Add(new ControlledEasyTransfer());
        multi.Add(new ControlledEasyTransfer());
        await multi.SocketActionAsync(CurlMultiSockets.Timeout, CurlSocketSelect.None, CancellationToken.None);
        CurlWaitSocket[] sockets = new CurlWaitSocket[1];

        CurlMultiCode code = multi.GetWaitSockets(sockets, out int count);

        Assert.AreEqual(CurlMultiCode.Ok, code);
        Assert.AreEqual(0, count);
        Assert.AreEqual(default, sockets[0]);
        Assert.IsEmpty(told);
    }

    [TestMethod]
    public async Task GetWaitSockets_TransfersHoldingRealSockets_AreOnePollInSocketEachThenTheResolverWakeupAndATooShortArrayIsOutOfMemory()
    {
        TimerManualTimeProvider time = new();
        await using ManagedCurlEngine engine = new(time, null);
        await using ManagedMultiTransfer multi = NewMulti(time);
        await using WrappingEasyTransfer first = TransferHoldingSocketUntilCancelled(engine, 0x1234, out Task firstHolds);
        await using WrappingEasyTransfer second = TransferHoldingSocketUntilCancelled(engine, 0x1235, out Task secondHolds);
        multi.Add(first);
        multi.Add(second);
        await multi.PerformAsync(CancellationToken.None);
        await Task.WhenAll(firstHolds, secondHolds);
        CurlWaitSocket[] sockets = new CurlWaitSocket[4];
        CurlWaitSocket[] tooShort = new CurlWaitSocket[1];

        CurlMultiCode code = multi.GetWaitSockets(sockets, out int count);
        CurlMultiCode tooShortCode = multi.GetWaitSockets(tooShort, out int tooShortCount);
        CurlMultiCode emptyCode = multi.GetWaitSockets([], out int emptyCount);

        Assert.AreEqual((CurlMultiCode.Ok, 3), (code, count));
        Assert.AreEqual(new CurlWaitSocket(new CurlSocket(0x1234), CurlWaitEvents.PollIn, CurlWaitEvents.None), sockets[0]);
        Assert.AreEqual(new CurlWaitSocket(new CurlSocket(0x1235), CurlWaitEvents.PollIn, CurlWaitEvents.None), sockets[1]);
        Assert.AreEqual(new CurlWaitSocket(new CurlSocket(ManagedMultiTransfer.ResolverWakeupSocket), CurlWaitEvents.PollIn, CurlWaitEvents.None), sockets[2]);
        Assert.AreEqual(default, sockets[3]);
        Assert.AreEqual((CurlMultiCode.OutOfMemory, 3), (tooShortCode, tooShortCount));
        Assert.AreEqual(sockets[0], tooShort[0]);
        Assert.AreEqual((CurlMultiCode.OutOfMemory, 3), (emptyCode, emptyCount));
    }

    /// <summary>A wrapped managed transfer that takes <paramref name="socket" /> and holds it until cancelled.</summary>
    private static WrappingEasyTransfer TransferHoldingSocketUntilCancelled(ManagedCurlEngine engine, nint socket, out Task holding)
    {
        TaskCompletionSource holds = new(TaskCreationOptions.RunContinuationsAsynchronously);
        holding = holds.Task;
        return new WrappingEasyTransfer((ManagedEasyTransfer)engine.CreateEasy(), async (wait, token) =>
        {
            wait.TakeSocket(socket);
            holds.TrySetResult();
            await Task.Delay(Timeout.InfiniteTimeSpan, token);
            return CurlCode.Ok;
        });
    }

    [TestMethod]
    public async Task GetWaitSockets_DisposedHandle_IsBadHandleWithNoSockets()
    {
        await using ManagedCurlEngine engine = new();
        IMultiTransfer multi = engine.CreateMulti();
        await multi.DisposeAsync();

        CurlMultiCode code = multi.GetWaitSockets(new CurlWaitSocket[1], out int count);

        Assert.AreEqual(CurlMultiCode.BadHandle, code);
        Assert.AreEqual(0, count);
    }

    [TestMethod]
    public async Task GetInfo_TransfersAddedRunDoneAndRemoved_CountsEachAsCurl()
    {
        await using ManagedCurlEngine engine = new();
        await using IMultiTransfer multi = engine.CreateMulti();
        ControlledEasyTransfer done = new(), running = new(), removed = new(), notStarted = new();
        multi.Add(done);
        multi.Add(running);
        multi.Add(removed);
        await multi.PerformAsync(CancellationToken.None);
        done.Finish(CurlCode.Ok);
        await multi.PerformAsync(CancellationToken.None);
        multi.Remove(removed);
        multi.Add(notStarted);

        Assert.AreEqual((CurlMultiCode.Ok, 3L), multi.GetInfo(CurlMultiInfo.TransfersCurrent));
        Assert.AreEqual((CurlMultiCode.Ok, 2L), multi.GetInfo(CurlMultiInfo.TransfersRunning));
        Assert.AreEqual((CurlMultiCode.Ok, 0L), multi.GetInfo(CurlMultiInfo.TransfersPending));
        Assert.AreEqual((CurlMultiCode.Ok, 1L), multi.GetInfo(CurlMultiInfo.TransfersDone));
        Assert.AreEqual((CurlMultiCode.Ok, 4L), multi.GetInfo(CurlMultiInfo.TransfersAdded));
    }

    [TestMethod]
    public async Task GetInfo_NoneOrAnUnknownNumber_IsUnknownOption()
    {
        await using ManagedCurlEngine engine = new();
        await using IMultiTransfer multi = engine.CreateMulti();

        Assert.AreEqual((CurlMultiCode.UnknownOption, 0L), multi.GetInfo(CurlMultiInfo.None));
        Assert.AreEqual((CurlMultiCode.UnknownOption, 0L), multi.GetInfo((CurlMultiInfo)6));
    }

    [TestMethod]
    public async Task GetInfo_DisposedHandle_IsBadHandle()
    {
        await using ManagedCurlEngine engine = new();
        IMultiTransfer multi = engine.CreateMulti();
        await multi.DisposeAsync();

        Assert.AreEqual((CurlMultiCode.BadHandle, 0L), multi.GetInfo(CurlMultiInfo.TransfersAdded));
    }

    [TestMethod]
    public async Task EnableNotification_BothOn_SendsInfoReadForTheFirstQueuedMessageThenEasyDoneForEachTransfer()
    {
        await using ManagedCurlEngine engine = new();
        await using IMultiTransfer multi = engine.CreateMulti();
        List<(CurlMultiNotification, IEasyTransfer)> sent = [];
        multi.Options = new MultiOptions { Notify = (notification, transfer) => sent.Add((notification, transfer)) };
        ControlledEasyTransfer first = new(), second = new(), third = new(), fourth = new();
        multi.Add(first);
        multi.Add(second);
        multi.Add(third);
        multi.Add(fourth);

        CurlMultiCode enableInfoRead = multi.EnableNotification(CurlMultiNotification.InfoRead);
        CurlMultiCode enableEasyDone = multi.EnableNotification(CurlMultiNotification.EasyDone);
        await multi.PerformAsync(CancellationToken.None);
        first.Finish(CurlCode.Ok);
        second.Finish(CurlCode.CouldntConnect);
        await multi.PerformAsync(CancellationToken.None);
        int afterTwo = sent.Count;
        third.Finish(CurlCode.Ok);
        await multi.PerformAsync(CancellationToken.None);
        int afterThirdUnread = sent.Count;
        while (multi.TryReadInfo(out _, out _))
        {
        }

        fourth.Finish(CurlCode.Ok);
        await multi.SocketActionAsync(CurlMultiSockets.Timeout, CurlSocketSelect.None, CancellationToken.None);

        Assert.AreEqual(CurlMultiCode.Ok, enableInfoRead);
        Assert.AreEqual(CurlMultiCode.Ok, enableEasyDone);
        Assert.AreEqual(3, afterTwo);
        Assert.AreEqual(4, afterThirdUnread);
        CollectionAssert.AreEqual(
            new (CurlMultiNotification, IEasyTransfer)[]
            {
                (CurlMultiNotification.InfoRead, first),
                (CurlMultiNotification.EasyDone, first),
                (CurlMultiNotification.EasyDone, second),
                (CurlMultiNotification.EasyDone, third),
                (CurlMultiNotification.InfoRead, fourth),
                (CurlMultiNotification.EasyDone, fourth),
            },
            sent);
    }

    [TestMethod]
    public async Task EnableNotification_NoneEnabledOrDisabledAgain_SendsNothing()
    {
        await using ManagedCurlEngine engine = new();
        await using IMultiTransfer multi = engine.CreateMulti();
        List<CurlMultiNotification> sent = [];
        multi.Options = new MultiOptions { Notify = (notification, _) => sent.Add(notification) };
        ControlledEasyTransfer first = new(), second = new();
        multi.Add(first);
        multi.Add(second);
        first.Finish(CurlCode.Ok);
        await multi.PerformAsync(CancellationToken.None);

        multi.EnableNotification(CurlMultiNotification.EasyDone);
        CurlMultiCode disabled = multi.DisableNotification(CurlMultiNotification.EasyDone);
        second.Finish(CurlCode.Ok);
        await multi.PerformAsync(CancellationToken.None);

        Assert.AreEqual(CurlMultiCode.Ok, disabled);
        Assert.IsEmpty(sent);
    }

    [TestMethod]
    public async Task EnableNotification_NoNotifyCallback_FinishesTheTransferWithoutOne()
    {
        await using ManagedCurlEngine engine = new();
        await using IMultiTransfer multi = engine.CreateMulti();
        ControlledEasyTransfer transfer = new();
        multi.Add(transfer);
        multi.EnableNotification(CurlMultiNotification.EasyDone);
        await multi.PerformAsync(CancellationToken.None);
        transfer.Finish(CurlCode.Ok);

        (CurlMultiCode code, int running) = await multi.PerformAsync(CancellationToken.None);

        Assert.AreEqual((CurlMultiCode.Ok, 0), (code, running));
        Assert.IsTrue(multi.TryReadInfo(out _, out _));
    }

    [TestMethod]
    public async Task EnableNotification_AbortedByCallback_SendsEasyDoneForEachTransferItEnds()
    {
        TimerManualTimeProvider time = new();
        await using ManagedCurlEngine engine = new(time, null);
        await using ManagedMultiTransfer multi = NewMulti(time);
        List<(CurlMultiNotification, IEasyTransfer)> sent = [];
        await using WrappingEasyTransfer transfer = ReadableWaitingTransfer(engine);
        multi.Options = new MultiOptions
        {
            Socket = (_, _, _, _) => -1,
            Notify = (notification, easy) => sent.Add((notification, easy)),
        };
        multi.SocketWaitSettleLimit = TimeSpan.FromSeconds(30);
        multi.EnableNotification(CurlMultiNotification.EasyDone);
        multi.Add(transfer);
        await multi.SocketActionAsync(CurlMultiSockets.Timeout, CurlSocketSelect.None, CancellationToken.None);

        await multi.PerformAsync(CancellationToken.None);

        CollectionAssert.AreEqual(new (CurlMultiNotification, IEasyTransfer)[] { (CurlMultiNotification.EasyDone, transfer) }, sent);
    }

    [TestMethod]
    [DataRow(-1)]
    [DataRow(32)]
    public async Task EnableNotification_NumberOutsideZeroToThirtyOne_IsUnknownOption(int number)
    {
        await using ManagedCurlEngine engine = new();
        await using IMultiTransfer multi = engine.CreateMulti();

        Assert.AreEqual(CurlMultiCode.UnknownOption, multi.EnableNotification((CurlMultiNotification)number));
        Assert.AreEqual(CurlMultiCode.UnknownOption, multi.DisableNotification((CurlMultiNotification)number));
        Assert.AreEqual(CurlMultiCode.Ok, multi.EnableNotification((CurlMultiNotification)31));
    }

    [TestMethod]
    public async Task EnableNotification_DisposedHandle_IsBadHandle()
    {
        await using ManagedCurlEngine engine = new();
        IMultiTransfer multi = engine.CreateMulti();
        await multi.DisposeAsync();

        Assert.AreEqual(CurlMultiCode.BadHandle, multi.EnableNotification(CurlMultiNotification.InfoRead));
        Assert.AreEqual(CurlMultiCode.BadHandle, multi.DisableNotification(CurlMultiNotification.InfoRead));
    }

    [TestMethod]
    [DataRow(CurlNetworkChange.ClearConnections, 1, 0)]
    [DataRow(CurlNetworkChange.ClearDns, 0, 1)]
    [DataRow(CurlNetworkChange.ClearConnections | CurlNetworkChange.ClearDns, 1, 1)]
    public async Task Options_NetworkChanged_ClearsWhatItNamesOnceAndIsKeptAsNone(CurlNetworkChange change, int expectedConnectionClears, int expectedDnsClears)
    {
        int connectionClears = 0, dnsClears = 0;
        await using ManagedMultiTransfer multi = new(
            new ConcurrentDictionary<IEasyTransfer, ManagedMultiTransfer>(ReferenceEqualityComparer.Instance),
            TimeProvider.System,
            new ScriptedWaitSocketPoller(),
            () => { connectionClears++; return ValueTask.CompletedTask; },
            _ => ValueTask.CompletedTask,
            () => dnsClears++);

        multi.Options = new MultiOptions { NetworkChanged = change, MaxConnects = 7 };
        multi.Options = multi.Options with { MaxHostConnections = 2 };

        Assert.AreEqual(expectedConnectionClears, connectionClears);
        Assert.AreEqual(expectedDnsClears, dnsClears);
        Assert.AreEqual(new MultiOptions { MaxConnects = 7, MaxHostConnections = 2 }, multi.Options);
    }

    [TestMethod]
    public async Task Options_ClearConnections_DisposeWaitsForTheIdleConnectionsToClose()
    {
        TaskCompletionSource closing = new(TaskCreationOptions.RunContinuationsAsynchronously);
        ManagedMultiTransfer multi = new(
            new ConcurrentDictionary<IEasyTransfer, ManagedMultiTransfer>(ReferenceEqualityComparer.Instance),
            TimeProvider.System,
            new ScriptedWaitSocketPoller(),
            () => new ValueTask(closing.Task),
            _ => ValueTask.CompletedTask,
            () => { });
        multi.Options = new MultiOptions { NetworkChanged = CurlNetworkChange.ClearConnections };

        Task dispose = multi.DisposeAsync().AsTask();
        bool doneBeforeClose = dispose.IsCompleted;
        closing.SetResult();
        await dispose;

        Assert.IsFalse(doneBeforeClose);
    }

    [TestMethod]
    public async Task Options_Null_ThrowsArgumentNull()
    {
        await using ManagedCurlEngine engine = new();
        await using IMultiTransfer multi = engine.CreateMulti();

        Assert.ThrowsExactly<ArgumentNullException>(() => multi.Options = null!);
    }

    [TestMethod]
    public async Task Options_NetworkChangedOnTheEngine_ClearsTheEngineCachesAndIsKeptAsNone()
    {
        await using ManagedCurlEngine engine = new();
        await using IMultiTransfer multi = engine.CreateMulti();

        multi.Options = new MultiOptions { NetworkChanged = CurlNetworkChange.ClearConnections | CurlNetworkChange.ClearDns };

        Assert.AreEqual(CurlNetworkChange.None, multi.Options.NetworkChanged);
    }

    [TestMethod]
    public async Task GetWaitSockets_NoSocketCallback_IsTheOperatingSystemSocketTheTransferDials()
    {
        HeldSocketDialer dialer = new(HeldSocket);
        await using ManagedCurlEngine engine = EngineOver(dialer);
        await using IMultiTransfer multi = engine.CreateMulti();
        multi.Add(EasyTo(engine));
        CurlWaitSocket[] sockets = new CurlWaitSocket[2];

        await multi.PerformAsync(CancellationToken.None);
        await dialer.Requested.Task.WaitAsync(LongestSocketWait);
        CurlMultiCode code = multi.GetWaitSockets(sockets, out int count);

        Assert.AreEqual(CurlMultiCode.Ok, code);
        Assert.AreEqual(2, count);
        Assert.AreEqual(new CurlWaitSocket(new CurlSocket(HeldSocket), CurlWaitEvents.PollIn, CurlWaitEvents.None), sockets[0]);
        Assert.AreEqual(new CurlSocket(ManagedMultiTransfer.ResolverWakeupSocket), sockets[1].Socket);
    }

    [TestMethod]
    public async Task GetWaitSockets_NoSocketCallbackAndAConnectionWithoutAnOperatingSystemSocket_IsItsEngineSocket()
    {
        HeldSocketDialer dialer = new(null);
        await using ManagedCurlEngine engine = EngineOver(dialer);
        await using IMultiTransfer multi = engine.CreateMulti();
        multi.Add(EasyTo(engine));
        CurlWaitSocket[] sockets = new CurlWaitSocket[2];

        await multi.PerformAsync(CancellationToken.None);
        await dialer.Requested.Task.WaitAsync(LongestSocketWait);
        multi.GetWaitSockets(sockets, out int count);

        Assert.AreEqual(2, count);
        Assert.IsGreaterThan((nint)TransferSocketWait.FirstEngineSocket, sockets[0].Socket.Handle);
    }

    [TestMethod]
    public async Task GetWaitSockets_SocketCallback_IsTheOperatingSystemSocketTheCallbackIsTold()
    {
        HeldSocketDialer dialer = new(HeldSocket);
        await using ManagedCurlEngine engine = EngineOver(dialer);
        await using IMultiTransfer multi = engine.CreateMulti();
        ConcurrentQueue<CurlSocket> told = new();
        multi.Options = new MultiOptions { Socket = (_, socket, _, _) => { told.Enqueue(socket); return 0; } };
        multi.Add(EasyTo(engine));
        CurlWaitSocket[] sockets = new CurlWaitSocket[2];

        await multi.SocketActionAsync(CurlMultiSockets.Timeout, CurlSocketSelect.None, CancellationToken.None);
        await dialer.Dialing.Task.WaitAsync(LongestSocketWait);
        multi.GetWaitSockets(sockets, out int count);
        await multi.SocketActionAsync(CurlMultiSockets.Timeout, CurlSocketSelect.None, CancellationToken.None);

        Assert.AreEqual(2, count);
        Assert.AreEqual(new CurlSocket(HeldSocket), sockets[0].Socket);
        CollectionAssert.Contains(told.ToArray(), new CurlSocket(HeldSocket));
    }

    private const nint HeldSocket = 4242;

    private static readonly TimeSpan LongestSocketWait = TimeSpan.FromSeconds(30);

    private static IEasyTransfer EasyTo(ManagedCurlEngine engine)
    {
        IEasyTransfer easy = engine.CreateEasy();
        easy.Options = new EasyOptions { Network = new EasyNetworkOptions { Url = "http://127.0.0.1:9000/" } };
        return easy;
    }

    private static ManagedCurlEngine EngineOver(ITcpDialer dialer) =>
        new(TimeProvider.System, null, dialing: new EngineDialing(dialer, new LoopbackDnsResolver(), new RecordingDatagramConnector(CurlExitCode.CouldntConnect, "no udp")));

    /// <summary>
    /// A dialer whose one connection carries <paramref name="socket" /> as its operating system socket (none when
    /// <see langword="null" />), published as the dial starts as <see cref="TcpDialer" /> publishes it, takes the
    /// request and never answers, so the transfer keeps running on it.
    /// </summary>
    private sealed class HeldSocketDialer(nint? socket) : ITcpDialer
    {
        private Action<nint> published = _ => { };

        public TaskCompletionSource Dialing { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource Requested { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public ITcpDialer PublishingSocketsTo(Action<nint> socketCreated)
        {
            published = socketCreated;
            return this;
        }

        public ValueTask<DialedTcpConnection> DialAsync(IPEndPoint endPoint, CancellationToken cancellationToken)
        {
            if (socket is { } handle)
            {
                published(handle);
            }

            Dialing.TrySetResult();
            return ValueTask.FromResult(new DialedTcpConnection(new HeldConnection(socket, Requested), new IPEndPoint(IPAddress.Loopback, 50000)));
        }

        public ValueTask<DialedTcpConnection> DialFromAsync(IPEndPoint endPoint, IPEndPoint localEndPoint, int localPortCount, CancellationToken cancellationToken) =>
            DialAsync(endPoint, cancellationToken);

        public ValueTask<IConnection> DialUnixSocketAsync(UnixSocketAddress address, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }

    private sealed class HeldConnection(nint? socket, TaskCompletionSource requested) : IConnection
    {
        public bool IsSecure => false;

        public EndPoint? RemoteEndPoint => new IPEndPoint(IPAddress.Loopback, 9000);

        public nint? SocketHandle => socket;

        public async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken)
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            return 0;
        }

        public ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken)
        {
            requested.TrySetResult();
            return ValueTask.CompletedTask;
        }

        public ValueTask FlushAsync(CancellationToken cancellationToken) => ValueTask.CompletedTask;

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
