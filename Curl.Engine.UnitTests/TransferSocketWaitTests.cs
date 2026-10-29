using Curl.Api.EngineContracts.Multi;

namespace Curl.Engine;

[TestClass]
public sealed class TransferSocketWaitTests
{
    private static readonly TimeSpan LongestWait = TimeSpan.FromSeconds(30);

    [TestMethod]
    public async Task StopGating_ReadInProgress_ReleasesItAndEveryLaterWait()
    {
        TransferSocketWait wait = new(TimeProvider.System);
        ValueTask reading = wait.WaitUntilReadableAsync(CancellationToken.None);
        bool heldBefore = !reading.IsCompleted;

        wait.StopGating();

        Assert.IsTrue(heldBefore);
        await reading.AsTask().WaitAsync(TimeSpan.FromSeconds(10));
        Assert.IsTrue(wait.WaitUntilWritableAsync(CancellationToken.None).IsCompleted);
        Assert.IsTrue(wait.WaitUntilWritableAsync(CancellationToken.None).IsCompleted);
        Assert.IsTrue(wait.WaitUntilReadableAsync(CancellationToken.None).IsCompleted);
    }

    [TestMethod]
    public void WaitingPredicates_NoSocketAndNoWait_AreAllFalse()
    {
        TransferSocketWait wait = new(TimeProvider.System);

        Assert.IsFalse(wait.IsWaitingOnItsSocket);
        Assert.IsFalse(wait.IsWaitingWithoutSocket);
        Assert.IsFalse(wait.IsWaitingOnAnEngineSocket);
    }

    [TestMethod]
    public void WaitingPredicates_ReadWaitingWithNoSocket_IsWaitingWithoutSocketOnly()
    {
        TransferSocketWait wait = new(TimeProvider.System);
        _ = wait.WaitUntilReadableAsync(CancellationToken.None);

        Assert.IsTrue(wait.IsWaitingWithoutSocket);
        Assert.IsFalse(wait.IsWaitingOnItsSocket);
        Assert.IsFalse(wait.IsWaitingOnAnEngineSocket);
        wait.StopGating();
    }

    [TestMethod]
    public void IsWaitingWithoutSocket_ConnectingWithNoSocket_IsFalse()
    {
        TransferSocketWait wait = new(TimeProvider.System);

        wait.BeginConnect();

        Assert.AreEqual(CurlPoll.Out, wait.Waiting);
        Assert.IsFalse(wait.IsWaitingWithoutSocket);
    }

    [TestMethod]
    public void WaitingPredicates_EngineSocketNotWaiting_AreAllFalse()
    {
        TransferSocketWait wait = new(TimeProvider.System);

        wait.TakeSocket(null);

        Assert.IsFalse(wait.IsWaitingOnItsSocket);
        Assert.IsFalse(wait.IsWaitingWithoutSocket);
        Assert.IsFalse(wait.IsWaitingOnAnEngineSocket);
    }

    [TestMethod]
    public void WaitingPredicates_ReadWaitingOnAnEngineSocket_IsWaitingOnItsSocketAndOnAnEngineSocket()
    {
        TransferSocketWait wait = new(TimeProvider.System);
        wait.TakeSocket(null);
        _ = wait.WaitUntilReadableAsync(CancellationToken.None);

        Assert.IsTrue(wait.IsWaitingOnItsSocket);
        Assert.IsTrue(wait.IsWaitingOnAnEngineSocket);
        Assert.IsFalse(wait.IsWaitingWithoutSocket);
        wait.StopGating();
    }

    [TestMethod]
    public void WaitingPredicates_ReadWaitingOnAnOperatingSystemSocket_IsWaitingOnItsSocketButNotOnAnEngineSocket()
    {
        TransferSocketWait wait = new(TimeProvider.System);
        wait.TakeSocket(42);
        _ = wait.WaitUntilReadableAsync(CancellationToken.None);

        Assert.IsTrue(wait.IsWaitingOnItsSocket);
        Assert.IsFalse(wait.IsWaitingOnAnEngineSocket);
        wait.StopGating();
    }

    [TestMethod]
    public void TakeSocket_NoOperatingSystemSocket_GivesAnEngineNumberAboveFirstEngineSocket()
    {
        TransferSocketWait wait = new(TimeProvider.System);

        wait.TakeSocket(null);

        Assert.IsNotNull(wait.Socket);
        Assert.IsGreaterThan(TransferSocketWait.FirstEngineSocket, (long)wait.Socket.Value);
        Assert.IsLessThan(ConnectOnlyConnection.FirstSocket, (long)wait.Socket.Value);
        Assert.IsGreaterThan((long)ManagedMultiTransfer.ResolverWakeupSocket, (long)wait.Socket.Value);
    }

    [TestMethod]
    public void TakeSocket_TwoTransfersWithNoOperatingSystemSocket_GivesEachItsOwnNumber()
    {
        TransferSocketWait first = new(TimeProvider.System);
        TransferSocketWait second = new(TimeProvider.System);

        first.TakeSocket(null);
        second.TakeSocket(null);

        Assert.AreNotEqual(first.Socket, second.Socket);
    }

    [TestMethod]
    public void TakeSocket_OperatingSystemSocket_KeepsItsHandle()
    {
        TransferSocketWait wait = new(TimeProvider.System);

        wait.TakeSocket(812);

        Assert.AreEqual((nint)812, wait.Socket);
    }

    [TestMethod]
    public void TakeSocket_SocketAlreadyKnownFromTheDial_KeepsIt()
    {
        TransferSocketWait wait = new(TimeProvider.System);
        wait.ConnectingOn(640);

        wait.TakeSocket(null);

        Assert.AreEqual((nint)640, wait.Socket);
    }

    [TestMethod]
    public void TakeSocket_RaisesChanged()
    {
        TransferSocketWait wait = new(TimeProvider.System);
        int changes = 0;
        wait.Changed += () => changes++;

        wait.TakeSocket(null);

        Assert.AreEqual(1, changes);
    }

    [TestMethod]
    public async Task SignalOn_TheEngineNumber_WakesTheReadWaitingOnIt()
    {
        TransferSocketWait wait = new(TimeProvider.System);
        wait.TakeSocket(null);
        Task read = wait.WaitUntilReadableAsync(CancellationToken.None).AsTask();
        CurlPoll waiting = wait.Waiting;

        bool signalled = wait.SignalOn(wait.Socket!.Value, CurlSocketSelect.In);

        await read.WaitAsync(LongestWait);
        Assert.IsTrue(signalled);
        Assert.AreEqual(CurlPoll.In, waiting);
    }

    [TestMethod]
    public void SignalOn_AnotherSocket_LeavesTheWaitWaiting()
    {
        TransferSocketWait wait = new(TimeProvider.System);
        wait.TakeSocket(null);
        Task write = wait.WaitUntilWritableAsync(CancellationToken.None).AsTask();

        bool signalled = wait.SignalOn(wait.Socket!.Value + 1, CurlSocketSelect.Out);

        Assert.IsFalse(signalled);
        Assert.IsFalse(write.IsCompleted);
        Assert.AreEqual(CurlPoll.Out, wait.Waiting);
        wait.Signal(CurlSocketSelect.Out);
    }

    [TestMethod]
    public void BeginResolve_ActionOnTheWakeup_KeepsNoSignalAndTheDialReplacesIt()
    {
        TransferSocketWait wait = new(TimeProvider.System);
        DisposalCounter first = new();
        DisposalCounter second = new();
        wait.BeginResolve(7, first);
        wait.BeginResolve(8, second);

        bool signalled = wait.SignalOn(8, CurlSocketSelect.In);
        wait.EndResolve();
        ValueTask read = wait.WaitUntilReadableAsync(CancellationToken.None);
        bool readHeld = !read.IsCompleted;
        wait.TakeSocket(null);

        Assert.IsTrue(signalled);
        Assert.IsTrue(readHeld);
        Assert.AreEqual(1, first.Disposals);
        Assert.AreEqual(0, second.Disposals);
        Assert.IsTrue(wait.IsWaitingOnAnEngineSocket);
        wait.StopGating();
        Assert.AreEqual(1, second.Disposals);
    }

    [TestMethod]
    public void ConnectingOn_AfterAResolve_MakesTheDialedSocketTheTransfersAndTakeSocketKeepsIt()
    {
        TransferSocketWait wait = new(TimeProvider.System);
        wait.BeginResolve(7, new DisposalCounter());
        wait.EndResolve();

        wait.ConnectingOn(9);
        wait.TakeSocket(10);

        Assert.AreEqual((nint)9, wait.Socket);
        wait.StopGating();
    }

    private sealed class DisposalCounter : IDisposable
    {
        internal int Disposals { get; private set; }

        public void Dispose() => Disposals++;
    }
}
