using System.Net;
using System.Net.Sockets;

using Curl.Api.Callbacks;
using Curl.Api.Codes;
using Curl.Api.EngineContracts;
using Curl.Api.EngineContracts.Multi;
using Curl.Api.Options;
using Curl.Api.Options.Behaviour;
using Curl.Api.Options.Network;
using Curl.Networking;
using Curl.Protocol.Abstractions;

namespace Curl.Engine;

/// <summary>
/// CGR-034: a multi handle driven by <c>curl_multi_socket_action</c> gives the socket callback the transfer's real
/// socket, <c>CURL_POLL_OUT</c> before <c>CURL_POLL_IN</c>, and the timer callback libcurl's timeouts, not a 10 ms
/// polling timer, so an application's own event loop drives the transfer (ADR-0973).
/// </summary>
[TestClass]
public sealed partial class Cgr034Tests
{
    private const nint Handle = 777;
    private static readonly TimeSpan LongestWait = TimeSpan.FromSeconds(30);
    private static readonly byte[] Response = "HTTP/1.1 200 OK\r\nContent-Length: 5\r\n\r\nhello"u8.ToArray();

    [TestMethod]
    public async Task SocketAction_OneTransfer_TellsTheRealSocketOutThenInThenRemove()
    {
        SocketDialer dialer = new(Response);
        List<string> calls = [];
        await using ManagedCurlEngine engine = EngineOver(dialer);
        await using IMultiTransfer multi = PatientMultiOf(engine);
        CurlSocket? socket = null;
        CurlPoll what = CurlPoll.None;
        multi.Options = new MultiOptions
        {
            Socket = (_, s, poll, _) =>
            {
                calls.Add($"S{s.Handle}={(int)poll}");
                (socket, what) = (s, poll);
                return 0;
            },
            Timer = milliseconds =>
            {
                calls.Add($"T{milliseconds}");
                return 0;
            },
        };
        IEasyTransfer easy = engine.CreateEasy();
        easy.Options = new EasyOptions { Network = new EasyNetworkOptions { Url = "http://127.0.0.1:9000/" } };

        multi.Add(easy);
        (CurlMultiCode code, int running) = await multi.SocketActionAsync(CurlMultiSockets.Timeout, CurlSocketSelect.None, CancellationToken.None);
        (_, int readyOnTheRealSocket) = await multi.WaitAsync(Memory<CurlWaitSocket>.Empty, TimeSpan.Zero, CancellationToken.None);
        for (int step = 0; running > 0 && step < 20; step++)
        {
            (code, running) = await multi.SocketActionAsync(socket!.Value, (CurlSocketSelect)(int)what, CancellationToken.None);
        }

        Assert.AreEqual(1, readyOnTheRealSocket, "only the connect the first action began (BL-3308); a real socket is the application's to poll");
        Assert.AreEqual(CurlMultiCode.Ok, code);
        Assert.AreEqual(0, running);
        CollectionAssert.AreEqual(new[] { "T0", "S777=2", "T-1", "S777=1", "S777=4" }, calls, string.Join(" ", calls));
        Assert.IsTrue(multi.TryReadInfo(out CurlMultiMessage? message, out _));
        Assert.AreEqual(CurlCode.Ok, message.Result);
    }

    [TestMethod]
    public async Task SocketAction_TimeoutAgainstASilentServer_TellsTheTimeoutThenRemoveWithOperationTimedOut()
    {
        SocketDialer dialer = new(null);
        List<string> calls = [];
        await using ManagedCurlEngine engine = EngineOver(dialer);
        await using IMultiTransfer multi = PatientMultiOf(engine);
        CurlSocket? socket = null;
        long timer = -1;
        multi.Options = new MultiOptions
        {
            Socket = (_, s, poll, _) =>
            {
                calls.Add($"S={(int)poll}");
                socket = s;
                return 0;
            },
            Timer = milliseconds =>
            {
                calls.Add(milliseconds >= 400 ? "T400+" : $"T{milliseconds}");
                timer = milliseconds;
                return 0;
            },
        };
        IEasyTransfer easy = engine.CreateEasy();
        easy.Options = new EasyOptions
        {
            Network = new EasyNetworkOptions { Url = "http://127.0.0.1:9000/" },
            // Long enough that a loaded runner reaches the read wait well before the deadline (BL-4027): with 1 s, a
            // Linux or macOS CI runner passed it on the way there, so the transfer ended without CURL_POLL_IN.
            Transfer = new EasyTransferOptions { Timeout = TimeSpan.FromMilliseconds(4000) },
        };

        multi.Add(easy);
        (_, int running) = await multi.SocketActionAsync(CurlMultiSockets.Timeout, CurlSocketSelect.None, CancellationToken.None);
        (_, running) = await multi.SocketActionAsync(socket!.Value, CurlSocketSelect.Out, CancellationToken.None);
        for (int step = 0; running > 0 && step < 100; step++)
        {
            // Past the deadline the timer named, not on it (BL-4082): a Windows timer can wake a few milliseconds early,
            // and the engine then tells the few left as a fresh short timeout before the transfer times out.
            await Task.Delay(TimeSpan.FromMilliseconds(Math.Max(timer, 0) + 100));
            (_, running) = await multi.SocketActionAsync(CurlMultiSockets.Timeout, CurlSocketSelect.None, CancellationToken.None);
        }

        Assert.AreEqual(0, running);
        CollectionAssert.AreEqual(new[] { "T0", "S=2", "T400+", "S=1", "S=4", "T-1" }, calls, string.Join(" ", calls));
        Assert.IsTrue(multi.TryReadInfo(out CurlMultiMessage? message, out _));
        Assert.AreEqual(CurlCode.OperationTimedOut, message.Result);
    }

    [TestMethod]
    public async Task Perform_TimerCallbackOnly_TellsZeroThenNoTimeout()
    {
        SocketDialer dialer = new(Response) { Held = true };
        List<long> timers = [];
        await using ManagedCurlEngine engine = EngineOver(dialer);
        await using IMultiTransfer multi = PatientMultiOf(engine);
        multi.Options = new MultiOptions { Timer = milliseconds => { timers.Add(milliseconds); return 0; } };
        IEasyTransfer easy = engine.CreateEasy();
        easy.Options = new EasyOptions { Network = new EasyNetworkOptions { Url = "http://127.0.0.1:9000/" } };

        multi.Add(easy);
        await multi.PerformAsync(CancellationToken.None);
        await dialer.Started.Task.WaitAsync(LongestWait);
        dialer.Release.SetResult();
        int running = 1;
        for (int step = 0; running > 0 && step < 20; step++)
        {
            await multi.WaitAsync(Memory<CurlWaitSocket>.Empty, TimeSpan.FromSeconds(1), CancellationToken.None);
            (_, running) = await multi.PerformAsync(CancellationToken.None);
        }

        Assert.AreEqual(0, running);
        CollectionAssert.AreEqual(new long[] { 0, -1 }, timers, string.Join(" ", timers));
    }

    [TestMethod]
    public async Task Perform_SocketCallbackSet_SignalsWhatTheTransferWaitsForSoPerformAloneFinishesIt()
    {
        SocketDialer dialer = new(Response);
        await using ManagedCurlEngine engine = EngineOver(dialer);
        await using IMultiTransfer multi = PatientMultiOf(engine);
        multi.Options = new MultiOptions { Socket = (_, _, _, _) => 0 };
        IEasyTransfer easy = engine.CreateEasy();
        easy.Options = new EasyOptions { Network = new EasyNetworkOptions { Url = "http://127.0.0.1:9000/" } };

        multi.Add(easy);
        int running = 1;
        for (int step = 0; running > 0 && step < 20; step++)
        {
            (_, running) = await multi.PerformAsync(CancellationToken.None);
        }

        Assert.AreEqual(0, running);
        Assert.IsTrue(multi.TryReadInfo(out CurlMultiMessage? message, out _));
        Assert.AreEqual(CurlCode.Ok, message.Result);
    }

    [TestMethod]
    public async Task SocketAction_ConnectionWithoutAnOperatingSystemSocket_TellsItsEngineSocketOutThenInThenRemove()
    {
        SocketDialer dialer = new(Response) { WithoutSocket = true };
        List<(nint Handle, CurlPoll Poll)> sockets = [];
        await using ManagedCurlEngine engine = EngineOver(dialer);
        await using IMultiTransfer multi = PatientMultiOf(engine);
        multi.Options = new MultiOptions { Socket = (_, s, poll, _) => { sockets.Add((s.Handle, poll)); return 0; } };
        IEasyTransfer easy = engine.CreateEasy();
        easy.Options = new EasyOptions { Network = new EasyNetworkOptions { Url = "http://127.0.0.1:9000/" } };

        multi.Add(easy);
        (_, int running) = await multi.SocketActionAsync(CurlMultiSockets.Timeout, CurlSocketSelect.None, CancellationToken.None);
        (CurlMultiCode waitCode, int ready) = await multi.WaitAsync(Memory<CurlWaitSocket>.Empty, LongestWait, CancellationToken.None);
        for (int step = 0; running > 0 && step < 20; step++)
        {
            (nint handle, CurlPoll poll) = sockets[^1];
            (_, running) = await multi.SocketActionAsync(new CurlSocket(handle), (CurlSocketSelect)(int)poll, CancellationToken.None);
        }

        Assert.AreEqual(CurlMultiCode.Ok, waitCode);
        Assert.AreEqual(2, ready, "the connect the first action began (BL-3308) and the engine socket the transfer waits on");
        Assert.AreEqual(0, running);
        CollectionAssert.AreEqual(new[] { CurlPoll.Out, CurlPoll.In, CurlPoll.Remove }, sockets.Select(call => call.Poll).ToArray());
        Assert.AreEqual(1, sockets.Select(call => call.Handle).Distinct().Count());
        Assert.IsGreaterThan((nint)TransferSocketWait.FirstEngineSocket, sockets[0].Handle);
        Assert.IsTrue(multi.TryReadInfo(out CurlMultiMessage? message, out _));
        Assert.AreEqual(CurlCode.Ok, message.Result);
    }

    [TestMethod]
    public async Task Add_ThreeTransfers_TellsTheTimerZeroForEach()
    {
        await using ManagedCurlEngine engine = EngineOver(new SocketDialer(Response));
        await using IMultiTransfer multi = PatientMultiOf(engine);
        List<long> timers = [];
        multi.Options = new MultiOptions { Socket = (_, _, _, _) => 0, Timer = milliseconds => { timers.Add(milliseconds); return 0; } };

        for (int index = 0; index < 3; index++)
        {
            multi.Add(engine.CreateEasy());
        }

        CollectionAssert.AreEqual(new long[] { 0, 0, 0 }, timers, string.Join(" ", timers));
    }

    // A multi handle whose actions wait as long as a loaded test machine needs for a transfer to reach its next wait.
    private static IMultiTransfer PatientMultiOf(ManagedCurlEngine engine)
    {
        var multi = (ManagedMultiTransfer)engine.CreateMulti();
        multi.SocketWaitSettleLimit = LongestWait;
        return multi;
    }

    private static ManagedCurlEngine EngineOver(ITcpDialer dialer) =>
        new(TimeProvider.System, null, dialing: new EngineDialing(dialer, new LoopbackDnsResolver(), new RecordingDatagramConnector(CurlExitCode.CouldntConnect, "no udp")));

    /// <summary>
    /// A dialer whose each connection carries its own operating system socket, from <see cref="Handle" /> up,
    /// published before the connect as <see cref="TcpDialer" /> publishes it, and answers one scripted response, or
    /// never; it can hold each connect until <see cref="Release" /> and then refuse it.
    /// </summary>
    private sealed class SocketDialer(byte[]? response) : ITcpDialer
    {
        private int dials;

        public bool Held { get; init; }

        public bool Refuses { get; init; }

        public bool WithoutSocket { get; init; }

        public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public ITcpDialer PublishingSocketsTo(Action<nint> socketCreated) => new PublishingView(this, socketCreated);

        public ValueTask<DialedTcpConnection> DialAsync(IPEndPoint endPoint, CancellationToken cancellationToken) =>
            DialPublishingAsync(_ => { }, cancellationToken);

        public ValueTask<DialedTcpConnection> DialFromAsync(IPEndPoint endPoint, IPEndPoint localEndPoint, int localPortCount, CancellationToken cancellationToken) =>
            DialAsync(endPoint, cancellationToken);

        public ValueTask<IConnection> DialUnixSocketAsync(UnixSocketAddress address, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        internal async ValueTask<DialedTcpConnection> DialPublishingAsync(Action<nint> socketCreated, CancellationToken cancellationToken)
        {
            nint socket = Handle + Interlocked.Increment(ref dials) - 1;
            if (!WithoutSocket)
            {
                socketCreated(socket);
            }

            Started.TrySetResult();
            if (Held)
            {
                await Release.Task.WaitAsync(cancellationToken);
            }

            if (Refuses)
            {
                throw new SocketException((int)SocketError.ConnectionRefused);
            }

            return new DialedTcpConnection(new SocketConnection(response, WithoutSocket ? null : socket), new IPEndPoint(IPAddress.Loopback, 50000));
        }
    }

    private sealed class PublishingView(SocketDialer dialer, Action<nint> socketCreated) : ITcpDialer
    {
        public ValueTask<DialedTcpConnection> DialAsync(IPEndPoint endPoint, CancellationToken cancellationToken) =>
            dialer.DialPublishingAsync(socketCreated, cancellationToken);

        public ValueTask<DialedTcpConnection> DialFromAsync(IPEndPoint endPoint, IPEndPoint localEndPoint, int localPortCount, CancellationToken cancellationToken) =>
            DialAsync(endPoint, cancellationToken);

        public ValueTask<IConnection> DialUnixSocketAsync(UnixSocketAddress address, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }

    private sealed class SocketConnection(byte[]? response, nint? socket) : IConnection
    {
        private bool answered;

        public bool IsSecure => false;

        public EndPoint? RemoteEndPoint => new IPEndPoint(IPAddress.Loopback, 9000);

        public nint? SocketHandle => socket;

        public async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken)
        {
            if (response is null)
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            }

            if (answered)
            {
                return 0;
            }

            answered = true;
            response!.CopyTo(buffer);
            return response!.Length;
        }

        public ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken) => ValueTask.CompletedTask;

        public ValueTask FlushAsync(CancellationToken cancellationToken) => ValueTask.CompletedTask;

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
