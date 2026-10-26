using System.Net;
using System.Net.Sockets;
using System.Text;

using Curl.Api.Codes;
using Curl.Api.EngineContracts.Multi;
using Curl.Api.Options;
using Curl.Api.Options.Network;
using Curl.Networking;
using Curl.Protocol.Abstractions;

namespace Curl.Engine;

[TestClass]
public sealed class SocketActionGatedTcpDialerTests
{
    private static readonly TimeSpan LongestWait = TimeSpan.FromSeconds(30);
    private static readonly IPEndPoint Origin = new(IPAddress.Loopback, 9000);
    private static readonly byte[] Response = "HTTP/1.1 200 OK\r\nContent-Length: 2\r\n\r\nhi"u8.ToArray();

    [TestMethod]
    public void Over_NoSocketWait_GivesTheDialerItself()
    {
        ScriptedTcpDialer dialer = new(new ScriptedConnector([]));

        Assert.AreSame(dialer, SocketActionGatedTcpDialer.Over(dialer, null));
    }

    [TestMethod]
    public async Task PerformAsync_SocketActionMode_WaitsOutDuringTheDialAndMovesBytesOnlyWhenSignalled()
    {
        ScriptedConnector server = new([Response]);
        HeldTcpDialer dialer = new(new ScriptedTcpDialer(server));
        EngineDialing dialing = new(dialer, new LoopbackDnsResolver(), new RecordingDatagramConnector(CurlExitCode.CouldntConnect, "no udp"));
        await using ManagedCurlEngine engine = new(TimeProvider.System, null, dialing: dialing);
        await using ManagedEasyTransfer easy = (ManagedEasyTransfer)engine.CreateEasy();
        TransferSocketWait wait = new(TimeProvider.System);
        easy.SocketWait = wait;
        easy.Options = new EasyOptions { Network = new EasyNetworkOptions { Url = "http://127.0.0.1:9000/" } };

        Task<CurlCode> perform = easy.PerformAsync(CancellationToken.None).AsTask();
        await dialer.Started.Task.WaitAsync(LongestWait);
        CurlPoll waitingDuringDial = wait.Waiting;
        DateTimeOffset? deadlineDuringDial = wait.EarliestDeadline;
        dialer.Release.SetResult();
        await UntilAsync(wait, () => wait.Waiting != CurlPoll.None);
        CurlPoll firstWait = wait.Waiting;
        int writtenBeforeSignal = server.Written.Length;
        int signals = 0;
        while (!perform.IsCompleted)
        {
            await Task.WhenAny(perform, UntilAsync(wait, () => wait.Waiting != CurlPoll.None));
            signals += SignalWhatWaits(wait);
        }

        Assert.AreEqual(CurlCode.Ok, await perform);
        Assert.AreEqual(CurlPoll.Out, waitingDuringDial);
        Assert.IsNotNull(deadlineDuringDial);
        Assert.AreEqual(CurlPoll.Out, firstWait);
        Assert.AreEqual(0, writtenBeforeSignal);
        Assert.IsGreaterThanOrEqualTo(2, signals);
        Assert.StartsWith("GET / HTTP/1.1\r\n", Encoding.ASCII.GetString(server.Written));
    }

    [TestMethod]
    public async Task PerformAsync_NoSocketWait_DialsTheConnectionUngated()
    {
        ScriptedConnector server = new([Response]);
        HeldTcpDialer dialer = new(new ScriptedTcpDialer(server));
        dialer.Release.SetResult();
        EngineDialing dialing = new(dialer, new LoopbackDnsResolver(), new RecordingDatagramConnector(CurlExitCode.CouldntConnect, "no udp"));
        await using ManagedCurlEngine engine = new(TimeProvider.System, null, dialing: dialing);
        await using ManagedEasyTransfer easy = (ManagedEasyTransfer)engine.CreateEasy();
        easy.Options = new EasyOptions { Network = new EasyNetworkOptions { Url = "http://127.0.0.1:9000/" } };

        CurlCode code = await easy.PerformAsync(CancellationToken.None).AsTask().WaitAsync(LongestWait);

        Assert.AreEqual(CurlCode.Ok, code);
        Assert.IsNotInstanceOfType<SocketActionGatedConnection>(dialer.Dialed);
    }

    [TestMethod]
    public async Task DialFromAsync_GatesTheConnectionAndEndsTheConnect()
    {
        TransferSocketWait wait = new(TimeProvider.System);
        ScriptedTcpDialer inner = new(new ScriptedConnector([]));
        SocketActionGatedTcpDialer dialer = new(inner, wait);

        DialedTcpConnection dialed = await dialer.DialFromAsync(Origin, new IPEndPoint(IPAddress.Any, 4000), 2, CancellationToken.None);

        Assert.IsInstanceOfType<SocketActionGatedConnection>(dialed.Connection);
        Assert.AreEqual(new IPEndPoint(IPAddress.Loopback, 50000), dialed.LocalEndPoint);
        Assert.AreEqual(CurlPoll.None, wait.Waiting);
        Assert.AreEqual((new IPEndPoint(IPAddress.Any, 4000), 2), inner.LocalBinds.Single());
    }

    [TestMethod]
    public async Task DialFromAsync_WithEvents_GatesTheConnection()
    {
        TransferSocketWait wait = new(TimeProvider.System);
        ScriptedTcpDialer inner = new(new ScriptedConnector([]));
        SocketActionGatedTcpDialer dialer = new(inner, wait);

        DialedTcpConnection dialed = await dialer.DialFromAsync(Origin, new IPEndPoint(IPAddress.Any, 0), 1, NoTransferEvents.Instance, CancellationToken.None);

        Assert.IsInstanceOfType<SocketActionGatedConnection>(dialed.Connection);
        Assert.HasCount(1, inner.LocalBinds);
    }

    [TestMethod]
    public async Task DialFromDeviceAsync_GatesTheConnection()
    {
        TransferSocketWait wait = new(TimeProvider.System);
        ScriptedTcpDialer inner = new(new ScriptedConnector([]));
        SocketActionGatedTcpDialer dialer = new(inner, wait);

        DialedTcpConnection dialed = await dialer.DialFromDeviceAsync(
            Origin, "eth0", bindsAddressAfterDevice: false, _ => ValueTask.FromResult(new IPEndPoint(IPAddress.Any, 0)), 1, NoTransferEvents.Instance, CancellationToken.None);

        Assert.IsInstanceOfType<SocketActionGatedConnection>(dialed.Connection);
        Assert.HasCount(1, inner.LocalBinds);
    }

    [TestMethod]
    public async Task DialUnixSocketAsync_GatesTheConnectionAndEndsTheConnect()
    {
        TransferSocketWait wait = new(TimeProvider.System);
        SocketActionGatedTcpDialer dialer = new(new ScriptedTcpDialer(new ScriptedConnector([])), wait);

        IConnection connection = await dialer.DialUnixSocketAsync(new UnixSocketAddress("/tmp/s", IsAbstract: false), CancellationToken.None);

        Assert.IsInstanceOfType<SocketActionGatedConnection>(connection);
        Assert.AreEqual(CurlPoll.None, wait.Waiting);
    }

    [TestMethod]
    public void FailureToOpenSocket_AsksTheInnerDialer()
    {
        SocketActionGatedTcpDialer dialer = new(new RefusingTcpDialer(), new TransferSocketWait(TimeProvider.System));

        Assert.AreEqual(SocketError.ProtocolNotSupported, dialer.FailureToOpenSocket(AddressFamily.InterNetwork)?.SocketErrorCode);
    }

    private static int SignalWhatWaits(TransferSocketWait wait)
    {
        CurlPoll waiting = wait.Waiting;
        CurlSocketSelect events = ((waiting & CurlPoll.In) != 0 ? CurlSocketSelect.In : CurlSocketSelect.None)
            | ((waiting & CurlPoll.Out) != 0 ? CurlSocketSelect.Out : CurlSocketSelect.None);
        if (events == CurlSocketSelect.None)
        {
            return 0;
        }

        wait.Signal(events);
        return 1;
    }

    private static async Task UntilAsync(TransferSocketWait wait, Func<bool> condition)
    {
        TaskCompletionSource met = new(TaskCreationOptions.RunContinuationsAsynchronously);
        void Check()
        {
            if (condition())
            {
                met.TrySetResult();
            }
        }

        wait.Changed += Check;
        try
        {
            Check();
            await met.Task.WaitAsync(LongestWait);
        }
        finally
        {
            wait.Changed -= Check;
        }
    }

    /// <summary>A dialer whose dials wait until the test releases them, keeping the last connection dialed.</summary>
    private sealed class HeldTcpDialer(ITcpDialer inner) : ITcpDialer
    {
        public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public IConnection? Dialed { get; private set; }

        public async ValueTask<DialedTcpConnection> DialAsync(IPEndPoint endPoint, CancellationToken cancellationToken)
        {
            Started.TrySetResult();
            await Release.Task.WaitAsync(cancellationToken);
            DialedTcpConnection dialed = await inner.DialAsync(endPoint, cancellationToken);
            Dialed = dialed.Connection;
            return dialed;
        }

        public ValueTask<DialedTcpConnection> DialFromAsync(IPEndPoint endPoint, IPEndPoint localEndPoint, int localPortCount, CancellationToken cancellationToken) =>
            DialAsync(endPoint, cancellationToken);

        public ValueTask<IConnection> DialUnixSocketAsync(UnixSocketAddress address, CancellationToken cancellationToken) =>
            inner.DialUnixSocketAsync(address, cancellationToken);
    }

    /// <summary>A dialer the operating system refuses every socket of.</summary>
    private sealed class RefusingTcpDialer : ITcpDialer
    {
        public ValueTask<DialedTcpConnection> DialAsync(IPEndPoint endPoint, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public ValueTask<DialedTcpConnection> DialFromAsync(IPEndPoint endPoint, IPEndPoint localEndPoint, int localPortCount, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public ValueTask<IConnection> DialUnixSocketAsync(UnixSocketAddress address, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public SocketException? FailureToOpenSocket(AddressFamily family) => new((int)SocketError.ProtocolNotSupported);
    }
}
