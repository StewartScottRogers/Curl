using System.Net.Sockets;

namespace Curl.Networking;

/// <summary>
/// Pins that a .NET listener's accept takes the connection <see cref="TcpDialer.DialUnixSocketAsync" />
/// opens, as it takes upstream curl's, on every platform the fast tests run on (BL-5686: on macOS a
/// .NET server's accept failed with "Destination is too short" for the port's client). No byte is
/// exchanged, so this is a unit test (CLAUDE.md).
/// </summary>
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
}
