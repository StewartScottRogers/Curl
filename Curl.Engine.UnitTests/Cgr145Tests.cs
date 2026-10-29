using System.Net;
using System.Net.Sockets;
using Curl.Api.Callbacks;
using Curl.Networking;
using Curl.Protocol.Abstractions;

namespace Curl.Engine;

/// <summary>
/// CGR-145: without an open-socket callback, the socket-option callback gets an operating system socket the engine
/// opened before the connect, as curl 8.22.0's <c>cf_socket_open</c> gives it, so <c>setsockopt</c> works on it;
/// <c>CURL_SOCKOPT_ERROR</c> makes no connection and <c>CURL_SOCKOPT_ALREADY_CONNECTED</c> leaves that socket
/// unconnected. The loopback listeners here are connected without a byte sent.
/// </summary>
[TestClass]
public sealed class Cgr145Tests
{
    [TestMethod]
    public async Task ConnectAsync_SockoptWithoutOpenSocket_GetsTheConnectionsOwnStreamSocketBeforeTheConnect()
    {
        using Socket listener = Listen();
        List<string> calls = [];
        SocketCallbackConnector connector = Create(new EasyCallbackOptions
        {
            SocketOption = (socket, _) =>
            {
                using Socket view = new(new SafeSocketHandle(socket.Handle, ownsHandle: false));
                calls.Add($"sockopt {view.SocketType} connected={view.Connected} pending={listener.Poll(0, SelectMode.SelectRead)}");
                return SocketOptionResult.Ok;
            },
        });

        ConnectResult result = await connector.ConnectAsync(TargetOf(listener), CancellationToken.None);

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        await using IConnection connection = result.Connection!;
        Assert.AreEqual(((SocketCallbackConnection)connection).Socket.Handle, connection.SocketHandle);
        CollectionAssert.AreEqual(new[] { "sockopt Stream connected=False pending=False" }, calls);
        using Socket accepted = await listener.AcceptAsync();
        Assert.AreEqual(accepted.RemoteEndPoint, connection.LocalEndPoint);
    }

    [TestMethod]
    public async Task ConnectAsync_SockoptReturnsErrorWithoutOpenSocket_ClosesTheSocketItSawAndMakesNoConnection()
    {
        using Socket listener = Listen();
        List<nint> seen = [];
        List<nint> closed = [];
        SocketCallbackConnector connector = Create(new EasyCallbackOptions
        {
            SocketOption = (socket, _) =>
            {
                seen.Add(socket.Handle);
                return SocketOptionResult.Error;
            },
            CloseSocket = socket =>
            {
                closed.Add(socket.Handle);
                return 0;
            },
        });

        ConnectResult result = await connector.ConnectAsync(TargetOf(listener), CancellationToken.None);

        Assert.AreEqual(CurlExitCode.AbortedByCallback, result.ExitCode);
        Assert.IsFalse(listener.Poll(100_000, SelectMode.SelectRead), "the listener has a connection waiting");
        CollectionAssert.AreEqual(seen, closed);
        Assert.AreNotEqual((nint)1, seen.Single());
    }

    [TestMethod]
    public async Task ConnectAsync_SockoptReturnsAlreadyConnectedWithoutOpenSocket_ConnectsNothingSoTheFirstWriteFails()
    {
        using Socket listener = Listen();
        List<string> calls = [];
        nint seen = 0;
        SocketCallbackConnector connector = Create(new EasyCallbackOptions
        {
            SocketOption = (socket, _) =>
            {
                seen = socket.Handle;
                return SocketOptionResult.AlreadyConnected;
            },
            CloseSocket = socket =>
            {
                calls.Add("close");
                return 0;
            },
        });

        ConnectResult result = await connector.ConnectAsync(TargetOf(listener), CancellationToken.None);

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        IConnection connection = result.Connection!;
        await Assert.ThrowsAsync<Exception>(async () => await connection.WriteAsync("GET / HTTP/1.1\r\n\r\n"u8.ToArray(), CancellationToken.None));
        // The socket the callback saw, not the listener: on Linux a parallel test's connect to a port just freed can
        // land on this listener's port, so a connection waiting there says nothing about this socket (BL-4268).
        using (Socket view = new(new SafeSocketHandle(seen, ownsHandle: false)))
        {
            Assert.IsFalse(view.Connected, "the socket the callback saw is connected");
        }

        await connection.DisposeAsync();
        CollectionAssert.AreEqual(new[] { "close" }, calls);
    }

    [TestMethod]
    public void AttemptSocketOpener_SockoptWithoutOpenSocketNotSupplyingSockets_GivesTheEngineNumberAndProceeds()
    {
        List<nint> seen = [];
        SocketCallbackConnector.AttemptSocketOpener opener = new(
            new EasyCallbackOptions
            {
                SocketOption = (socket, _) =>
                {
                    seen.Add(socket.Handle);
                    return SocketOptionResult.AlreadyConnected;
                },
            },
            new CurlSocketHandles(),
            suppliesSockets: false);

        TcpSocketOpenDecision decision = opener.Open(new IPEndPoint(IPAddress.Loopback, 1));

        Assert.AreSame(TcpSocketOpenDecision.Proceed, decision);
        CollectionAssert.AreEqual(new nint[] { 1 }, seen);
    }

    private static SocketCallbackConnector Create(EasyCallbackOptions callbacks) =>
        new(new TcpConnector(new LoopbackResolver(), new TcpDialer(), new PassThroughTlsProvider(), TimeProvider.System), callbacks, new CurlSocketHandles(), TimeProvider.System);

    private static ConnectTarget TargetOf(Socket listener) => new("127.0.0.1", ((IPEndPoint)listener.LocalEndPoint!).Port, UseTls: false);

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
}
