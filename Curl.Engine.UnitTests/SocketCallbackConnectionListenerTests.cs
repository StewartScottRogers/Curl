using System.Net;
using Curl.Api.Callbacks;
using Curl.Protocol.Abstractions;

namespace Curl.Engine;

[TestClass]
public sealed class SocketCallbackConnectionListenerTests
{
    private static readonly ListenTarget Target = new(IPAddress.Loopback, 0, 0);

    // test596's order: [OPEN] for the listening socket, its [CLOSE] once the peer is accepted, and none for the
    // accepted connection (BL-2588).
    [TestMethod]
    public async Task ListenAsync_OpenAndCloseSet_OpensAtListenAndClosesOnceAtAccept()
    {
        List<string> calls = [];
        CurlSocketAddress? seen = null;
        FakePendingConnection pending = new();
        SocketCallbackConnectionListener listener = Create(new FakeListener(ListenResult.Listening(pending)), new EasyCallbackOptions
        {
            OpenSocket = (purpose, address) =>
            {
                seen = address;
                calls.Add($"open {purpose}");
                return new CurlSocket(2);
            },
            CloseSocket = socket =>
            {
                calls.Add($"close {socket.Handle}");
                return 0;
            },
        });

        ListenResult listen = await listener.ListenAsync(Target, CancellationToken.None);
        IPendingConnection wrapped = listen.PendingConnection!;
        calls.Add("listening");
        ConnectResult accepted = await wrapped.AcceptAsync(CancellationToken.None);
        await wrapped.DisposeAsync();

        CollectionAssert.AreEqual(new[] { "open IpConnection", "listening", "close 2" }, calls);
        Assert.AreSame(pending.Accepted, accepted);
        Assert.AreSame(pending.LocalEndPoint, wrapped.LocalEndPoint);
        Assert.IsTrue(pending.Disposed);
        Assert.AreEqual(2, seen!.Family);
        Assert.AreEqual(SocketCallbackConnector.StreamSocketType, seen.SocketType);
    }

    [TestMethod]
    public async Task ListenAsync_NeverAccepted_ClosesOnceWhenTheListeningStops()
    {
        List<nint> closed = [];
        FakePendingConnection pending = new();
        SocketCallbackConnectionListener listener = Create(new FakeListener(ListenResult.Listening(pending)), new EasyCallbackOptions
        {
            CloseSocket = socket =>
            {
                closed.Add(socket.Handle);
                return 0;
            },
        });

        ListenResult listen = await listener.ListenAsync(Target, CancellationToken.None);
        await listen.PendingConnection!.DisposeAsync();
        await listen.PendingConnection!.DisposeAsync();

        CollectionAssert.AreEqual(new nint[] { 1 }, closed);
        Assert.IsTrue(pending.Disposed);
    }

    [TestMethod]
    public async Task ListenAsync_OpenSocketReturnsBad_StopsListeningAndFailsWithFtpPortFailed()
    {
        int closes = 0;
        FakePendingConnection pending = new();
        SocketCallbackConnectionListener listener = Create(new FakeListener(ListenResult.Listening(pending)), new EasyCallbackOptions
        {
            OpenSocket = (_, _) => CurlSocket.Bad,
            CloseSocket = _ => ++closes,
        });

        ListenResult listen = await listener.ListenAsync(Target, CancellationToken.None);

        Assert.AreEqual(CurlExitCode.FtpPortFailed, listen.ExitCode);
        Assert.AreEqual(SocketCallbackConnectionListener.SocketFailureText, listen.ErrorMessage);
        Assert.IsTrue(pending.Disposed);
        Assert.AreEqual(0, closes);
    }

    [TestMethod]
    public async Task ListenAsync_InnerFails_GivesItsFailureWithoutCallingTheCallbacks()
    {
        int calls = 0;
        ListenResult failed = ListenResult.Failed(CurlExitCode.FtpPortFailed, "bind failed");
        SocketCallbackConnectionListener listener = Create(new FakeListener(failed), new EasyCallbackOptions
        {
            OpenSocket = (_, _) => new CurlSocket(++calls),
            CloseSocket = _ => ++calls,
        });

        ListenResult listen = await listener.ListenAsync(Target, CancellationToken.None);

        Assert.AreSame(failed, listen);
        Assert.AreEqual(0, calls);
    }

    [TestMethod]
    public void Over_NoOpenOrCloseSocketSet_GivesTheListenerItself()
    {
        FakeListener inner = new(ListenResult.Failed(CurlExitCode.FtpPortFailed, "x"));
        ManagedEngineSharedState shared = new(TimeProvider.System, _ => string.Empty);

        Assert.AreSame(inner, SocketCallbackConnectionListener.Over(inner, new EasyCallbackOptions { SocketOption = (_, _) => SocketOptionResult.Ok }, shared));
        Assert.IsInstanceOfType<SocketCallbackConnectionListener>(SocketCallbackConnectionListener.Over(inner, new EasyCallbackOptions { OpenSocket = (_, _) => CurlSocket.Bad }, shared));
        Assert.IsInstanceOfType<SocketCallbackConnectionListener>(SocketCallbackConnectionListener.Over(inner, new EasyCallbackOptions { CloseSocket = _ => 0 }, shared));
    }

    private static SocketCallbackConnectionListener Create(IConnectionListener inner, EasyCallbackOptions callbacks) =>
        new(inner, callbacks, new CurlSocketHandles());

    /// <summary>Answers every listen with one result.</summary>
    private sealed class FakeListener(ListenResult result) : IConnectionListener
    {
        public ValueTask<ListenResult> ListenAsync(ListenTarget target, CancellationToken cancellationToken) => ValueTask.FromResult(result);
    }

    /// <summary>A pending connection whose accept answers one failed result and which records its disposal.</summary>
    private sealed class FakePendingConnection : IPendingConnection
    {
        public EndPoint LocalEndPoint { get; } = new IPEndPoint(IPAddress.Loopback, 4021);

        public ConnectResult Accepted { get; } = ConnectResult.Failed(CurlExitCode.FtpAcceptFailed, "accept answered");

        public bool Disposed { get; private set; }

        public ValueTask<ConnectResult> AcceptAsync(CancellationToken cancellationToken) => ValueTask.FromResult(Accepted);

        public ValueTask DisposeAsync()
        {
            Disposed = true;
            return ValueTask.CompletedTask;
        }
    }
}
