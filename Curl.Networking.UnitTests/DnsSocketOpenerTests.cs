using System.Net;
using Curl.Testing;

namespace Curl.Networking;

/// <summary>
/// Pins <see cref="DnsSocketOpener" />'s UDP side, which opens a loopback socket and sends nothing;
/// its TCP connect is an ADR-0083 adapter measured by the Integration run.
/// </summary>
[TestClass]
public sealed class DnsSocketOpenerTests
{
    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    public async Task OpenDatagramChannel_BindsTheLocalAddressItIsGiven()
    {
        var server = new IPEndPoint(IPAddress.Loopback, 53);
        Diagnostics.Arrange("server", server);
        Diagnostics.Arrange("local address", IPAddress.Loopback);

        var channel = (UdpDatagramChannel)new DnsSocketOpener().OpenDatagramChannel(server, IPAddress.Loopback);
        await using (channel)
        {
            var localAddress = ((IPEndPoint)channel.LocalEndPoint).Address;
            Diagnostics.Act("bound local address", localAddress);
            Diagnostics.Act("server end point", channel.ServerEndPoint);
            Diagnostics.Assert("bound local address", IPAddress.Loopback, localAddress);
            Diagnostics.Assert("server end point", server, channel.ServerEndPoint);
            Assert.AreEqual(IPAddress.Loopback, ((IPEndPoint)channel.LocalEndPoint).Address);
            Assert.AreEqual(server, channel.ServerEndPoint);
        }
    }

}
