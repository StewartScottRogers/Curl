using System.Net;
using System.Net.Sockets;
using Curl.Api.Callbacks;
using Curl.Networking;
using Curl.Protocol.Abstractions;

namespace Curl.Engine;

/// <summary>
/// CGR-129: <c>CURLINFO_ACTIVESOCKET</c> and the <c>CURLOPT_CLOSESOCKETFUNCTION</c> argument are the connection's
/// operating system socket, as libcurl 8.22.0 gives them, not an engine number, so an application can poll the
/// socket after <c>CURLOPT_CONNECT_ONLY</c> or inspect it in its close-socket callback before closing it. The
/// loopback listeners here are connected without a byte sent.
/// </summary>
[TestClass]
public sealed class Cgr129Tests
{
    [TestMethod]
    public async Task OpenAsync_ConnectionWithAnOperatingSystemSocket_GivesThatSocket()
    {
        ConnectOnlyConnection connectOnly = new(() => true);

        await connectOnly.OpenAsync(new HandleConnection([], 4711));

        Assert.AreEqual((nint)4711, connectOnly.Socket.Handle);
        Assert.AreEqual((nint)4711, connectOnly.LiveSocket.Handle);
    }

    [TestMethod]
    public async Task ConnectAsync_OnlyCloseSocketOverARealConnection_CallsItWithTheOpenOperatingSystemSocket()
    {
        using Socket listener = Listen();
        List<(nint Handle, SocketType Type)> closed = [];
        SocketCallbackConnector connector = new(
            new TcpConnector(new LoopbackResolver(), new TcpDialer(), new PassThroughTlsProvider(), TimeProvider.System),
            new EasyCallbackOptions
            {
                CloseSocket = socket =>
                {
                    closed.Add((socket.Handle, SocketTypeOf(socket.Handle)));
                    return 0;
                },
            },
            new CurlSocketHandles(),
            TimeProvider.System);

        ConnectResult result = await connector.ConnectAsync(new("127.0.0.1", ((IPEndPoint)listener.LocalEndPoint!).Port, UseTls: false), CancellationToken.None);
        nint? handle = result.Connection!.SocketHandle;
        await result.Connection.DisposeAsync();

        Assert.IsNotNull(handle);
        Assert.HasCount(1, closed);
        Assert.AreEqual(handle.Value, closed[0].Handle);
        Assert.AreEqual(SocketType.Stream, closed[0].Type);
    }

    [TestMethod]
    public async Task ConnectAsync_OpenerAskedAndConnectionHasASocket_ClosesThatSocketBeforeTheConnection()
    {
        List<string> calls = [];
        SocketCallbackConnector connector = Create(
            new OpenerAskingConnector(opener => opener.Open(new IPEndPoint(IPAddress.Loopback, 1)), ConnectResult.Connected(new HandleConnection(calls, 5150))),
            calls);

        ConnectResult result = await connector.ConnectAsync(new("h", 1, UseTls: false), CancellationToken.None);
        await result.Connection!.DisposeAsync();

        CollectionAssert.AreEqual(new[] { "close 5150", "dispose" }, calls);
    }

    [TestMethod]
    public async Task ConnectAsync_OpenerNotAskedAndConnectionHasASocket_ClosesThatSocketBeforeTheConnection()
    {
        List<string> calls = [];
        SocketCallbackConnector connector = Create(new OpenerAskingConnector(_ => { }, ConnectResult.Connected(new HandleConnection(calls, 6060))), calls);

        ConnectResult result = await connector.ConnectAsync(new("h", 1, UseTls: false), CancellationToken.None);
        await result.Connection!.DisposeAsync();

        CollectionAssert.AreEqual(new[] { "close 6060", "dispose" }, calls);
    }

    [TestMethod]
    public async Task ConnectAsync_OpenerAskedAndConnectionHasNoSocket_ClosesTheEngineNumberAfterTheConnection()
    {
        List<string> calls = [];
        SocketCallbackConnector connector = Create(
            new OpenerAskingConnector(opener => opener.Open(new IPEndPoint(IPAddress.Loopback, 1)), ConnectResult.Connected(new HandleConnection(calls, null))),
            calls);

        ConnectResult result = await connector.ConnectAsync(new("h", 1, UseTls: false), CancellationToken.None);
        await result.Connection!.DisposeAsync();

        CollectionAssert.AreEqual(new[] { "dispose", "close 1" }, calls);
    }

    private static SocketCallbackConnector Create(IConnector inner, List<string> calls) =>
        new(
            inner,
            new EasyCallbackOptions
            {
                CloseSocket = socket =>
                {
                    calls.Add($"close {socket.Handle}");
                    return 0;
                },
            },
            new CurlSocketHandles(),
            TimeProvider.System);

    // getsockopt(SO_TYPE) on the handle, as the report's probe asks it; a handle that is no open socket throws.
    private static SocketType SocketTypeOf(nint handle)
    {
        using Socket socket = new(new SafeSocketHandle(handle, ownsHandle: false));
        return socket.SocketType;
    }

    private static Socket Listen()
    {
        Socket listener = new(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        listener.Bind(new IPEndPoint(IPAddress.Loopback, 0));
        listener.Listen();
        return listener;
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

    /// <summary>A connection over the operating system socket <paramref name="handle" />, recording its disposal in <paramref name="calls" />.</summary>
    private sealed class HandleConnection(List<string> calls, nint? handle) : IConnection
    {
        public bool IsSecure => false;

        public EndPoint? RemoteEndPoint => new IPEndPoint(IPAddress.Loopback, 1);

        public nint? SocketHandle => handle;

        public ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken) => ValueTask.FromResult(0);

        public ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken) => ValueTask.CompletedTask;

        public ValueTask FlushAsync(CancellationToken cancellationToken) => ValueTask.CompletedTask;

        public ValueTask DisposeAsync()
        {
            calls.Add("dispose");
            return ValueTask.CompletedTask;
        }
    }
}
