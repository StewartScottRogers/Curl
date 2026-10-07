using System.Net;

namespace Curl.Networking;

/// <summary>
/// Pins <see cref="DnsSocketOpener" />'s UDP side, which opens a loopback socket and sends nothing;
/// its TCP connect is an ADR-0083 adapter measured by the Integration run.
/// </summary>
[TestClass]
public sealed class DnsSocketOpenerTests
{
    [TestMethod]
    public async Task OpenDatagramChannel_BindsTheLocalAddressItIsGiven()
    {
        var server = new IPEndPoint(IPAddress.Loopback, 53);

        var channel = (UdpDatagramChannel)new DnsSocketOpener().OpenDatagramChannel(server, IPAddress.Loopback);
        await using (channel)
        {
            Assert.AreEqual(IPAddress.Loopback, ((IPEndPoint)channel.LocalEndPoint).Address);
            Assert.AreEqual(server, channel.ServerEndPoint);
        }
    }

}
