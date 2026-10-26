using System.Net;

using Curl.Api.EngineContracts.Multi;
using Curl.Api.Options;
using Curl.Api.Options.Behaviour;
using Curl.Api.Options.Network;
using Curl.Protocol.Abstractions;

namespace Curl.Engine;

[TestClass]
public sealed class SocketActionGatedConnectionTests
{
    private static readonly DateTimeOffset Epoch = new(2026, 10, 26, 12, 0, 0, TimeSpan.Zero);

    [TestMethod]
    public void Waiting_Fresh_IsNoneWithNoDeadline()
    {
        TransferSocketWait wait = new(new SettableClock());

        Assert.AreEqual(CurlPoll.None, wait.Waiting);
        Assert.IsNull(wait.EarliestDeadline);
    }

    [TestMethod]
    public async Task ReadAsync_BeforeReadableSignal_DoesNotReadAndWaitsIn()
    {
        RecordingConnection inner = new();
        TransferSocketWait wait = new(new SettableClock());
        SocketActionGatedConnection connection = new(inner, wait);
        byte[] buffer = new byte[4];

        ValueTask<int> read = connection.ReadAsync(buffer, CancellationToken.None);
        wait.Signal(CurlSocketSelect.Out);

        Assert.IsFalse(read.IsCompleted);
        Assert.AreEqual(0, inner.Reads);
        Assert.AreEqual(CurlPoll.In, wait.Waiting);

        wait.Signal(CurlSocketSelect.In);

        Assert.AreEqual(3, await read);
        Assert.AreEqual(1, inner.Reads);
        Assert.AreEqual(CurlPoll.None, wait.Waiting);
    }

    [TestMethod]
    public async Task WriteAsync_BeforeWritableSignal_DoesNotWriteAndWaitsOut()
    {
        RecordingConnection inner = new();
        TransferSocketWait wait = new(new SettableClock());
        SocketActionGatedConnection connection = new(inner, wait);

        ValueTask write = connection.WriteAsync(new byte[] { 1, 2 }, CancellationToken.None);
        wait.Signal(CurlSocketSelect.In);

        Assert.IsFalse(write.IsCompleted);
        Assert.AreEqual(0, inner.Writes);
        Assert.AreEqual(CurlPoll.Out, wait.Waiting);

        wait.Signal(CurlSocketSelect.Out);
        await write;

        Assert.AreEqual(1, inner.Writes);
        Assert.AreEqual(CurlPoll.None, wait.Waiting);
    }

    [TestMethod]
    public async Task Signal_Error_ReleasesBothWaitsAndPublishesInOutBefore()
    {
        TransferSocketWait wait = new(new SettableClock());
        ValueTask read = wait.WaitUntilReadableAsync(CancellationToken.None);
        ValueTask write = wait.WaitUntilWritableAsync(CancellationToken.None);

        Assert.AreEqual(CurlPoll.InOut, wait.Waiting);

        wait.Signal(CurlSocketSelect.Error);
        await read;
        await write;

        Assert.AreEqual(CurlPoll.None, wait.Waiting);
    }

    [TestMethod]
    public async Task Signal_WhileNothingWaits_IsKeptForOneWaitOnly()
    {
        TransferSocketWait wait = new(new SettableClock());
        wait.Signal(CurlSocketSelect.None);
        wait.Signal(CurlSocketSelect.In | CurlSocketSelect.Out);

        await wait.WaitUntilReadableAsync(CancellationToken.None);
        await wait.WaitUntilWritableAsync(CancellationToken.None);
        ValueTask second = wait.WaitUntilReadableAsync(CancellationToken.None);

        Assert.IsFalse(second.IsCompleted);
        Assert.AreEqual(CurlPoll.In, wait.Waiting);
        wait.Signal(CurlSocketSelect.In);
        await second;
    }

    [TestMethod]
    public async Task WaitUntilReadableAsync_Cancelled_ThrowsAndStopsWaitingInKeepingTheWriteWait()
    {
        SettableClock clock = new();
        TransferSocketWait wait = new(clock);
        wait.Start(new EasyOptions { Transfer = new EasyTransferOptions { LowSpeedLimit = 10, LowSpeedTime = TimeSpan.FromSeconds(30) } });
        wait.EndConnect();
        using CancellationTokenSource cancel = new();
        ValueTask write = wait.WaitUntilWritableAsync(CancellationToken.None);
        ValueTask read = wait.WaitUntilReadableAsync(cancel.Token);

        await cancel.CancelAsync();

        await Assert.ThrowsExactlyAsync<TaskCanceledException>(async () => await read);
        Assert.AreEqual(CurlPoll.Out, wait.Waiting);
        Assert.AreEqual(Epoch + TimeSpan.FromSeconds(30), wait.EarliestDeadline);
        wait.Signal(CurlSocketSelect.Out);
        await write;
        Assert.IsNull(wait.EarliestDeadline);
    }

    [TestMethod]
    public async Task WaitUntilWritableAsync_Cancelled_ThrowsAndStopsWaitingOut()
    {
        TransferSocketWait wait = new(new SettableClock());
        using CancellationTokenSource cancel = new();
        ValueTask write = wait.WaitUntilWritableAsync(cancel.Token);

        await cancel.CancelAsync();

        await Assert.ThrowsExactlyAsync<TaskCanceledException>(async () => await write);
        Assert.AreEqual(CurlPoll.None, wait.Waiting);
    }

    [TestMethod]
    public async Task Signal_OutWhileAReadStillWaits_KeepsTheReadsWaitAndLowSpeedDeadline()
    {
        TransferSocketWait wait = new(new SettableClock());
        wait.Start(new EasyOptions { Transfer = new EasyTransferOptions { LowSpeedLimit = 10, LowSpeedTime = TimeSpan.FromSeconds(30) } });
        wait.EndConnect();
        ValueTask read = wait.WaitUntilReadableAsync(CancellationToken.None);
        ValueTask write = wait.WaitUntilWritableAsync(CancellationToken.None);

        wait.Signal(CurlSocketSelect.Out);
        await write;

        Assert.AreEqual(CurlPoll.In, wait.Waiting);
        Assert.AreEqual(Epoch + TimeSpan.FromSeconds(30), wait.EarliestDeadline);
        wait.Signal(CurlSocketSelect.In);
        await read;
    }

    [TestMethod]
    public void BeginConnect_WaitsOutUntilEndConnect()
    {
        TransferSocketWait wait = new(new SettableClock());
        wait.Start(new EasyOptions());

        wait.BeginConnect();
        Assert.AreEqual(CurlPoll.Out, wait.Waiting);
        Assert.AreEqual(Epoch + TransferSocketWait.DefaultConnectTimeout, wait.EarliestDeadline);

        wait.EndConnect();
        Assert.AreEqual(CurlPoll.None, wait.Waiting);
        Assert.IsNull(wait.EarliestDeadline);
    }

    [TestMethod]
    public void Start_TimeoutBeforeConnectTimeout_IsTheEarliestDeadline()
    {
        TransferSocketWait wait = new(new SettableClock());

        wait.Start(new EasyOptions { Network = new EasyNetworkOptions { ConnectTimeout = TimeSpan.FromSeconds(2) }, Transfer = new EasyTransferOptions { Timeout = TimeSpan.FromMilliseconds(600) } });

        Assert.AreEqual(Epoch + TimeSpan.FromMilliseconds(600), wait.EarliestDeadline);
    }

    [TestMethod]
    public void Start_ConnectTimeoutBeforeTimeout_IsTheEarliestDeadlineUntilTheConnectEnds()
    {
        TransferSocketWait wait = new(new SettableClock());

        wait.Start(new EasyOptions { Network = new EasyNetworkOptions { ConnectTimeout = TimeSpan.FromSeconds(2) }, Transfer = new EasyTransferOptions { Timeout = TimeSpan.FromSeconds(5) } });
        Assert.AreEqual(Epoch + TimeSpan.FromSeconds(2), wait.EarliestDeadline);

        wait.EndConnect();
        Assert.AreEqual(Epoch + TimeSpan.FromSeconds(5), wait.EarliestDeadline);
    }

    [TestMethod]
    [DataRow(0L, 30)]
    [DataRow(10L, 0)]
    public async Task WaitUntilReadableAsync_LowSpeedNotLimited_HasNoLowSpeedDeadline(long limit, int seconds)
    {
        TransferSocketWait wait = new(new SettableClock());
        wait.Start(new EasyOptions { Transfer = new EasyTransferOptions { LowSpeedLimit = limit, LowSpeedTime = TimeSpan.FromSeconds(seconds) } });
        wait.EndConnect();

        ValueTask read = wait.WaitUntilReadableAsync(CancellationToken.None);

        Assert.IsNull(wait.EarliestDeadline);
        wait.Signal(CurlSocketSelect.In);
        await read;
    }

    [TestMethod]
    public async Task WaitUntilReadableAsync_LowSpeedLimited_DeadlineIsLowSpeedTimeFromTheWaitsStart()
    {
        SettableClock clock = new();
        TransferSocketWait wait = new(clock);
        wait.Start(new EasyOptions { Transfer = new EasyTransferOptions { LowSpeedLimit = 10, LowSpeedTime = TimeSpan.FromSeconds(30) } });
        wait.EndConnect();
        clock.Now = Epoch + TimeSpan.FromSeconds(4);

        ValueTask read = wait.WaitUntilReadableAsync(CancellationToken.None);

        Assert.AreEqual(Epoch + TimeSpan.FromSeconds(34), wait.EarliestDeadline);
        wait.Signal(CurlSocketSelect.In);
        await read;
        Assert.IsNull(wait.EarliestDeadline);
    }

    [TestMethod]
    public void BeginExpectContinueWait_DeadlineLastsUntilItEnds()
    {
        SettableClock clock = new() { Now = Epoch + TimeSpan.FromSeconds(1) };
        TransferSocketWait wait = new(clock);

        wait.BeginExpectContinueWait(TimeSpan.FromSeconds(1));
        Assert.AreEqual(Epoch + TimeSpan.FromSeconds(2), wait.EarliestDeadline);

        wait.EndExpectContinueWait();
        Assert.IsNull(wait.EarliestDeadline);
    }

    [TestMethod]
    public async Task Changed_RaisedForEachChangeToWhatItWaitsForOrItsDeadline()
    {
        TransferSocketWait wait = new(new SettableClock());
        int changes = 0;
        wait.Changed += () => changes++;

        wait.Start(new EasyOptions());
        wait.BeginConnect();
        wait.EndConnect();
        ValueTask read = wait.WaitUntilReadableAsync(CancellationToken.None);
        wait.Signal(CurlSocketSelect.In);
        await read;

        Assert.AreEqual(5, changes);
    }

    [TestMethod]
    public async Task OtherMembers_AreTheInnerConnections()
    {
        RecordingConnection inner = new();
        SocketActionGatedConnection connection = new(inner, new TransferSocketWait(new SettableClock()));
        RecordingSession session = new();

        Assert.IsTrue(connection.IsSecure);
        Assert.AreEqual(RecordingConnection.Remote, connection.RemoteEndPoint);
        Assert.AreEqual(RecordingConnection.Local, connection.LocalEndPoint);
        Assert.AreEqual((nint)42, connection.SocketHandle);
        Assert.AreSame(inner.HeldSession, connection.Session);
        Assert.IsTrue(connection.IsSharedWithAnotherTransfer);
        Assert.IsTrue(connection.HasPeerClosed);
        Assert.IsTrue(connection.TryHoldSession(session));
        Assert.AreSame(inner, await connection.ClearTlsAsync(true, CancellationToken.None));
        connection.MarkReusable();
        await connection.FlushAsync(CancellationToken.None);
        await connection.ShutDownSendingAsync(CancellationToken.None);
        await connection.DisposeAsync();

        Assert.AreEqual("hold mark flush shutdown dispose", string.Join(' ', inner.Calls));
    }

    private sealed class SettableClock : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = Epoch;

        public override DateTimeOffset GetUtcNow() => Now;
    }

    private sealed class RecordingSession : IConnectionSession
    {
        public int ConcurrentTransferLimit => 1;

        public ValueTask ShutDownAsync(CancellationToken cancellationToken) => ValueTask.CompletedTask;
    }

    private sealed class RecordingConnection : IConnection
    {
        public static readonly EndPoint Remote = new IPEndPoint(IPAddress.Loopback, 80);

        public static readonly EndPoint Local = new IPEndPoint(IPAddress.Loopback, 50000);

        public List<string> Calls { get; } = [];

        public int Reads { get; private set; }

        public int Writes { get; private set; }

        public IConnectionSession HeldSession { get; } = new RecordingSession();

        public bool IsSecure => true;

        public EndPoint? RemoteEndPoint => Remote;

        public EndPoint? LocalEndPoint => Local;

        public nint? SocketHandle => 42;

        public IConnectionSession? Session => HeldSession;

        public bool IsSharedWithAnotherTransfer => true;

        public bool HasPeerClosed => true;

        public ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken)
        {
            Reads++;
            return ValueTask.FromResult(3);
        }

        public ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken)
        {
            Writes++;
            return ValueTask.CompletedTask;
        }

        public ValueTask FlushAsync(CancellationToken cancellationToken) => Record("flush");

        public void MarkReusable() => Calls.Add("mark");

        public bool TryHoldSession(IConnectionSession session)
        {
            Calls.Add("hold");
            return true;
        }

        public ValueTask<IConnection?> ClearTlsAsync(bool sendCloseNotifyFirst, CancellationToken cancellationToken) =>
            ValueTask.FromResult<IConnection?>(this);

        public ValueTask ShutDownSendingAsync(CancellationToken cancellationToken) => Record("shutdown");

        public ValueTask DisposeAsync() => Record("dispose");

        private ValueTask Record(string call)
        {
            Calls.Add(call);
            return ValueTask.CompletedTask;
        }
    }
}
