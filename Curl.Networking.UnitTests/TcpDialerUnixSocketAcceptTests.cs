using System.Net.Sockets;

namespace Curl.Networking;

/// <summary>
/// Pins that a .NET listener's accept takes the connection <see cref="TcpDialer.DialUnixSocketAsync" />
/// opens, as it takes upstream curl's, on every platform the fast tests run on, and that the dial sets
/// none of the TCP options a TCP dial sets (BL-5686). No byte is exchanged, so this is a unit test
/// (CLAUDE.md).
/// </summary>
/// <remarks>
/// The listener's path is absolute and longer than 14 bytes on purpose. On macOS the kernel reports an
/// unbound peer as a 16-byte <c>sockaddr_un</c>, and .NET's accept copies it into a buffer sized to the
/// listener's own address, so a .NET listener on a shorter path (<c>s.sock</c>, <c>surl.sock</c>) dies
/// with "Destination is too short" on its first accept from any client: the port, <c>/usr/bin/curl</c>
/// and <c>nc -U</c> alike (measured on macos-latest, probe run 45069983006). That is the server's
/// fault, not the client's, and here it would kill the test host.
/// </remarks>
[TestClass]
public sealed class TcpDialerUnixSocketAcceptTests
{
    [TestMethod]
    public async Task DialUnixSocketAsync_ToADotNetListener_IsAccepted()
    {
        var path = Path.Combine(Path.GetTempPath(), $"bl5686-{Guid.NewGuid():N}.sock");
        using var listener = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
        listener.Bind(new UnixDomainSocketEndPoint(path));
        listener.Listen(1);
        try
        {
            var accepting = listener.AcceptAsync();

            await using var connection = await new TcpDialer().DialUnixSocketAsync(new UnixSocketAddress(path, IsAbstract: false), CancellationToken.None);
            using var accepted = await accepting;

            Assert.IsTrue(accepted.Connected);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [TestMethod]
    public async Task DialUnixSocketAsync_WithCurlsDefaultTcpOptions_SetsNeitherNoDelayNorKeepAlive()
    {
        var path = Path.Combine(Path.GetTempPath(), $"bl5686-{Guid.NewGuid():N}.sock");
        using var listener = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
        listener.Bind(new UnixDomainSocketEndPoint(path));
        listener.Listen(1);
        try
        {
            var dialer = new TcpDialer();
            Assert.IsTrue(dialer.SocketOptions.NoDelay && dialer.SocketOptions.KeepAlive, "the TCP options the Unix dial must leave off are on for a TCP dial");
            nint handle = 0;
            var accepting = listener.AcceptAsync();

            await using var connection = await dialer.PublishingSocketsTo(created => handle = created)
                .DialUnixSocketAsync(new UnixSocketAddress(path, IsAbstract: false), CancellationToken.None);
            using var accepted = await accepting;
            using var dialed = new Socket(new SafeSocketHandle(handle, ownsHandle: false));

            Assert.IsFalse(IsOptionOn(dialed, SocketOptionLevel.Tcp, SocketOptionName.NoDelay), "TCP_NODELAY on a Unix socket");
            Assert.IsFalse(IsOptionOn(dialed, SocketOptionLevel.Socket, SocketOptionName.KeepAlive), "SO_KEEPALIVE on a Unix socket");
        }
        finally
        {
            File.Delete(path);
        }
    }

    // A platform that refuses to read a TCP option from a Unix socket has certainly not set it.
    private static bool IsOptionOn(Socket socket, SocketOptionLevel level, SocketOptionName name)
    {
        try
        {
            return socket.GetSocketOption(level, name) is int value && value != 0;
        }
        catch (SocketException)
        {
            return false;
        }
    }
}
