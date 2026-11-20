using System.Net;
using System.Text;
using Curl.Extended.Protocol.SignalR.Transport;
using Curl.Protocol.Abstractions;

namespace Curl.Extended.Protocol.SignalR.Client;

/// <summary>Tests the client's keep-alive Ping (HubProtocol.md, "Ping") against a manual clock: one per idle interval, none while sending.</summary>
[TestClass]
public sealed class SignalRKeepAliveTests
{
    private const string NegotiateBody =
        "{\"connectionToken\":\"tok\",\"connectionId\":\"cid\",\"negotiateVersion\":1,"
        + "\"availableTransports\":[{\"transport\":\"WebSockets\",\"transferFormats\":[\"Text\",\"Binary\"]}]}";

    private const string PingRecord = "{\"type\":6}\u001e";

    private static readonly string Upgraded =
        "HTTP/1.1 101 Switching Protocols\r\nUpgrade: websocket\r\nConnection: Upgrade\r\nSec-WebSocket-Accept: "
        + SignalRWebSocketUpgrade.AcceptFor(Convert.ToBase64String(new byte[16])) + "\r\n\r\n";

    [TestMethod]
    public async Task ListeningClient_SendsOnePingPerKeepAliveInterval_HubProtocolPing()
    {
        var clock = new ManualTimeProvider();
        var hub = new BlockingConnection(Encoding.Latin1.GetBytes(Upgraded), Frame("{}\u001e"));
        Task<TransferResult> run = Start(clock, hub);
        await WaitUntil(() => clock.PendingTimers == 1);

        clock.Advance(TimeSpan.FromSeconds(14));
        Assert.AreEqual(0, CountPings(hub));
        clock.Advance(TimeSpan.FromSeconds(1));
        await WaitUntil(() => CountPings(hub) == 1);
        await WaitUntil(() => clock.PendingTimers == 1);
        clock.Advance(TimeSpan.FromSeconds(15));
        await WaitUntil(() => CountPings(hub) == 2);

        hub.Give(Frame("{\"type\":7}\u001e"));
        Assert.AreEqual(CurlExitCode.Ok, (await run).ExitCode);
        Assert.AreEqual(2, CountPings(hub));
    }

    [TestMethod]
    public async Task ClientSendingWithinTheInterval_SendsNoPingUntilAFullIntervalIsIdle_HubProtocolPing()
    {
        var clock = new ManualTimeProvider();
        var hub = new BlockingConnection(Encoding.Latin1.GetBytes(Upgraded), Frame("{}\u001e"));
        Task<TransferResult> run = Start(clock, hub);
        await WaitUntil(() => clock.PendingTimers == 1);

        clock.Advance(TimeSpan.FromSeconds(10));
        hub.Give(Frame("{\"type\":1,\"invocationId\":\"s1\",\"target\":\"Ask\",\"arguments\":[]}\u001e"));
        await WaitUntil(() => hub.WrittenText.Contains("\"invocationId\":\"s1\"", StringComparison.Ordinal));
        clock.Advance(TimeSpan.FromSeconds(10));
        await WaitUntil(() => clock.PendingTimers == 1);
        Assert.AreEqual(0, CountPings(hub));
        clock.Advance(TimeSpan.FromSeconds(5));
        await WaitUntil(() => CountPings(hub) == 1);

        hub.Give(Frame("{\"type\":7}\u001e"));
        Assert.AreEqual(CurlExitCode.Ok, (await run).ExitCode);
    }

    [TestMethod]
    public async Task CompletionWhileAPingWriteIsBlocked_WaitsForTheWholePingFrameThenSendsClose_HubProtocolPing()
    {
        var clock = new ManualTimeProvider();
        var hub = new BlockingConnection(Encoding.Latin1.GetBytes(Upgraded), Frame("{}\u001e"));
        Task<TransferResult> run = Start(clock, hub);
        await WaitUntil(() => clock.PendingTimers == 1);
        hub.HoldNextWrite();
        clock.Advance(TimeSpan.FromSeconds(15));
        await WaitUntil(() => hub.IsHoldingAWrite);

        hub.Give(Frame("{\"type\":7}\u001e"));
        await Task.Delay(50);
        Assert.IsFalse(run.IsCompleted);
        hub.ReleaseHeldWrite();

        Assert.AreEqual(CurlExitCode.Ok, (await run).ExitCode);
        string written = hub.WrittenText;
        int closeAt = written.Length - 8;
        Assert.AreEqual("\u0088\u0082", written.Substring(closeAt, 2));
        Assert.AreEqual("\u0081\u008b", written.Substring(closeAt - 17, 2));
    }

    [TestMethod]
    public async Task PingWriteFailsWithAnIoError_FailsTheTransferWithTheReceiveErrorCode_HubProtocolPing()
    {
        var clock = new ManualTimeProvider();
        var hub = new BlockingConnection(Encoding.Latin1.GetBytes(Upgraded), Frame("{}\u001e"));
        Task<TransferResult> run = Start(clock, hub);
        await WaitUntil(() => clock.PendingTimers == 1);
        hub.FailWritesFromNowOn();
        clock.Advance(TimeSpan.FromSeconds(15));
        await WaitUntil(() => hub.FailedWrites > 0);

        hub.Give(Frame("{\"type\":7}\u001e"));

        Assert.AreEqual(CurlExitCode.RecvError, (await run).ExitCode);
    }

    [TestMethod]
    public async Task PingQueuedBehindTheServersCloseAnswer_IsDroppedNotWrittenAfterTheClose_HubProtocolPing()
    {
        var clock = new ManualTimeProvider();
        var hub = new BlockingConnection(Encoding.Latin1.GetBytes(Upgraded), Frame("{}\u001e"));
        Task<TransferResult> run = Start(clock, hub);
        await WaitUntil(() => clock.PendingTimers == 1);
        hub.HoldNextWrite();
        hub.Give([0x88, 0x02, 0x03, 0xE8]);
        await WaitUntil(() => hub.IsHoldingAWrite);
        int timestampReadsBefore = clock.TimestampReads;
        clock.Advance(TimeSpan.FromSeconds(15));

        // The Ping loop reads the clock when its idle wait ends, then goes straight to the send semaphore without yielding.
        await WaitUntil(() => clock.TimestampReads > timestampReadsBefore);
        hub.ReleaseHeldWrite();

        Assert.AreEqual(CurlExitCode.Ok, (await run).ExitCode);
        Assert.AreEqual(0, CountPings(hub));
    }

    [TestMethod]
    public async Task PingWriteFailsAfterTheServersCloseArrived_DoesNotChangeTheExitCode_HubProtocolPing()
    {
        var clock = new ManualTimeProvider();
        var hub = new BlockingConnection(Encoding.Latin1.GetBytes(Upgraded), Frame("{}\u001e"));
        Task<TransferResult> run = Start(clock, hub);
        await WaitUntil(() => clock.PendingTimers == 1);
        hub.HoldNextWrite();
        clock.Advance(TimeSpan.FromSeconds(15));
        await WaitUntil(() => hub.IsHoldingAWrite);
        int readsBefore = hub.ReadsConsumed;
        hub.Give([0x88, 0x02, 0x03, 0xE8]);
        await WaitUntil(() => hub.ReadsConsumed > readsBefore);
        hub.ReleaseHeldWriteWithFailure();

        Assert.AreEqual(CurlExitCode.Ok, (await run).ExitCode);
    }

    [TestMethod]
    public async Task PingWriteFailsBeforeTheServersCloseIsDecoded_DoesNotChangeTheExitCode_HubProtocolPing()
    {
        var clock = new ManualTimeProvider();
        var hub = new BlockingConnection(Encoding.Latin1.GetBytes(Upgraded), Frame("{}\u001e"));
        Task<TransferResult> run = Start(clock, hub);
        await WaitUntil(() => clock.PendingTimers == 1);
        hub.HoldNextWrite();
        clock.Advance(TimeSpan.FromSeconds(15));
        await WaitUntil(() => hub.IsHoldingAWrite);
        hub.ReleaseHeldWriteWithFailure();
        await Task.Delay(50);
        hub.Give([0x88, 0x02, 0x03, 0xE8]);

        Assert.AreEqual(CurlExitCode.Ok, (await run).ExitCode);
    }

    [TestMethod]
    public async Task ReadLoopFailsWhileAPingWriteIsBlocked_WaitsForThePingThenReportsTheReadFailure_HubProtocolPing()
    {
        var clock = new ManualTimeProvider();
        var hub = new BlockingConnection(Encoding.Latin1.GetBytes(Upgraded), Frame("{}\u001e"));
        Task<TransferResult> run = Start(clock, hub);
        await WaitUntil(() => clock.PendingTimers == 1);
        hub.HoldNextWrite();
        clock.Advance(TimeSpan.FromSeconds(15));
        await WaitUntil(() => hub.IsHoldingAWrite);

        int readsBefore = hub.ReadsConsumed;
        hub.Give(Frame("not json\u001e"));
        await WaitUntil(() => hub.ReadsConsumed > readsBefore);
        Assert.IsFalse(run.IsCompleted);
        hub.ReleaseHeldWriteWithFailure();

        Assert.AreEqual(CurlExitCode.WeirdServerReply, (await run).ExitCode);
    }

    private static Task<TransferResult> Start(ManualTimeProvider clock, BlockingConnection hub)
    {
        ScriptedConnection negotiate = ScriptedConnection.FromText($"HTTP/1.1 200 OK\r\nContent-Length: {NegotiateBody.Length}\r\n\r\n{NegotiateBody}");
        var connector = new ScriptedConnector(ConnectResult.Connected(negotiate), ConnectResult.Connected(hub));
        return new SignalRProtocolHandler(connector, new RepeatingRandomSource(0), clock).ExecuteAsync(new TransferContext
        {
            Url = CurlUrl.Parse("signalr://example.com/chat"),
            Output = new MemoryStream(),
        }).AsTask();
    }

    private static int CountPings(BlockingConnection hub) =>
        hub.WrittenText.Split(PingRecord).Length - 1;

    private static byte[] Frame(string payload)
    {
        byte[] bytes = Encoding.Latin1.GetBytes(payload);
        return [0x81, (byte)bytes.Length, .. bytes];
    }

    private static async Task WaitUntil(Func<bool> condition)
    {
        DateTime deadline = DateTime.UtcNow.AddSeconds(30);
        while (!condition())
        {
            Assert.IsTrue(DateTime.UtcNow < deadline, "The condition was not met in 30 s.");
            await Task.Delay(1);
        }
    }

    /// <summary>A connection that gives its first reads at once, then waits for <see cref="Give"/>, and records every write.</summary>
    private sealed class BlockingConnection(params byte[][] firstReads) : IConnection
    {
        private readonly Lock gate = new();

        private readonly Queue<byte[]> pending = new(firstReads);

        private readonly SemaphoreSlim available = new(firstReads.Length);

        private readonly StringBuilder written = new();

        public bool IsSecure => false;

        public EndPoint? RemoteEndPoint => null;

        public string WrittenText
        {
            get
            {
                lock (gate)
                {
                    return written.ToString();
                }
            }
        }

        public void Give(byte[] bytes)
        {
            lock (gate)
            {
                pending.Enqueue(bytes);
            }

            available.Release();
        }

        public async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken)
        {
            await available.WaitAsync(cancellationToken);
            byte[] next;
            lock (gate)
            {
                next = pending.Dequeue();
            }

            next.CopyTo(buffer);
            Interlocked.Increment(ref readsConsumed);
            return next.Length;
        }

        public bool IsHoldingAWrite => Volatile.Read(ref holding) == 1;

        public int ReadsConsumed => Volatile.Read(ref readsConsumed);

        public int FailedWrites => Volatile.Read(ref failedWrites);

        public void HoldNextWrite() => Volatile.Write(ref holdNext, 1);

        public void ReleaseHeldWrite() => release.TrySetResult();

        public void ReleaseHeldWriteWithFailure() => release.TrySetException(new IOException("The held write failed."));

        public void FailWritesFromNowOn() => Volatile.Write(ref failing, 1);

        public async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken)
        {
            if (Volatile.Read(ref failing) == 1)
            {
                Interlocked.Increment(ref failedWrites);
                throw new IOException("The write failed.");
            }

            if (Interlocked.Exchange(ref holdNext, 0) == 1)
            {
                Volatile.Write(ref holding, 1);
                await release.Task;
            }

            lock (gate)
            {
                written.Append(Encoding.Latin1.GetString(buffer.Span));
            }
        }

        private readonly TaskCompletionSource release = new(TaskCreationOptions.RunContinuationsAsynchronously);

        private int holdNext;

        private int holding;

        private int failing;

        private int failedWrites;

        private int readsConsumed;

        public ValueTask FlushAsync(CancellationToken cancellationToken) => ValueTask.CompletedTask;

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    /// <summary>A clock that moves only when <see cref="Advance"/> says, firing each timer that falls due.</summary>
    private sealed class ManualTimeProvider : TimeProvider
    {
        private readonly List<ManualTimer> timers = [];

        private long now;

        private int timestampReads;

        public override long TimestampFrequency => TimeSpan.TicksPerSecond;

        public int PendingTimers
        {
            get
            {
                lock (timers)
                {
                    return timers.Count(timer => timer.IsArmed);
                }
            }
        }

        public int TimestampReads => Volatile.Read(ref timestampReads);

        public override long GetTimestamp()
        {
            Interlocked.Increment(ref timestampReads);
            return Interlocked.Read(ref now);
        }

        public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
        {
            var timer = new ManualTimer(this, callback, state);
            timer.Change(dueTime, period);
            lock (timers)
            {
                timers.Add(timer);
            }

            return timer;
        }

        public void Advance(TimeSpan by)
        {
            long target = Interlocked.Add(ref now, by.Ticks);
            ManualTimer[] due;
            lock (timers)
            {
                due = [.. timers.Where(timer => timer.IsArmed && timer.DueAt <= target)];
            }

            foreach (ManualTimer timer in due)
            {
                timer.Fire();
            }
        }

        private sealed class ManualTimer(ManualTimeProvider owner, TimerCallback callback, object? state) : ITimer
        {
            public bool IsArmed { get; private set; }

            public long DueAt { get; private set; }

            public bool Change(TimeSpan dueTime, TimeSpan period)
            {
                DueAt = owner.GetTimestamp() + dueTime.Ticks;
                IsArmed = dueTime != Timeout.InfiniteTimeSpan;
                return true;
            }

            public void Fire()
            {
                IsArmed = false;
                callback(state);
            }

            public void Dispose() => IsArmed = false;

            public ValueTask DisposeAsync()
            {
                IsArmed = false;
                return ValueTask.CompletedTask;
            }
        }
    }
}
