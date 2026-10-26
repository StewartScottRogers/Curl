using System.Net;
using System.Net.Sockets;
using Curl.Api.Callbacks;
using Curl.Networking;
using Curl.Protocol.Abstractions;

namespace Curl.Engine;

/// <summary>
/// CGR-036: the open-socket and socket-option callbacks run before each TCP attempt dials, as curl 8.22.0's
/// <c>cf_socket_open</c> calls them, so a refusal opens no connection, a socket the application supplies is the one
/// used, and a failed attempt's socket goes to the close-socket callback. The loopback listeners here are connected
/// without a byte sent.
/// </summary>
[TestClass]
public sealed class Cgr036Tests
{
    [TestMethod]
    public async Task ConnectAsync_OpenSocketReturnsBad_MakesNoConnectionAndFailsWithCouldntConnect()
    {
        using Socket listener = Listen();
        List<string> calls = [];
        SocketCallbackConnector connector = Create(new EasyCallbackOptions
        {
            OpenSocket = (_, _) =>
            {
                calls.Add("open");
                return CurlSocket.Bad;
            },
            CloseSocket = _ =>
            {
                calls.Add("close");
                return 0;
            },
        });

        ConnectResult result = await connector.ConnectAsync(TargetOf(listener), CancellationToken.None);

        Assert.AreEqual(CurlExitCode.CouldntConnect, result.ExitCode);
        StringAssert.EndsWith(result.ErrorMessage, " ms: " + SocketCallbackConnector.CouldntConnectText);
        Assert.IsFalse(listener.Poll(100_000, SelectMode.SelectRead), "the listener has a connection waiting");
        CollectionAssert.AreEqual(new[] { "open" }, calls);
    }

    [TestMethod]
    public async Task ConnectAsync_ClosedPort_CallsOpenWithTheAddressThenCloseOnceAndFailsWithCouldntConnect()
    {
        int port = ClosedLoopbackPort();
        List<CurlSocketAddress> opens = [];
        int closes = 0;
        using Socket socket = new(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        SocketCallbackConnector connector = Create(new EasyCallbackOptions
        {
            OpenSocket = (_, address) =>
            {
                opens.Add(address);
                return new CurlSocket(socket.Handle);
            },
            CloseSocket = _ => ++closes,
        });

        ConnectResult result = await connector.ConnectAsync(new ConnectTarget("127.0.0.1", port, UseTls: false), CancellationToken.None);

        Assert.AreEqual(CurlExitCode.CouldntConnect, result.ExitCode);
        Assert.HasCount(1, opens);
        Assert.AreEqual(2, opens[0].Family);
        Assert.AreEqual(SocketCallbackConnector.StreamSocketType, opens[0].SocketType);
        Assert.AreEqual(SocketCallbackConnector.TcpProtocol, opens[0].Protocol);
        CollectionAssert.AreEqual(SocketCallbackConnector.AddressOf(new IPEndPoint(IPAddress.Loopback, port)).Address.ToArray(), opens[0].Address.ToArray());
        Assert.AreEqual(1, closes);
    }

    [TestMethod]
    public async Task ConnectAsync_OpenSocketSuppliesAConnectedSocket_UsesItAndNotTheTargetsAddress()
    {
        using Socket target = Listen();
        using Socket other = Listen();
        using Socket supplied = new(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        await supplied.ConnectAsync(other.LocalEndPoint!);
        using Socket accepted = await other.AcceptAsync();
        List<string> calls = [];
        SocketCallbackConnector connector = Create(new EasyCallbackOptions
        {
            OpenSocket = (_, _) => new CurlSocket(supplied.Handle),
            SocketOption = (_, _) => SocketOptionResult.AlreadyConnected,
            CloseSocket = closed =>
            {
                calls.Add(closed.Handle == supplied.Handle ? "close supplied" : "close other");
                return 0;
            },
        });

        ConnectResult result = await connector.ConnectAsync(TargetOf(target), CancellationToken.None);

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        IConnection connection = result.Connection!;
        Assert.AreEqual(accepted.RemoteEndPoint, connection.LocalEndPoint);
        Assert.IsFalse(target.Poll(100_000, SelectMode.SelectRead), "the target's listener has a connection waiting");
        Assert.IsEmpty(calls);
        await connection.DisposeAsync();
        CollectionAssert.AreEqual(new[] { "close supplied" }, calls);
    }

    [TestMethod]
    public async Task ConnectAsync_OpenSocketSuppliesAnUnconnectedSocket_ConnectsThatSocketToTheTarget()
    {
        using Socket target = Listen();
        using Socket supplied = new(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        SocketCallbackConnector connector = Create(new EasyCallbackOptions { OpenSocket = (_, _) => new CurlSocket(supplied.Handle) });

        ConnectResult result = await connector.ConnectAsync(TargetOf(target), CancellationToken.None);

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        await using IConnection connection = result.Connection!;
        Assert.AreEqual(supplied.Handle, connection.SocketHandle);
        using Socket accepted = await target.AcceptAsync();
        Assert.AreEqual(accepted.RemoteEndPoint, connection.LocalEndPoint);
    }

    [TestMethod]
    public async Task ConnectAsync_SockoptReturnsError_MakesNoConnectionClosesTheSocketAndFailsWithAbortedByCallback()
    {
        using Socket listener = Listen();
        List<string> calls = [];
        SocketCallbackConnector connector = Create(new EasyCallbackOptions
        {
            SocketOption = (socket, _) =>
            {
                calls.Add($"sockopt {socket.Handle}");
                return SocketOptionResult.Error;
            },
            CloseSocket = socket =>
            {
                calls.Add($"close {socket.Handle}");
                return 0;
            },
        });

        ConnectResult result = await connector.ConnectAsync(TargetOf(listener), CancellationToken.None);

        Assert.AreEqual(CurlExitCode.AbortedByCallback, result.ExitCode);
        StringAssert.EndsWith(result.ErrorMessage, " ms: " + SocketCallbackConnector.AbortedByCallbackText);
        Assert.IsFalse(listener.Poll(100_000, SelectMode.SelectRead), "the listener has a connection waiting");
        CollectionAssert.AreEqual(new[] { "sockopt 1", "close 1" }, calls);
    }

    [TestMethod]
    public async Task ConnectAsync_InnerFailsAfterAnAttemptOpened_ClosesThatSocketAndReturnsTheInnerFailure()
    {
        int closes = 0;
        ConnectResult failure = ConnectResult.Failed(CurlExitCode.SslConnectError, "handshake failed");
        SocketCallbackConnector connector = Create(
            new OpenerAskingConnector(opener => opener.Open(new IPEndPoint(IPAddress.Loopback, 1)), failure),
            new EasyCallbackOptions { CloseSocket = _ => ++closes });

        ConnectResult result = await connector.ConnectAsync(new ConnectTarget("h", 1, UseTls: true), CancellationToken.None);

        Assert.AreSame(failure, result);
        Assert.AreEqual(1, closes);
    }

    [TestMethod]
    public async Task ConnectAsync_ConnectedAfterTheOpenerLostTheAttempt_GivesASocketNoCallbackSaw()
    {
        int closes = 0;
        IPEndPoint endPoint = new(IPAddress.Loopback, 1);
        SocketCallbackConnector connector = Create(
            new OpenerAskingConnector(
                opener =>
                {
                    opener.Open(endPoint);
                    opener.SocketClosed(endPoint);
                },
                ConnectResult.Connected(new ClosingConnection())),
            new EasyCallbackOptions { CloseSocket = _ => ++closes });

        ConnectResult result = await connector.ConnectAsync(new ConnectTarget("h", 1, UseTls: false), CancellationToken.None);

        await result.Connection!.DisposeAsync();
        Assert.AreEqual(1, closes);
    }

    [TestMethod]
    public void AttemptSocketOpener_NotSupplyingSockets_ProceedsWithTheDialersOwnSocket()
    {
        List<string> calls = [];
        SocketCallbackConnector.AttemptSocketOpener opener = new(
            new EasyCallbackOptions
            {
                OpenSocket = (_, _) =>
                {
                    calls.Add("open");
                    return new CurlSocket(1960);
                },
                SocketOption = (_, _) => SocketOptionResult.AlreadyConnected,
            },
            new CurlSocketHandles(),
            suppliesSockets: false);

        TcpSocketOpenDecision decision = opener.Open(new IPEndPoint(IPAddress.Loopback, 1));

        Assert.AreSame(TcpSocketOpenDecision.Proceed, decision);
        CollectionAssert.AreEqual(new[] { "open" }, calls);
        Assert.AreEqual((nint)1960, opener.TakeOpened()!.Socket.Handle);
    }

    [TestMethod]
    public void Over_EngineDialsThroughAnEngineDialing_WrapsTheConnector()
    {
        ManagedEngineSharedState shared = new(TimeProvider.System, _ => string.Empty, dialing: new EngineDialing(new TcpDialer(), new LoopbackResolver(), null!));

        Assert.IsInstanceOfType<SocketCallbackConnector>(SocketCallbackConnector.Over(new OpenerAskingConnector(_ => { }, ConnectResult.Failed(CurlExitCode.CouldntConnect, "x")), new EasyCallbackOptions { CloseSocket = _ => 0 }, shared));
    }

    [TestMethod]
    public void AttemptSocketOpener_SocketClosedForAnAddressNotOpen_CallsNothing()
    {
        int closes = 0;
        SocketCallbackConnector.AttemptSocketOpener opener = new(new EasyCallbackOptions { CloseSocket = _ => ++closes }, new CurlSocketHandles(), suppliesSockets: true);

        opener.SocketClosed(new IPEndPoint(IPAddress.Loopback, 1));

        Assert.AreEqual(0, closes);
        Assert.IsFalse(opener.WasAsked);
        Assert.IsNull(opener.TakeOpened());
    }

    [TestMethod]
    public void AttemptSocketOpener_TwoAttemptsStillOpen_TakesTheLastAndClosesTheOther()
    {
        List<nint> closed = [];
        SocketCallbackConnector.AttemptSocketOpener opener = new(
            new EasyCallbackOptions
            {
                CloseSocket = socket =>
                {
                    closed.Add(socket.Handle);
                    return 0;
                },
            },
            new CurlSocketHandles(),
            suppliesSockets: true);
        Assert.AreSame(TcpSocketOpenDecision.Proceed, opener.Open(new IPEndPoint(IPAddress.Loopback, 1)));
        opener.Open(new IPEndPoint(IPAddress.Loopback, 2));

        CurlSocketCloser? taken = opener.TakeOpened();

        Assert.AreEqual((nint)2, taken!.Socket.Handle);
        CollectionAssert.AreEqual(new nint[] { 1 }, closed);
        Assert.AreEqual(CurlExitCode.Ok, opener.Refusal);
    }

    private static SocketCallbackConnector Create(EasyCallbackOptions callbacks) =>
        Create(new TcpConnector(new LoopbackResolver(), new TcpDialer(), new PassThroughTlsProvider(), TimeProvider.System), callbacks);

    private static SocketCallbackConnector Create(IConnector inner, EasyCallbackOptions callbacks) =>
        new(inner, callbacks, new CurlSocketHandles(), TimeProvider.System);

    private static ConnectTarget TargetOf(Socket listener) => new("127.0.0.1", ((IPEndPoint)listener.LocalEndPoint!).Port, UseTls: false);

    private static Socket Listen()
    {
        Socket listener = new(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        listener.Bind(new IPEndPoint(IPAddress.Loopback, 0));
        listener.Listen();
        return listener;
    }

    /// <summary>A loopback port nothing listens on: one the system just handed a socket, now closed.</summary>
    private static int ClosedLoopbackPort()
    {
        using Socket socket = new(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        socket.Bind(new IPEndPoint(IPAddress.Loopback, 0));
        return ((IPEndPoint)socket.LocalEndPoint!).Port;
    }

    private sealed class LoopbackResolver : IDnsResolver
    {
        public ValueTask<IReadOnlyList<IPAddress>> ResolveAsync(string host, CancellationToken cancellationToken) =>
            ValueTask.FromResult<IReadOnlyList<IPAddress>>([IPAddress.Loopback]);
    }

    /// <summary>A connector that runs <paramref name="ask" /> on the target's opener, then answers <paramref name="result" />.</summary>
    private sealed class OpenerAskingConnector(Action<ITcpSocketOpener> ask, ConnectResult result) : IConnector
    {
        public ValueTask<ConnectResult> ConnectAsync(ConnectTarget target, CancellationToken cancellationToken)
        {
            ask(target.SocketOpener!);
            return ValueTask.FromResult(result);
        }
    }

    private sealed class ClosingConnection : IConnection
    {
        public bool IsSecure => false;

        public EndPoint? RemoteEndPoint => null;

        public ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken) => ValueTask.FromResult(0);

        public ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken) => ValueTask.CompletedTask;

        public ValueTask FlushAsync(CancellationToken cancellationToken) => ValueTask.CompletedTask;

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
