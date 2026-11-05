using System.Net;
using System.Net.Sockets;

namespace Curl.Extension.VirtualMachineSocket;

/// <summary>
/// Checks that <see cref="SystemVirtualMachineSocketOpener" /> hands the connection a stream that owns its socket and
/// closes the socket on every failure, with a loopback TCP socket standing in for the vsock or Hyper-V one.
/// </summary>
[TestClass]
public sealed class SystemVirtualMachineSocketOpenerTests
{
    private static VirtualMachineSocketAddress Address()
    {
        Assert.IsTrue(VirtualMachineSocketAddress.TryParse("3:5000", out VirtualMachineSocketAddress? address));
        return address!;
    }

    private static Socket NewTcpSocket() => new(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

    [TestMethod]
    public async Task OpenAsync_StreamDisposed_ClosesTheSocket()
    {
        using TcpListener listener = new(IPAddress.Loopback, 0);
        listener.Start();
        using Socket socket = NewTcpSocket();
        SystemVirtualMachineSocketOpener opener = new(_ => socket, (connecting, _) => connecting.Connect(listener.LocalEndpoint));

        Stream stream = await opener.OpenAsync(Address(), CancellationToken.None);
        using Socket accepted = await listener.AcceptSocketAsync();
        await stream.DisposeAsync();

        Assert.IsTrue(socket.SafeHandle.IsClosed);
    }

    [TestMethod]
    public async Task OpenAsync_ConnectFails_ClosesTheSocketAndThrows()
    {
        using Socket socket = NewTcpSocket();
        SystemVirtualMachineSocketOpener opener = new(_ => socket, (_, _) => throw new SocketException((int)SocketError.ConnectionRefused));

        await Assert.ThrowsExactlyAsync<SocketException>(async () => await opener.OpenAsync(Address(), CancellationToken.None));

        Assert.IsTrue(socket.SafeHandle.IsClosed);
    }

    [TestMethod]
    public async Task OpenAsync_CancelledWhileConnecting_ClosesTheSocketAndThrows()
    {
        using Socket socket = NewTcpSocket();
        using CancellationTokenSource cancellation = new();
        SystemVirtualMachineSocketOpener opener = new(_ => socket, (_, _) => cancellation.Cancel());

        await Assert.ThrowsExactlyAsync<OperationCanceledException>(async () => await opener.OpenAsync(Address(), cancellation.Token));

        Assert.IsTrue(socket.SafeHandle.IsClosed);
    }
}
