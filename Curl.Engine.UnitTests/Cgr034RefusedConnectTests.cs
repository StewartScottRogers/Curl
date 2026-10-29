using System.Net;
using System.Net.Sockets;

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
/// CGR-034 <c>C_65</c> with a connect refused at once: the socket the socket callback is told of stays open until its
/// removal, held by <see cref="TransferSocketWait.HoldFailedConnectAsync" /> until the application's action on it,
/// and no zero timer comes between the announcement and that action, as libcurl's sequence <c>T0 add S1=2 T-1 kick
/// S1=4</c>. The stand-ins they drive are <see cref="Cgr034Tests" />'s.
/// </summary>
public sealed partial class Cgr034Tests
{
    [TestMethod]
    public async Task SocketAction_ConnectRefusedAtOnce_KeepsTheSocketOpenUntilItsRemoveAndAsksNoZeroTimer()
    {
        List<string> calls = [];
        RefusingAtOnceDialer dialer = new(calls);
        await using ManagedCurlEngine engine = EngineOver(dialer);
        await using IMultiTransfer multi = PatientMultiOf(engine);
        CurlSocket? socket = null;
        multi.Options = new MultiOptions
        {
            Socket = (_, s, poll, _) =>
            {
                lock (calls)
                {
                    calls.Add($"S={(int)poll}");
                }

                socket = s;
                return 0;
            },
            Timer = milliseconds =>
            {
                lock (calls)
                {
                    calls.Add($"T{milliseconds}");
                }

                return 0;
            },
        };
        IEasyTransfer easy = engine.CreateEasy();
        easy.Options = new EasyOptions { Network = new EasyNetworkOptions { Url = "http://127.0.0.1:9000/" } };

        multi.Add(easy);
        await multi.SocketActionAsync(CurlMultiSockets.Timeout, CurlSocketSelect.None, CancellationToken.None);
        lock (calls)
        {
            calls.Add("kick");
        }

        (CurlMultiCode code, int running) = await multi.SocketActionAsync(socket!.Value, CurlSocketSelect.Error, CancellationToken.None);

        Assert.AreEqual(CurlMultiCode.Ok, code);
        Assert.AreEqual(0, running);
        Assert.AreEqual(new CurlSocket(Handle), socket);
        CollectionAssert.AreEqual(new[] { "T0", "S=2", "T-1", "kick", "closed", "S=4" }, calls, string.Join(" ", calls));
        Assert.IsTrue(multi.TryReadInfo(out CurlMultiMessage? message, out _));
        Assert.AreEqual(CurlCode.CouldntConnect, message.Result);
    }

    [TestMethod]
    public async Task HoldFailedConnect_ReleasedBySignalOutAndNotHeldAfterIt()
    {
        TransferSocketWait wait = new(TimeProvider.System);
        wait.BeginConnect();
        ValueTask first = wait.HoldFailedConnectAsync(CancellationToken.None);
        ValueTask second = wait.HoldFailedConnectAsync(CancellationToken.None);
        wait.Signal(CurlSocketSelect.In);
        Assert.IsFalse(first.IsCompleted);

        wait.Signal(CurlSocketSelect.Out);
        await first;
        await second;

        Assert.IsTrue(wait.HoldFailedConnectAsync(CancellationToken.None).IsCompleted);
    }

    [TestMethod]
    public async Task HoldFailedConnect_EndedByStopGatingOrCancellation()
    {
        TransferSocketWait stopped = new(TimeProvider.System);
        stopped.BeginConnect();
        ValueTask held = stopped.HoldFailedConnectAsync(CancellationToken.None);
        stopped.StopGating();
        await held;
        Assert.IsTrue(stopped.HoldFailedConnectAsync(CancellationToken.None).IsCompleted);

        TransferSocketWait cancelled = new(TimeProvider.System);
        cancelled.BeginConnect();
        using CancellationTokenSource cancellation = new();
        ValueTask waiting = cancelled.HoldFailedConnectAsync(cancellation.Token);
        await cancellation.CancelAsync();
        await waiting;
    }

    /// <summary>
    /// A dialer whose connect is refused as soon as it starts, publishing <see cref="Handle" /> first and awaiting the
    /// hold <see cref="ITcpDialer.HoldingFailedSocketsUntil" /> gave before it records the socket <c>closed</c>.
    /// </summary>
    private sealed class RefusingAtOnceDialer(List<string> calls) : ITcpDialer
    {
        private Action<nint> socketCreated = _ => { };
        private Func<CancellationToken, ValueTask> released = _ => ValueTask.CompletedTask;

        public ITcpDialer PublishingSocketsTo(Action<nint> socketCreated)
        {
            this.socketCreated = socketCreated;
            return this;
        }

        public ITcpDialer HoldingFailedSocketsUntil(Func<CancellationToken, ValueTask> released)
        {
            this.released = released;
            return this;
        }

        public async ValueTask<DialedTcpConnection> DialAsync(IPEndPoint endPoint, CancellationToken cancellationToken)
        {
            socketCreated(Handle);
            await released(cancellationToken);
            lock (calls)
            {
                calls.Add("closed");
            }

            throw new SocketException((int)SocketError.ConnectionRefused);
        }

        public ValueTask<DialedTcpConnection> DialFromAsync(IPEndPoint endPoint, IPEndPoint localEndPoint, int localPortCount, CancellationToken cancellationToken) =>
            DialAsync(endPoint, cancellationToken);

        public ValueTask<IConnection> DialUnixSocketAsync(UnixSocketAddress address, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }
}
