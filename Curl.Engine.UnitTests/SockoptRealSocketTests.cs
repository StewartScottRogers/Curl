using System.Net;
using System.Net.Sockets;
using Curl.Api.Callbacks;
using Curl.Networking;
using Curl.Protocol.Abstractions;

namespace Curl.Engine;

/// <summary>
/// BL-3907: with only <c>CURLOPT_SOCKOPTFUNCTION</c> and <c>CURLOPT_CLOSESOCKETFUNCTION</c> set, the socket-option
/// callback gets the operating system socket the connect uses, open and unconnected, so <c>getsockopt(SO_TYPE)</c>
/// answers <c>SOCK_STREAM</c>, and the close-socket callback gets that same handle, as libcurl 8.22.0 does. The
/// loopback listener is connected without a byte sent.
/// </summary>
[TestClass]
public sealed class SockoptRealSocketTests
{
    [TestMethod]
    public async Task ConnectAsync_OnlySockoptAndCloseSocket_SockoptSocketIsAStreamSocketAndCloseSocketGetsTheSameHandle()
    {
        using Socket listener = new(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        listener.Bind(new IPEndPoint(IPAddress.Loopback, 0));
        listener.Listen();
        List<nint> sockoptHandles = [];
        List<int> socketTypes = [];
        List<nint> closedHandles = [];
        SocketCallbackConnector connector = new(
            new TcpConnector(new LoopbackResolver(), new TcpDialer(), new PassThroughTlsProvider(), TimeProvider.System),
            new EasyCallbackOptions
            {
                SocketOption = (socket, _) =>
                {
                    using Socket view = new(new SafeSocketHandle(socket.Handle, ownsHandle: false));
                    sockoptHandles.Add(socket.Handle);
                    socketTypes.Add((int)view.GetSocketOption(SocketOptionLevel.Socket, SocketOptionName.Type)!);
                    return SocketOptionResult.Ok;
                },
                CloseSocket = socket =>
                {
                    closedHandles.Add(socket.Handle);
                    return 0;
                },
            },
            new CurlSocketHandles(),
            TimeProvider.System);

        ConnectResult result = await connector.ConnectAsync(new ConnectTarget("127.0.0.1", ((IPEndPoint)listener.LocalEndPoint!).Port, UseTls: false), CancellationToken.None);
        await result.Connection!.DisposeAsync();

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        CollectionAssert.AreEqual(new[] { SocketCallbackConnector.StreamSocketType }, socketTypes);
        CollectionAssert.AreEqual(sockoptHandles, closedHandles);
    }

    private sealed class LoopbackResolver : IDnsResolver
    {
        public ValueTask<IReadOnlyList<IPAddress>> ResolveAsync(string host, CancellationToken cancellationToken) =>
            ValueTask.FromResult<IReadOnlyList<IPAddress>>([IPAddress.Loopback]);
    }
}
