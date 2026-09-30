using System.Net;

using Curl.Networking.Fakes;
using Curl.Protocol.Abstractions;

namespace Curl.Networking;

/// <summary>
/// Pins the members of <see cref="LocalBindingTcpDialer" /> that <see cref="TcpConnector" /> does not
/// reach; its choice of local address is pinned through the connector in
/// <c>TcpConnectorTests.LocalBinding</c> (BL-600).
/// </summary>
[TestClass]
public sealed class LocalBindingTcpDialerTests
{
    [TestMethod]
    public async Task DialAsync_WithNullEndPoint_ThrowsArgumentNullException()
    {
        var dialer = new LocalBindingTcpDialer(new FakeTcpDialer(), new LocalBinding(null, null, null, 0, 1), new SystemNetworkInterfaceLookup(), new FakeDnsResolver());

        var exception = await Assert.ThrowsExactlyAsync<ArgumentNullException>(
            async () => await dialer.DialAsync(null!, CancellationToken.None));

        Assert.AreEqual("endPoint", exception.ParamName);
    }

    [TestMethod]
    public async Task DialFromAsync_PassesTheLocalEndAsGiven()
    {
        var inner = new FakeTcpDialer { DialOutcome = _ => new FakeConnection() };
        var dialer = new LocalBindingTcpDialer(inner, new LocalBinding("127.0.0.1", "127.0.0.1", null, 40000, 2), new SystemNetworkInterfaceLookup(), new FakeDnsResolver());
        var endPoint = new IPEndPoint(IPAddress.Loopback, 80);
        var localEndPoint = new IPEndPoint(IPAddress.Loopback, 50001);

        await dialer.DialFromAsync(endPoint, localEndPoint, 7, CancellationToken.None);

        Assert.AreEqual((endPoint, localEndPoint, 7), inner.BoundDials.Single());
    }

    [TestMethod]
    public async Task DialUnixSocketAsync_DialsTheSocketUnbound()
    {
        var connection = new FakeConnection();
        var inner = new FakeTcpDialer { UnixSocketDialOutcome = _ => connection };
        var dialer = new LocalBindingTcpDialer(inner, new LocalBinding(null, null, null, 40000, 2), new SystemNetworkInterfaceLookup(), new FakeDnsResolver());
        var address = new UnixSocketAddress("/tmp/curl.sock", false);

        var dialed = await dialer.DialUnixSocketAsync(address, CancellationToken.None);

        Assert.AreSame(connection, dialed);
        Assert.AreSame(address, inner.DialedUnixSockets.Single());
        Assert.IsEmpty(inner.BoundDials);
    }

}
