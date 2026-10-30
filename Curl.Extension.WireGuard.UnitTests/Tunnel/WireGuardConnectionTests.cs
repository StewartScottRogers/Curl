using System.Net;
using Curl.Extension.WireGuard.Tunnel;

namespace Curl.Extension.WireGuard.UnitTests.Tunnel;

/// <summary>Checks the connection that owns a tunnel's TCP stream, UDP socket and keys.</summary>
[TestClass]
public sealed class WireGuardConnectionTests
{
    [TestMethod]
    public async Task ReadWriteFlushAndDispose_OverTheStream_PassThroughAndCloseEverything()
    {
        var (peer, tunnel, _) = await WireGuardPacketTunnelTests.OpenTunnelAsync();
        MemoryStream stream = new();
        IPEndPoint remote = new(IPAddress.Parse("10.0.0.1"), 80);
        WireGuardConnection connection = new(stream, peer, tunnel, remote);

        await connection.WriteAsync(new byte[] { 1, 2 }, CancellationToken.None);
        await connection.FlushAsync(CancellationToken.None);
        stream.Position = 0;
        byte[] buffer = new byte[2];
        int read = await connection.ReadAsync(buffer, CancellationToken.None);
        await connection.DisposeAsync();

        Assert.IsFalse(connection.IsSecure);
        Assert.AreEqual(remote, connection.RemoteEndPoint);
        Assert.AreEqual(2, read);
        CollectionAssert.AreEqual(new byte[] { 1, 2 }, buffer);
        Assert.IsFalse(stream.CanRead);
        Assert.IsTrue(peer.IsDisposed);
    }
}
